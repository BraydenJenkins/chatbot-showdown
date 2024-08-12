using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    [SerializeField] private string[] requiredFragments;
    [SerializeField] private AnswerDatabase exampleAnswerDatabase;
    [SerializeField] private int minimumFragments = 30;

    private void Awake()
    {
        serverCanvas.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // serverCanvas.gameObject.SetActive(true);
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

            List<string> allFragments = new List<string>();

            for (int j = 0; j < otherPlayers.Count; j++)
            {
                var otherPlayer = otherPlayers[j];
                var otherPlayerAnswer = otherPlayer.answer.Value.ToString();
                // anything non-alphanumeric should be surrounded by spaces
                string pattern = @"([^\w\s])";
                otherPlayerAnswer = System.Text.RegularExpressions.Regex.Replace(otherPlayerAnswer, pattern, " $1 ");
                // remove all newlines and extra spaces
                otherPlayerAnswer = System.Text.RegularExpressions.Regex.Replace(otherPlayerAnswer, @"\s+", " ");
                // split the answer into fragments
                var fragments = otherPlayerAnswer.Split(' ');
                // shuffle the fragments, then take half
                var randomFragments = new List<string>(fragments);
                // shuffle 
                // for (int k = 0; k < randomFragments.Count; k++)
                // {
                //     var temp = randomFragments[k];
                //     var randomIndex = Random.Range(0, randomFragments.Count);
                //     randomFragments[k] = randomFragments[randomIndex];
                //     randomFragments[randomIndex] = temp;
                // }
                randomFragments = ShuffleFragmentGroups(randomFragments);
                var randomFragmentCount = randomFragments.Count / 2;
                allFragments.AddRange(randomFragments.GetRange(0, randomFragmentCount));
            }

            // now, if the required fragments are not in the list, add them at random positions (to make it less obvious)
            for (int j = 0; j < requiredFragments.Length; j++)
            {
                if (!allFragments.Contains(requiredFragments[j]))
                {
                    var randomIndex = Random.Range(0, allFragments.Count);
                    allFragments.Insert(randomIndex, requiredFragments[j]);
                }
            }

            // if there aren't enough fragments, add some random ones from the example answer database
            int trys = 0;
            int maxTrys = 5;
            while (allFragments.Count < minimumFragments && trys < maxTrys)
            {
                var randomIndex = Random.Range(0, exampleAnswerDatabase.answers.Count);
                var randomAnswer = exampleAnswerDatabase.answers[randomIndex];
                string pattern = @"([^\w\s])";
                randomAnswer = System.Text.RegularExpressions.Regex.Replace(randomAnswer, pattern, " $1 ");
                // remove all newlines and extra spaces
                randomAnswer = System.Text.RegularExpressions.Regex.Replace(randomAnswer, @"\s+", " ");
                var randomFragments = randomAnswer.Split(' ').ToList();
                // shuffle
                // for (int k = 0; k < randomFragments.Length; k++)
                // {
                //     var temp = randomFragments[k];
                //     var randomIndex2 = Random.Range(0, randomFragments.Length);
                //     randomFragments[k] = randomFragments[randomIndex2];
                //     randomFragments[randomIndex2] = temp;
                // }
                randomFragments = ShuffleFragmentGroups(randomFragments);
                var randomFragmentCount = randomFragments.Count / 2;
                allFragments.AddRange(randomFragments.Take(randomFragmentCount));

                allFragments = allFragments.Distinct().ToList();

                trys++;
            }

            // set all fragments to lowercase and remove duplicates
            allFragments = allFragments.ConvertAll(f => f.ToLower());
            allFragments = allFragments.Distinct().ToList();

            // finally, send the fragments to the player
            string fragmentsString = string.Join(" ", allFragments);
            // limit the length of the fragments string to fit into the FixedString512Bytes
            string truncatedFragments = fragmentsString.Substring(0, Mathf.Min(fragmentsString.Length, 500));
            if (truncatedFragments.Length < fragmentsString.Length)
            {
                Debug.LogWarning("Fragments string was truncated");
            }
            truncatedFragments = truncatedFragments.Trim();
            player.fragments.Value = truncatedFragments;
        }



        // now that we have sent the fragments to the players, start listening for their answers once again
        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            playerAnswers[player.OwnerClientId] = false;
            player.answer.OnValueChanged += (prev, current) =>
            {
                OnAnswerReceived(prev, current, player.OwnerClientId);
            };
        }
    }

    private void OnFragmentsAnswerReceived(FixedString512Bytes prev, FixedString512Bytes current, ulong clientId)
    {
        // do something
        Debug.Log("Fragments answer received from " + clientId + ": " + current);

        // remove the event listener
        var player = players.Find(p => p.OwnerClientId == clientId);
        if (player == null)
        {
            Debug.LogError("Player " + clientId + " not found");
            return;
        }
        player.fragments.OnValueChanged -= (prev, current) =>
        {
            OnFragmentsAnswerReceived(prev, current, clientId);
        };

        playerAnswers[clientId] = true;

        CheckReceivedAllFragmentsAnswers();
    }

    private void CheckReceivedAllFragmentsAnswers()
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


    }

    public static List<string> ShuffleFragmentGroups(List<string> fragments, int groupSize = 3)
    {
        // Step 1: Split the list into groups of groupSize
        List<List<string>> groups = new List<List<string>>();
        for (int i = 0; i < fragments.Count; i += groupSize)
        {
            List<string> group = new List<string>();
            for (int j = 0; j < groupSize && i + j < fragments.Count; j++)
            {
                group.Add(fragments[i + j]);
            }
            groups.Add(group);
        }

        // Step 2: Shuffle the groups
        for (int k = 0; k < groups.Count; k++)
        {
            var temp = groups[k];
            var randomIndex = Random.Range(0, groups.Count);
            groups[k] = groups[randomIndex];
            groups[randomIndex] = temp;
        }

        // Step 3: Recombine the shuffled groups into a single list
        List<string> shuffledFragments = new List<string>();
        foreach (var group in groups)
        {
            shuffledFragments.AddRange(group);
        }

        return shuffledFragments;
    }

}
