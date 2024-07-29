using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerControls : NetworkBehaviour
{
    [SerializeField] private Canvas serverCanvas;

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
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var client in clients)
        {
            var player = client.PlayerObject.GetComponent<NetworkPlayer>();
            players.Add(player);
        }

        var randomQuestion = "What is the capital of France?";
        foreach (var player in players)
        {
            player.question.Value = randomQuestion;
        }
    }

}
