using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class JobJobManager : NetworkBehaviour
{
    [SerializeField] private Canvas serverCanvas;

    // TODO: allow database swapping at runtime (would be cool, must sync to clients)
    [SerializeField] private QuestionDatabase questionDatabase;
    private Dictionary<ulong, bool> playerAnswers = new Dictionary<ulong, bool>();

    private List<NetworkPlayer> players = new List<NetworkPlayer>();


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

    public void SendRandomQuestionToAllPlayers()
    {
        if (!IsServer)
            return;

        players.Clear();
        playerAnswers.Clear();
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            var player = client.PlayerObject.GetComponent<NetworkPlayer>();
            players.Add(player);
            playerAnswers[client.ClientId] = false;
        }

        // TODO: networkvariable on the index of the question rather than the question itself (in NetworkPlayer)

        for (int i = 0; i < players.Count; i++)
        {
            var randomIndex = Random.Range(0, questionDatabase.questions.Count);
            var randomQuestion = questionDatabase.questions[randomIndex];

            NetworkPlayer player = players[i];

            player.question.Value = randomQuestion;
            playerAnswers[player.OwnerClientId] = false;
            Debug.Log("Sent question to " + player.OwnerClientId + ": " + randomQuestion);
            player.answer.OnValueChanged += (prev, current) =>
            {
                OnAnswerReceived(prev, current, player.OwnerClientId);
            };
        }
    }

    private void OnAnswerReceived(FixedString512Bytes prev, FixedString512Bytes current, ulong clientId)
    {
        // do something
        Debug.Log("Answer received from " + clientId + ": " + current);

        // remove the event listener
        var player = players.Find(p => p.OwnerClientId == clientId);
        if (player == null)
        {
            Debug.LogError("Player " + clientId + " not found");
            return;
        }
        player.answer.OnValueChanged -= (prev, current) =>
        {
            OnAnswerReceived(prev, current, clientId);
        };

        playerAnswers[clientId] = true;

        CheckReceivedAllAnswers();
    }

    private void CheckReceivedAllAnswers()
    {
        if (!IsServer)
            return;

        Debug.Log("Connected players:");
        foreach (var player in players)
        {
            Debug.Log(player.OwnerClientId);
        }

        foreach (var player in playerAnswers)
        {
            Debug.Log("Player " + player.Key + " answered: " + player.Value);
            if (!player.Value)
                return;
        }

        Debug.Log("All players have answered");

        // need to notify players (in some form or another) that all players have answered
        // next step is to send the answer fragments to the players

        // there are a number of ways to do this, but for now:
        // for each player, send random fragments of other players' answers

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var otherPlayers = players.FindAll(p => p.OwnerClientId != player.OwnerClientId);

            for (int j = 0; j < otherPlayers.Count; j++)
            {
                var otherPlayer = otherPlayers[j];
                var otherPlayerAnswer = otherPlayer.answer.Value.ToString();
                // split the answer into fragments
                var fragments = otherPlayerAnswer.Split(' ');
                // shuffle the fragments, then take half
                var randomFragments = new List<string>(fragments);
                // shuffle 
                for (int k = 0; k < randomFragments.Count; k++)
                {
                    var temp = randomFragments[k];
                    var randomIndex = Random.Range(0, randomFragments.Count);
                    randomFragments[k] = randomFragments[randomIndex];
                    randomFragments[randomIndex] = temp;
                }
                var randomFragmentCount = randomFragments.Count / 2;
                var randomFragment = string.Join(" ", randomFragments.GetRange(0, randomFragmentCount));
                player.fragments.Value = randomFragment;
            }
        }


    }

}
