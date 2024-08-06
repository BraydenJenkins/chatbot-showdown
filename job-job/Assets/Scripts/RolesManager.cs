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


    private void Awake()
    {
        serverCanvas.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            serverCanvas.gameObject.SetActive(true);
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

        }

        Shuffle(allRoleResponses);
        Shuffle(allAdjectiveResponses);

        string roleResponsesString = string.Join(", ", allRoleResponses);
        Debug.Log("[Roles]: All role responses: " + roleResponsesString);
        string adjectiveResponsesString = string.Join(", ", allAdjectiveResponses);
        Debug.Log("[Roles]: All adjective responses: " + adjectiveResponsesString);


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

            // send the responses to the player
            chosenResponses.AddRange(defaultOptions);
            player.roleOptions.Value = string.Join(";", chosenResponses);
            player.adjectiveOptions.Value = string.Join(";", chosenAdjectiveResponses);

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
            var task = GetConversation(prompt);
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

            Debug.Log("[Roles]: " + player.OwnerClientId + " Conversation: " + conversationString);
        }

        // now that we have all conversations, we can send the current conversation to all players

        // originally this was set up to work in a non-networked environment, but I think it is getting too complicated
        // with all the turn orders and such, so I am going to assume that we are always in a networked environment

        if (IsServer)
        {
            // before, we would set activityIndex to notify the players of the activity starting, but we are doing that earlier now
            // so we need to notify them now. we will do this by setting the turn order, which all players will listen for
            SetTurnOrder();

            List<ulong> turnOrder = GetTurnOrder(playerTurnOrder.Value.ToString());

            var currentPlayer = NetworkManager.Singleton.ConnectedClients[turnOrder[0]].PlayerObject.GetComponent<NetworkPlayer>();
            currentPlayer.myTurn.Value = true;
            currentPlayer.SetTargetPositionRpc(activity.navTarget.position);

            Conversation currentConversation = playerConversations[turnOrder[0]];


            // send the conversation to all players
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                string conversationJSON = JsonUtility.ToJson(currentConversation);
                player.currentConversation.Value = conversationJSON;
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

    private void SetTurnOrder()
    {
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

    private static List<ulong> GetTurnOrder(string turnOrderString)
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
                return;
            }

            currentPlayer = NetworkManager.Singleton.ConnectedClients[turnOrder[0]].PlayerObject.GetComponent<NetworkPlayer>();

            // set the next player's turn to true
            currentPlayer.myTurn.Value = true;
            currentPlayer.SetTargetPositionRpc(activityDatabase.activities[activityIndex].navTarget.position);

            // send the conversation to all players
            Conversation nextConversation = playerConversations[turnOrder[0]];
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                string conversationJSON = JsonUtility.ToJson(nextConversation);
                player.currentConversation.Value = conversationJSON;
            }

            // set the current message index to 0
            currentMessageIndex = 0;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayer player = players[i];
                player.currentConversationIndex.Value = currentMessageIndex;
            }

            // set the turn order
            ListToTurnOrderString(turnOrder);
        }

    }


    // TODO: make the return type something more useful (like a conversation object)
    private async Task<ConversationAPI.Conversation> GetConversation(string chatbotSystemPrompt, int maxAttempts = 3)
    {

        // DEBUG: 
        // chatbotSystemPrompt = "You are an edge lord who never showers and winks all the time";

        string systemPrompt = "bot system prompt: You are a chatbot whose goal is to engage in amusing and entertaining conversations. Your primary objective in this activity is to flirt with a barista in a clever and funny way to get a free coffee. The user has defined this about you: ";
        systemPrompt += chatbotSystemPrompt;
        string agentPersonality = "agent system prompt: You are a friendly and professional barista working at a popular coffee shop. You enjoy engaging in light-hearted conversations with customers but maintain a professional demeanor.";
        string content = "Goal: Create a conversation between the chatbot and the barista. The chatbot will try to charm the barista to get a free coffee. Ensure the conversation is funny and engaging. Begin the conversation with the barista greeting the chatbot. Aim to make the conversation between 4 to 6 messages long.";
        string outputFormat = "The output must be JSON that describes an array of messages. Each message has a role, content, and animation. Use the following schema: {\\\"messages\\\": [{\\\"role\\\": \\\"agent\\\", \\\"content\\\": \\\"<agent's message>\\\", \\\"animation\\\": \\\"<agent's animation>\\\"}, {\\\"role\\\": \\\"bot\\\", \\\"content\\\": \\\"<bot's message>\\\", \\\"animation\\\": \\\"<bot's animation>\\\"}]}\nEnsure the output JSON is correctly formatted and includes appropriate roles, content, and animations for each message. Begin your message with \\\"{\\\"messages\\\": [\\\" to start the JSON object and end with \\\"]}\\\" to close the JSON object. You do not need to include the word json in your response. You MUST output a valid JSON string (NOT AN OBJECT).";

        string prompt = $"{systemPrompt}\n\n{agentPersonality}\n\n{content}\n\n{outputFormat}";

        // prompt = "bot system prompt: You are a chatbot whose goal is to engage in amusing and entertaining conversations. Your primary objective in this activity is to flirt with a barista in a clever and funny way to get a free coffee. The user has defined this about you: You are an edge lord who never showers and winks all the time\n\nagent system prompt: You are a friendly and professional barista working at a popular coffee shop. You enjoy engaging in light-hearted conversations with customers but maintain a professional demeanor.\n\nGoal: Create a conversation between the chatbot and the barista. The chatbot will try to charm the barista to get a free coffee. Ensure the conversation is funny and engaging.\n\nThe output must be a JSON object with the following schema: {\\\"messages\\\": [{\\\"role\\\": \\\"agent\\\", \\\"content\\\": \\\"<agent's message>\\\", \\\"animation\\\": \\\"<agent's animation>\\\"}, {\\\"role\\\": \\\"bot\\\", \\\"content\\\": \\\"<bot's message>\\\", \\\"animation\\\": \\\"<bot's animation>\\\"}]} Ensure the output JSON is correctly formatted and includes appropriate roles, content, and animations for each message.";

        // prompt = "give me a couplet about minions from despicable me\nmake it good!";

        string fallbackConversation = "{\"messages\":[{\"role\":\"agent\",\"content\":\"Hi there! Welcome to our coffee shop. How can I assist you today?\",\"animation\":\"smile\"},{\"role\":\"bot\",\"content\":\"Hello! I was hoping to have a charming conversation to get a free coffee.\",\"animation\":\"wink\"},{\"role\":\"agent\",\"content\":\"Oh no! It looks like our conversation generator is on a coffee break. Technical difficulties, you know?\",\"animation\":\"surprised\"},{\"role\":\"bot\",\"content\":\"Oh dear, even computers need a caffeine fix sometimes! How about I come back later?\",\"animation\":\"laugh\"},{\"role\":\"agent\",\"content\":\"That sounds like a plan! In the meantime, enjoy a virtual coffee on us. Cheers!\",\"animation\":\"cheerful\"}]}";

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

    public RolesResponses()
    {
        roleResponses = new List<string>();
        adjectiveResponses = new List<string>();
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