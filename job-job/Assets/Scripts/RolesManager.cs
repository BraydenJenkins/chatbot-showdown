using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ConversationAPI;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

[GenerateSerializationForType(typeof(byte))]
public class RolesManager : NetworkBehaviour
{
    [SerializeField] private Canvas serverCanvas;

    [SerializeField] private QuestionDatabase rolesQuestionDatabase;
    [SerializeField] private QuestionDatabase adjectivesQuestionDatabase;

    [SerializeField] private AnswerDatabase exampleRoleAnswers;
    [SerializeField] private AnswerDatabase exampleAdjectiveAnswers;
    [SerializeField] private AnswerDatabase exampleFreeAnswers;

    [SerializeField] private string[] defaultOptions;

    // using a network variable to keep track of the timer start for clients
    [SerializeField] private int timerDuration = 30;
    public int GetTimerDuration() { return timerDuration; }
    public NetworkVariable<long> timerStart = new NetworkVariable<long>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // but using a local variable to calculate the time elapsed
    private float localTimerStart;
    private bool timerRunning = false;

    private enum RolesState
    {
        RoleQuestions,
        AdjectiveQuestions,
        FreeQuestion,
        BotCreation
    }
    private RolesState currentState = RolesState.RoleQuestions;



    private List<NetworkPlayer> players = new List<NetworkPlayer>();

    private Dictionary<ulong, RolesResponses> responses = new Dictionary<ulong, RolesResponses>();

    private Dictionary<ulong, bool> botCreated = new Dictionary<ulong, bool>();

    [SerializeField] private ActivityDatabase activityDatabase;

    private ThinkerModule thinkerModule;

    public bool fakeThinkerModule = false;

    private int currentMessageIndex = 0;

    private int activityIndex = 0;

    // networklist has bugs and is prone to memory leaks
    // should switch to one in the future, but for a quick and dirty solution, we will use a netvar
    // TODO: implement a proper networklist
    // public NetworkList<ulong> playerTurnOrder;
    public NetworkVariable<FixedString512Bytes> playerTurnOrder = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Dictionary<ulong, Conversation> playerConversations = new Dictionary<ulong, Conversation>();

    private Dictionary<ulong, ulong> playerVotes = new Dictionary<ulong, ulong>();
    // 0 - not voting, 1 - voting started, 2 - voting ended, 3 - end game
    public NetworkVariable<int> votingState = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private CanvasGroup nextRoundButton;


    private void Awake()
    {
        serverCanvas.gameObject.SetActive(false);

        nextRoundButton.interactable = false;
        nextRoundButton.blocksRaycasts = false;
        nextRoundButton.alpha = 0;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // serverCanvas.gameObject.SetActive(true);
        }
    }

    // once we start the game, the first step is to send everyone a random role question
    // then start a timer


    public void SendRoleQuestions()
    {
        if (!IsServer)
        {
            return;
        }

        players.Clear();
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            var player = client.PlayerObject.GetComponent<NetworkPlayer>();
            players.Add(player);
        }


        responses.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];

            int randomIndex = Random.Range(0, rolesQuestionDatabase.questions.Count);
            string randomQuestion = rolesQuestionDatabase.questions[randomIndex];

            player.roleQuestion.Value = randomQuestion;

            Debug.Log("[Roles]: " + player.playerName.Value + " has been sent the question: " + randomQuestion);

            responses.Add(player.OwnerClientId, new RolesResponses());

            // listen for answer from player
            player.roleAnswer.OnValueChanged += (prev, current) =>
            {
                OnRoleAnswerReceived(prev, current, player.OwnerClientId);
            };
        }

        currentState = RolesState.RoleQuestions;

        // set timer start to current time
        timerStart.Value = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        localTimerStart = Time.time;
        timerRunning = true;
    }

    private void OnRoleAnswerReceived(FixedString512Bytes prev, FixedString512Bytes current, ulong clientId)
    {
        if (currentState != RolesState.RoleQuestions)
        {
            return;
        }

        Debug.Log("[Roles]: Answer received from " + clientId + ": " + current);
        // Debug.Log("[Roles]: Previous answer from " + clientId + ": " + prev);

        responses[clientId].roleResponses.Add(current.ToString());
    }

    private void Update()
    {
        if (IsServer)
        {
            if (timerRunning)
            {
                float timeElapsed = Time.time - localTimerStart;
                if (timeElapsed >= timerDuration)
                {
                    switch (currentState)
                    {
                        case RolesState.RoleQuestions:
                            OnRoleTimerEnd();
                            break;
                        case RolesState.AdjectiveQuestions:
                            OnAdjectiveTimerEnded();
                            break;
                        case RolesState.FreeQuestion:
                            OnFreeTimerEnded();
                            break;
                        case RolesState.BotCreation:
                            break;
                    }
                }
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        playerTurnOrder.Dispose();

        if (IsServer)
        {


            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                player.roleAnswer.OnValueChanged -= (prev, current) =>
                {
                    OnRoleAnswerReceived(prev, current, player.OwnerClientId);
                };
                player.adjectiveAnswer.OnValueChanged -= (prev, current) =>
                {
                    OnAdjectiveAnswerReceived(prev, current, player.OwnerClientId);
                };
                player.bot.OnValueChanged -= (prev, current) =>
                {
                    OnBotCreated(prev, current, player.OwnerClientId);
                };
            }
        }
    }

    private void OnRoleTimerEnd()
    {
        Debug.Log("[Roles]: Timer has ended");

        // stop the timer
        timerRunning = false;

        // remove the listeners for role answers
        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];
            player.roleAnswer.OnValueChanged -= (prev, current) =>
            {
                OnRoleAnswerReceived(prev, current, player.OwnerClientId);
            };
        }


        // send adjective questions to players
        SendAdjectiveQuestions();
    }

    private void SendAdjectiveQuestions()
    {
        if (!IsServer)
        {
            return;
        }

        players.Clear();
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            var player = client.PlayerObject.GetComponent<NetworkPlayer>();
            players.Add(player);
        }

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];

            int randomIndex = Random.Range(0, adjectivesQuestionDatabase.questions.Count);
            string randomQuestion = adjectivesQuestionDatabase.questions[randomIndex];

            player.adjectiveQuestion.Value = randomQuestion;

            Debug.Log("[Roles]: " + player.playerName.Value + " has been sent the question: " + randomQuestion);

            // listen for answer from player
            player.adjectiveAnswer.OnValueChanged += (prev, current) =>
            {
                OnAdjectiveAnswerReceived(prev, current, player.OwnerClientId);
            };
        }

        currentState = RolesState.AdjectiveQuestions;

        // set timer start to current time
        timerStart.Value = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        localTimerStart = Time.time;
        timerRunning = true;
    }

    private void OnAdjectiveAnswerReceived(FixedString512Bytes prev, FixedString512Bytes current, ulong clientId)
    {
        if (currentState != RolesState.AdjectiveQuestions)
        {
            return;
        }

        Debug.Log("[Roles]: Answer received from " + clientId + ": " + current);

        responses[clientId].adjectiveResponses.Add(current.ToString());
    }

    private void OnAdjectiveTimerEnded()
    {
        Debug.Log("[Roles]: Timer has ended");

        // stop the timer
        timerRunning = false;

        // remove the listeners for adjective answers
        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];
            player.adjectiveAnswer.OnValueChanged -= (prev, current) =>
            {
                OnAdjectiveAnswerReceived(prev, current, player.OwnerClientId);
            };
        }

        // // now, we have all the responses.
        // // we can now go into bot creation
        // CompileResponses();
        // added free play question !
        SendFreePlayQuestion();
    }

    private void SendFreePlayQuestion()
    {
        if (!IsServer)
        {
            return;
        }

        players.Clear();
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            var player = client.PlayerObject.GetComponent<NetworkPlayer>();
            players.Add(player);
        }

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];

            string question = "Free play! List any person, thing, title, or descriptor.";

            player.freeQuestion.Value = question;

            Debug.Log("[Roles]: " + player.playerName.Value + " has been sent the question: " + question);

            // listen for answer from player
            player.freeAnswer.OnValueChanged += (prev, current) =>
            {
                OnFreeAnswerReceived(prev, current, player.OwnerClientId);
            };
        }

        currentState = RolesState.FreeQuestion;

        // set timer start to current time
        timerStart.Value = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        localTimerStart = Time.time;
        timerRunning = true;
    }

    private void OnFreeAnswerReceived(FixedString512Bytes prev, FixedString512Bytes current, ulong clientId)
    {
        if (currentState != RolesState.FreeQuestion)
        {
            return;
        }

        Debug.Log("[Roles]: Answer received from " + clientId + ": " + current);

        responses[clientId].freeResponses.Add(current.ToString());
    }

    private void OnFreeTimerEnded()
    {
        Debug.Log("[Roles]: Timer has ended");

        // stop the timer
        timerRunning = false;

        // remove the listeners for adjective answers
        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];
            player.freeAnswer.OnValueChanged -= (prev, current) =>
            {
                OnFreeAnswerReceived(prev, current, player.OwnerClientId);
            };
        }

        // now, we have all the responses.
        // we can now go into bot creation
        CompileResponses();
    }

    private void CompileResponses()
    {
        if (!IsServer)
        {
            return;
        }

        // we now have a dictionary of all role and adjective responses
        // we are going to shuffle them and send them out to each player randomly, but not to themselves

        currentState = RolesState.BotCreation;

        // before sending out the responses, we need to decide on the activity
        activityIndex = Random.Range(0, activityDatabase.activities.Length);

        players.Clear();
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            var player = client.PlayerObject.GetComponent<NetworkPlayer>();
            players.Add(player);
        }

        // compile a list of all responses and shuffle
        List<(string, ulong)> allRoleResponses = new List<(string, ulong)>();
        List<(string, ulong)> allAdjectiveResponses = new List<(string, ulong)>();
        List<(string, ulong)> allFreeResponses = new List<(string, ulong)>();

        foreach (var response in responses)
        {
            foreach (var roleResponse in response.Value.roleResponses)
            {
                allRoleResponses.Add((roleResponse, response.Key));
            }
            foreach (var adjectiveResponse in response.Value.adjectiveResponses)
            {
                allAdjectiveResponses.Add((adjectiveResponse, response.Key));
            }
            foreach (var freeResponse in response.Value.freeResponses)
            {
                allFreeResponses.Add((freeResponse, response.Key));
            }

        }

        Shuffle(allRoleResponses);
        Shuffle(allAdjectiveResponses);
        Shuffle(allFreeResponses);

        string roleResponsesString = string.Join(", ", allRoleResponses);
        Debug.Log("[Roles]: All role responses: " + roleResponsesString);
        string adjectiveResponsesString = string.Join(", ", allAdjectiveResponses);
        Debug.Log("[Roles]: All adjective responses: " + adjectiveResponsesString);
        string freeResponsesString = string.Join(", ", allFreeResponses);
        Debug.Log("[Roles]: All free responses: " + freeResponsesString);

        botCreated.Clear();

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];

            // send role responses in order, skipping any responses from the player
            List<(string, ulong)> roleResponses = new List<(string, ulong)>();
            List<string> chosenResponses = new List<string>();
            for (int j = 0; j < allRoleResponses.Count; j++)
            {
                if (allRoleResponses[j].Item2 != player.OwnerClientId)
                {
                    roleResponses.Add((allRoleResponses[j].Item1, allRoleResponses[j].Item2));
                }
            }

            string roleResponsesString2 = string.Join(", ", roleResponses);

            Debug.Log("[Roles]: " + player.playerName.Value + ": possible: " + roleResponsesString2);

            // roleResponses now contains all responses except the player's own
            // we want to give them the first 2 responses, if they exist
            while (chosenResponses.Count < 2 && roleResponses.Count > 0)
            {
                chosenResponses.Add(roleResponses[0].Item1);
                allRoleResponses.Remove(roleResponses[0]);
                roleResponses.RemoveAt(0);
            }
            if (chosenResponses.Count < 2)
            {
                // if there are not enough responses, add a random example response
                int randomIndex = Random.Range(0, exampleRoleAnswers.answers.Count);
                chosenResponses.Add(exampleRoleAnswers.answers[randomIndex]);
            }

            Debug.Log("[Roles]: " + player.playerName.Value + ": chosen: " + string.Join(", ", chosenResponses));

            // send adjective responses in order, skipping any responses from the player
            List<(string, ulong)> adjectiveResponses = new List<(string, ulong)>();
            List<string> chosenAdjectiveResponses = new List<string>();
            for (int j = 0; j < allAdjectiveResponses.Count; j++)
            {
                if (allAdjectiveResponses[j].Item2 != player.OwnerClientId)
                {
                    adjectiveResponses.Add((allAdjectiveResponses[j].Item1, allAdjectiveResponses[j].Item2));
                }
            }

            // adjectiveResponses now contains all responses except the player's own
            // we want to give them the first 2 responses, if they exist
            while (chosenAdjectiveResponses.Count < 2 && adjectiveResponses.Count > 0)
            {
                chosenAdjectiveResponses.Add(adjectiveResponses[0].Item1);
                allAdjectiveResponses.Remove(adjectiveResponses[0]);
                adjectiveResponses.RemoveAt(0);
            }
            if (chosenAdjectiveResponses.Count < 2)
            {
                // if there are not enough responses, add a random example response
                int randomIndex = Random.Range(0, exampleAdjectiveAnswers.answers.Count);
                chosenAdjectiveResponses.Add(exampleAdjectiveAnswers.answers[randomIndex]);
            }

            // free responses
            List<(string, ulong)> freeResponses = new List<(string, ulong)>();
            List<string> chosenFreeResponses = new List<string>();
            for (int j = 0; j < allFreeResponses.Count; j++)
            {
                if (allFreeResponses[j].Item2 != player.OwnerClientId)
                {
                    freeResponses.Add((allFreeResponses[j].Item1, allFreeResponses[j].Item2));
                }
            }

            // freeResponses now contains all responses except the player's own
            // we want to give them the first 2 responses, if they exist
            while (chosenFreeResponses.Count < 2 && freeResponses.Count > 0)
            {
                chosenFreeResponses.Add(freeResponses[0].Item1);
                allFreeResponses.Remove(freeResponses[0]);
                freeResponses.RemoveAt(0);
            }
            if (chosenFreeResponses.Count < 2)
            {
                // if there are not enough responses, add a random example response
                int randomIndex = Random.Range(0, exampleFreeAnswers.answers.Count);
                chosenFreeResponses.Add(exampleFreeAnswers.answers[randomIndex]);
            }

            // send the responses to the player
            chosenResponses.AddRange(defaultOptions);
            player.roleOptions.Value = string.Join(";", chosenResponses);
            player.adjectiveOptions.Value = string.Join(";", chosenAdjectiveResponses);
            player.freeOptions.Value = string.Join(";", chosenFreeResponses);

            // send the current activity to the player
            player.activityIndex.Value = activityIndex;


            botCreated.Add(player.OwnerClientId, false);

            // listen for bot creation
            player.bot.OnValueChanged += (prev, current) =>
            {
                OnBotCreated(prev, current, player.OwnerClientId);
            };
        }

    }

    private void OnBotCreated(FixedString512Bytes prev, FixedString512Bytes current, ulong clientId)
    {
        if (currentState != RolesState.BotCreation)
        {
            return;
        }

        Debug.Log("[Roles]: Bot created by " + clientId + ": " + current);

        // remove the event listener
        var player = players.Find(p => p.OwnerClientId == clientId);
        if (player == null)
        {
            Debug.LogError("Player " + clientId + " not found");
            return;
        }
        player.bot.OnValueChanged -= (prev, current) =>
        {
            OnBotCreated(prev, current, clientId);
        };

        botCreated[clientId] = true;

        CheckReceivedAllBots();
    }

    private void CheckReceivedAllBots()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (var player in botCreated)
        {
            if (!player.Value)
                return;
        }

        Debug.Log("[Roles]: All bots have been created");

        // the below was to send the activity to all players, but we are doing that earlier now

        // players.Clear();
        // var clients = NetworkManager.Singleton.ConnectedClientsList;
        // foreach (var client in clients)
        // {
        //     var player = client.PlayerObject.GetComponent<NetworkPlayer>();
        //     players.Add(player);
        // }

        // int activityIndex = 0;

        // for (int i = 0; i < players.Count; i++)
        // {
        //     NetworkPlayer player = players[i];

        //     player.activityIndex.Value = activityIndex;
        // }

        // start the activity

        var activity = activityDatabase.activities[activityIndex];
        StartCoroutine(RunActivity(activity));

    }

    public void Debug_RunActivity(int index)
    {
        var activity = activityDatabase.activities[index];

        if (IsServer)
        {
            players.Clear();
            var clients = NetworkManager.Singleton.ConnectedClientsList;
            foreach (var client in clients)
            {
                var player = client.PlayerObject.GetComponent<NetworkPlayer>();
                players.Add(player);
            }

            int activityIndex = index;

            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];

                player.activityIndex.Value = activityIndex;
            }
        }

        Debug.Log("[Roles]: DEBUG - Running activity " + index);

        StartCoroutine(RunActivity(activity));
    }


    private IEnumerator RunActivity(RolesActivity activity)
    {

        // NOTE: the IsServer below is to allow for activity testing when not in a networked environment
        List<string> botPrompts = new List<string>();
        // string botPrompt = "";
        List<Task<ConversationAPI.Conversation>> conversationTasks = new List<Task<ConversationAPI.Conversation>>();

        if (IsServer)
        {
            // use bot prompt to get the first conversation from gemini
            // botPrompt = currentPlayer.bot.Value.ToString();
            // swapping to batch all prompts
            foreach (var player in players)
            {
                botPrompts.Add(player.bot.Value.ToString());
            }
        }

        if (thinkerModule == null)
        {
            thinkerModule = new ThinkerModule();
        }

        foreach (var prompt in botPrompts)
        {
            Debug.Log("[Roles]: Prompt: " + prompt);
            var task = GetConversation(prompt, activity.activityDescription, activity.roleName, activity.conversationStarter);
            conversationTasks.Add(task);
        }

        yield return new WaitUntil(() => conversationTasks.All(t => t.IsCompleted));

        Debug.Log("[Roles]: All conversations received");

        foreach (var task in conversationTasks)
        {
            if (!task.IsCompletedSuccessfully)
            {
                Debug.LogError("[Roles]: Failed to get conversation. This should NEVER happen: " + task.Exception);
                yield break;
            }
        }

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];
            var playerConversation = conversationTasks[i].Result;
            playerConversations.Add(player.OwnerClientId, playerConversation);

            string conversationString = "";
            foreach (var message in playerConversation.messages)
            {
                conversationString += message.role + ": " + message.content + "\n";
            }

            // Debug.Log("[Roles]: " + player.OwnerClientId + " Conversation: " + conversationString);
        }

        // now that we have all conversations, we can send the current conversation to all players

        // originally this was set up to work in a non-networked environment, but I think it is getting too complicated
        // with all the turn orders and such, so I am going to assume that we are always in a networked environment

        if (IsServer)
        {
            Debug.Log("[Roles]: Server starting the activity");

            // before, we would set activityIndex to notify the players of the activity starting, but we are doing that earlier now
            // so we need to notify them now. we will do this by setting the turn order, which all players will listen for
            SetTurnOrder();

            List<ulong> turnOrder = GetTurnOrder(playerTurnOrder.Value.ToString());

            var currentPlayer = NetworkManager.Singleton.ConnectedClients[turnOrder[0]].PlayerObject.GetComponent<NetworkPlayer>();
            currentPlayer.myTurn.Value = true;
            currentPlayer.SetTargetPositionRpc(activity.navTarget.position);

            Conversation currentConversation = playerConversations[turnOrder[0]];

            string conversationJSON = GetConversationJSON(currentConversation);

            // send the conversation to all players
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                player.currentConversation.Value = conversationJSON;
                player.currentBotPrompt.Value = currentPlayer.bot.Value;
                player.currentAvatarIndex.Value = currentPlayer.avatarIndex.Value;
            }
        }



        // now, whoever's turn it is needs to be able to control the conversation
        // lets have a server network variable that keeps track of how far into the conversation we are
        // that the player can control

        if (IsServer)
        {
            currentMessageIndex = 0;

            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                player.currentConversationIndex.Value = currentMessageIndex;
            }
        }

        // all players should now have the conversation and be ready to listen for changes to the conversation index
        // so we need to give the current player control of the conversation

    }

    private string GetConversationJSON(ConversationAPI.Conversation currentConversation)
    {
        string conversationJSON = JsonUtility.ToJson(currentConversation);
        // check length of conversation because if it doesn't fit in FixedString4096 bytes, it won't send.

        if (conversationJSON.Length > 4096)
        {
            Debug.LogError("[Roles]: Conversation too long to send to players. Length: " + conversationJSON.Length);
            // let's remove all animations to make it smaller
            for (int i = 0; i < currentConversation.messages.Length; i++)
            {
                currentConversation.messages[i].animation = "";
            }
            conversationJSON = JsonUtility.ToJson(currentConversation);
            if (conversationJSON.Length > 4096)
            {
                Debug.LogError("[Roles]: Conversation still too long to send to players. Length: " + conversationJSON.Length);
                // remove messages until it fits
                while (conversationJSON.Length > 4096)
                {
                    currentConversation.messages = currentConversation.messages.Take(currentConversation.messages.Length - 1).ToArray();
                    conversationJSON = JsonUtility.ToJson(currentConversation);
                }
            }
        }

        return conversationJSON;
    }

    private void SetTurnOrder()
    {
        Debug.Log("[Roles]: Setting turn order!");

        if (!IsServer)
        {
            return;
        }

        List<ulong> order = new List<ulong>();

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            order.Add(client.ClientId);
        }

        Shuffle(order);

        ListToTurnOrderString(order);

    }

    public static List<ulong> GetTurnOrder(string turnOrderString)
    {
        List<ulong> order = new List<ulong>();

        string[] orderStrings = turnOrderString.Split(';');
        foreach (var orderString in orderStrings)
        {
            ulong clientId;
            if (ulong.TryParse(orderString, out clientId))
            {
                order.Add(clientId);
            }
        }

        return order;
    }

    private void ListToTurnOrderString(List<ulong> order)
    {
        playerTurnOrder.Value = string.Join(";", order);
    }

    [Rpc(SendTo.Server)]
    public void EndGameRpc()
    {
        if (!IsServer)
        {
            return;
        }

        // tell all players to go to the end game screen
        votingState.Value = 3;
    }

    [Rpc(SendTo.Server)]
    public void AdvanceConversationRpc(ulong SenderClientId)
    {
        if (!IsServer)
        {
            return;
        }

        // get the current player
        NetworkPlayer currentPlayer = null;
        foreach (var player in players)
        {
            if (player.myTurn.Value)
            {
                currentPlayer = player;
                break;
            }
        }

        if (currentPlayer == null)
        {
            Debug.LogError("No player has their turn");
            return;
        }

        // check if the current player is the one who called this method
        if (currentPlayer.OwnerClientId != SenderClientId)
        {
            Debug.LogError("Player " + SenderClientId + " is not the current player");
            return;
        }

        // if here, the current player is the one who called this method
        // so we need to advance the conversation
        currentMessageIndex++;
        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];
            player.currentConversationIndex.Value = currentMessageIndex;
        }

        // check if the conversation is over
        if (currentMessageIndex > playerConversations[SenderClientId].messages.Length)
        {
            // the conversation is over
            // so we need to move to the next player
            // and send the conversation to all players

            List<ulong> turnOrder = GetTurnOrder(playerTurnOrder.Value.ToString());

            // remove the current player from the turn order
            // and set their turn to false
            currentPlayer.myTurn.Value = false;

            turnOrder.RemoveAt(0);

            if (turnOrder.Count == 0)
            {
                Debug.Log("[Roles]: All players have had their turn");
                GoToVoting();
                return;
            }

            currentPlayer = NetworkManager.Singleton.ConnectedClients[turnOrder[0]].PlayerObject.GetComponent<NetworkPlayer>();

            // set the next player's turn to true
            currentPlayer.myTurn.Value = true;
            currentPlayer.SetTargetPositionRpc(activityDatabase.activities[activityIndex].navTarget.position);

            Debug.Log("[Roles]: Resetting conversation index and sending new conversation to all players.");

            // set the current message index to 0
            currentMessageIndex = 0;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                player.currentConversationIndex.Value = currentMessageIndex;
            }

            // send the conversation to all players
            Conversation nextConversation = playerConversations[turnOrder[0]];

            string conversationJSON = GetConversationJSON(nextConversation);

            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                player.currentConversation.Value = conversationJSON;
                player.currentBotPrompt.Value = currentPlayer.bot.Value;
                player.currentAvatarIndex.Value = currentPlayer.avatarIndex.Value;
            }

            // set the turn order
            ListToTurnOrderString(turnOrder);
        }

    }

    private void GoToVoting()
    {
        playerVotes.Clear();
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];

            playerVotes.Add(player.OwnerClientId, ulong.MaxValue);

            player.votedPlayer.OnValueChanged += (prev, current) =>
            {
                OnPlayerVoted(prev, current, player.OwnerClientId);
            };
        }

        // tell all players to go to the voting screen
        votingState.Value = 1;
    }

    private void OnPlayerVoted(ulong previousValue, ulong newValue, ulong voterId)
    {
        // remove the event listener
        var player = players.Find(p => p.OwnerClientId == voterId);
        if (player == null)
        {
            Debug.LogError("Player " + voterId + " not found");
            return;
        }
        player.bot.OnValueChanged -= (prev, current) =>
        {
            OnBotCreated(prev, current, voterId);
        };

        playerVotes[voterId] = newValue;

        CheckAllPlayersVoted();

    }

    private void CheckAllPlayersVoted()
    {
        if (!IsServer)
        {
            return;
        }

        foreach (var vote in playerVotes)
        {
            if (vote.Value == ulong.MaxValue)
                return;
        }

        // all players have voted
        // so we need to tally the votes and announce the winner
        Dictionary<ulong, int> voteCounts = new Dictionary<ulong, int>();

        foreach (var vote in playerVotes)
        {
            if (voteCounts.ContainsKey(vote.Value))
            {
                voteCounts[vote.Value]++;
            }
            else
            {
                voteCounts.Add(vote.Value, 1);
            }
        }

        foreach (var player in players)
        {
            if (voteCounts.ContainsKey(player.OwnerClientId))
                player.votes.Value = voteCounts[player.OwnerClientId];
            else
                player.votes.Value = 0;

            player.score.Value += player.votes.Value;
        }

        // TODO: fix scoring

        // give player with most votes 3 points, second most 2 points, third most 1 point
        // if there is a tie, all tied players get the same points
        // var sortedVotes = voteCounts.OrderByDescending(v => v.Value).ToList();

        // int points = 3;
        // int lastVotes = sortedVotes[0].Value;
        // for (int i = 0; i < sortedVotes.Count; i++)
        // {
        //     if (sortedVotes[i].Value < lastVotes)
        //     {
        //         points--;
        //     }
        //     players.Find(p => p.OwnerClientId == sortedVotes[i].Key).score.Value += points;
        //     lastVotes = sortedVotes[i].Value;
        // }


        // tell all players to show the total votes
        votingState.Value = 2;

        if (IsServer)
        {
            nextRoundButton.interactable = true;
            nextRoundButton.blocksRaycasts = true;
            nextRoundButton.alpha = 1;
        }

    }


    // TODO: make the return type something more useful (like a conversation object)
    private async Task<ConversationAPI.Conversation> GetConversation(string playerChatbotPersonality, string goal, string cpuRole, string cpuConversationStarter, int maxAttempts = 3)
    {

        // DEBUG: 
        // chatbotSystemPrompt = "You are an edge lord who never showers and winks all the time";

        // string systemPrompt = "bot system prompt: You are a chatbot whose goal is to engage in amusing and entertaining conversations. Your primary objective in this activity is to flirt with a barista in a clever and funny way to get a free coffee. The user has defined this about you: ";
        // systemPrompt += chatbotSystemPrompt;
        // string agentPersonality = "agent system prompt: You are a friendly and professional barista working at a popular coffee shop. You enjoy engaging in light-hearted conversations with customers but maintain a professional demeanor.";
        // string content = "Goal: Create a conversation between the chatbot and the barista. The chatbot will try to charm the barista to get a free coffee. Ensure the conversation is funny and engaging. Begin the conversation with the barista greeting the chatbot. Aim to make the conversation between 4 to 6 messages long. Do not stop after only two messages.";
        // string outputFormat = "The output must be JSON that describes an array of messages. Each message has a role, content, and animation. Use the following schema: {\\\"messages\\\": [{\\\"role\\\": \\\"agent\\\", \\\"content\\\": \\\"<agent's message>\\\", \\\"animation\\\": \\\"<agent's animation>\\\"}, {\\\"role\\\": \\\"bot\\\", \\\"content\\\": \\\"<bot's message>\\\", \\\"animation\\\": \\\"<bot's animation>\\\"}]}\nEnsure the output JSON is correctly formatted and includes appropriate roles, content, and animations for each message. Begin your message with \\\"{\\\"messages\\\": [\\\" to start the JSON object and end with \\\"]}\\\" to close the JSON object. You do not need to include the word json in your response. You MUST output a valid JSON string (NOT AN OBJECT).";

        // string prompt = $"{systemPrompt}\n\n{agentPersonality}\n\n{content}\n\n{outputFormat}";

        string mainPrompt = "You are the AI game master for a game where where players are challenged with a dialogue based goal and must build a chatbot to take on the the task. You will be given a Goal for the players, a role for the CPU character the player's chatbot must interact with, a conversation starter for the CPU role and a personality for the player's chatbot. Your role as the game master is to create a conversation between the players chatbot and the CPU character, starting with the conversation starter from the CPU. Conversations should be 4 to 6 messages long. 6 SENTENCES MAX. Do you best to expand on the personalities of the CPU and the player's chatbot. Conversations should respect the personalities given to the Players chatbot and the CPU but the conversation should be as over the top and funny as possible. Lines of dialouge can also consist of actions and movement by the characters in the scene, so use that to heighten conversations. Every conversation should slowly build in humor as the player's chatbot tries to achieve the goal until the Player's chatbot either fails or succeeds in the last line. MAKE SURE IT'S CLEAR IF THE PLAYER'S CHATBOT ACHIEVED OR FAILED IT'S GOAL IN THE LAST LINE.";

        string sceneInformation = "Scene Information:\nGoal: ";
        sceneInformation += goal;
        sceneInformation += "\nCPU Role: ";
        sceneInformation += cpuRole;
        sceneInformation += "\nCPU Conversation Starter: ";
        sceneInformation += cpuConversationStarter;
        sceneInformation += "\nPlayer Chatbot Personality: ";
        sceneInformation += playerChatbotPersonality;

        string outputFormat = "The output must be JSON that describes an array of messages. Each message has a role, content, and animation. Use the following schema: {\\\"messages\\\": [{\\\"role\\\": \\\"<CPU ROLE>\\\", \\\"content\\\": \\\"<CPU's message>\\\", \\\"animation\\\": \\\"<CPU's animation>\\\"}, {\\\"role\\\": \\\"player\\\", \\\"content\\\": \\\"<player's message>\\\", \\\"animation\\\": \\\"<player's animation>\\\"}]}\nEnsure the output JSON is correctly formatted and includes appropriate roles, content, and animations for each message. Begin your message with \\\"{\\\"messages\\\": [\\\" to start the JSON object and end with \\\"]}\\\" to close the JSON object. You do not need to include the word json in your response. You MUST output a valid JSON string (NOT AN OBJECT).";

        string prompt = $"{mainPrompt}\n\n{sceneInformation}\n\n{outputFormat}";

        string fallbackConversation = "{\"messages\":[{\"role\":\"agent\",\"content\":\"Hi there! Welcome to our coffee shop. How can I assist you today?\",\"animation\":\"smile\"},{\"role\":\"player\",\"content\":\"Hello! I was hoping to have a charming conversation to get a free coffee.\",\"animation\":\"wink\"},{\"role\":\"agent\",\"content\":\"Oh no! It looks like our conversation generator is on a coffee break. Technical difficulties, you know?\",\"animation\":\"surprised\"},{\"role\":\"player\",\"content\":\"Oh dear, even computers need a caffeine fix sometimes! How about I come back later?\",\"animation\":\"laugh\"},{\"role\":\"agent\",\"content\":\"That sounds like a plan! In the meantime, enjoy a virtual coffee on us. Cheers!\",\"animation\":\"cheerful\"}]}";

        Debug.Log(prompt);

        for (int attempts = 0; attempts < maxAttempts; attempts++)
        {
            try
            {
                Task<string> task;
                if (!fakeThinkerModule)
                {
                    task = thinkerModule.GetCompletion(prompt);
                }
                else
                {
                    string exampleConversation = "{\"messages\":[{\"role\":\"agent\",\"content\":\"Hi!\",\"animation\":\"neutral\"},{\"role\":\"bot\",\"content\":\"Hello there!\",\"animation\":\"wave\"},{\"role\":\"agent\",\"content\":\"How can I assist you today?\",\"animation\":\"question\"},{\"role\":\"bot\",\"content\":\"I need help with my account.\",\"animation\":\"thinking\"},{\"role\":\"agent\",\"content\":\"Sure, I can help with that. What seems to be the issue?\",\"animation\":\"neutral\"},{\"role\":\"bot\",\"content\":\"I forgot my password.\",\"animation\":\"sad\"}]}";

                    task = Task.FromResult(exampleConversation);
                }

                string result = await task;

                ConversationAPI.Conversation conversation = JsonUtility.FromJson<ConversationAPI.Conversation>(result);
                return conversation;
            }
            catch (Exception e)
            {
                Debug.LogError("Attempt " + (attempts + 1) + " failed: " + e.Message);
                if (attempts < maxAttempts - 1)
                {
                    // exponential backoff
                    await Task.Delay((int)Math.Pow(2, attempts) * 1000);
                }
                else
                {
                    Debug.LogError("[Roles]: Failed to get conversation multiple times: " + e.Message);
                    return JsonUtility.FromJson<ConversationAPI.Conversation>(fallbackConversation);
                }
            }
        }

        Debug.LogError("[Roles]: Failed to get conversation");
        return JsonUtility.FromJson<ConversationAPI.Conversation>(fallbackConversation);
    }

    // Helper method to shuffle a list
    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

}

public class RolesResponses
{
    public List<string> roleResponses;
    public List<string> adjectiveResponses;
    public List<string> freeResponses;

    public RolesResponses()
    {
        roleResponses = new List<string>();
        adjectiveResponses = new List<string>();
        freeResponses = new List<string>();
    }

}

namespace ConversationAPI
{
    [Serializable]
    public class Message
    {
        public string role;
        public string content;
        public string animation;
    }

    [Serializable]
    public class Conversation
    {
        public Message[] messages;
    }
}