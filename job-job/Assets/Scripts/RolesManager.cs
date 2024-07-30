using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Unity.Collections;
using Unity.Netcode;
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

    [SerializeField] private RolesActivity[] activities;

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

        players.Clear();
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            var player = client.PlayerObject.GetComponent<NetworkPlayer>();
            players.Add(player);
        }

        int activityIndex = 0;

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayer player = players[i];

            player.activityIndex.Value = activityIndex;
        }

        // start the activity

        var activity = activities[activityIndex];

        players[0].myTurn.Value = true;
        players[0].SetTargetPositionRpc(activity.navTarget.position);


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