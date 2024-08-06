using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : NetworkBehaviour
{
    public List<PlayerSlot> playerSlots = new List<PlayerSlot>();

    private List<ulong> connectedPlayers = new List<ulong>();
    public NetworkVariable<FixedString512Bytes> connectedPlayerIds = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private TMP_Text waitingForHostText;

    void Start()
    {
        startGameButton.gameObject.SetActive(false);
        waitingForHostText.gameObject.SetActive(false);

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

    }



    public override void OnDestroy()
    {
        base.OnDestroy();

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

    }


    private void OnClientConnected(ulong obj)
    {
        if (!IsServer)
            return;

        Debug.Log("[LobbyManager] OnClientConnected: " + obj);
        if (!connectedPlayers.Contains(obj))
            connectedPlayers.Add(obj);

        connectedPlayerIds.Value = string.Join(",", connectedPlayers);
    }

    private void OnClientDisconnect(ulong obj)
    {
        if (!IsServer)
            return;

        connectedPlayers.Remove(obj);
        connectedPlayerIds.Value = string.Join(",", connectedPlayers);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            startGameButton.gameObject.SetActive(true);
        }
        else
        {
            waitingForHostText.gameObject.SetActive(true);
        }

        Debug.Log("[LobbyManager] OnNetworkSpawn");
        connectedPlayerIds.OnValueChanged += UpdatePlayerSlots;
        WritePlayerIds();


    }

    private void WritePlayerIds()
    {
        if (IsServer)
        {
            var clients = NetworkManager.Singleton.ConnectedClientsList;
            connectedPlayers.Clear();
            foreach (var client in clients)
            {
                connectedPlayers.Add(client.ClientId);
            }
            connectedPlayerIds.Value = string.Join(",", connectedPlayers);
        }
    }

    private void UpdatePlayerSlots(FixedString512Bytes previousValue, FixedString512Bytes newValue)
    {
        UpdatePlayerSlots();
    }

    private void UpdatePlayerSlots()
    {
        Debug.Log("[LobbyManager] UpdatePlayerSlots");

        // find all network players, assign them to the slots
        var networkPlayers = FindObjectsOfType<NetworkPlayer>();
        // sort networkPlayers to match the order of connectedPlayers
        var sortedNetworkPlayers = new List<NetworkPlayer>();

        // get connectedPlayers from connectedPlayerIds if we are not the server
        if (!IsServer)
        {
            connectedPlayers.Clear();
            var connectedPlayerIdsArray = connectedPlayerIds.Value.ToString().Split(',');
            foreach (var connectedPlayerId in connectedPlayerIdsArray)
            {
                if (!string.IsNullOrEmpty(connectedPlayerId))
                {
                    connectedPlayers.Add(Convert.ToUInt64(connectedPlayerId));
                }
            }
            Debug.Log("connectedPlayers: " + connectedPlayers.Count);
        }

        foreach (var connectedPlayer in connectedPlayers)
        {
            foreach (var networkPlayer in networkPlayers)
            {
                if (networkPlayer.OwnerClientId == connectedPlayer)
                {
                    sortedNetworkPlayers.Add(networkPlayer);
                    break;
                }
            }
        }

        Debug.Log("sortedNetworkPlayers: " + sortedNetworkPlayers.Count);

        playerCountText.text = "(" + sortedNetworkPlayers.Count + "/8)";

        startGameButton.interactable = sortedNetworkPlayers.Count >= 2;

        for (int i = 0; i < playerSlots.Count; i++)
        {
            playerSlots[i].gameObject.SetActive(true);
            if (i < sortedNetworkPlayers.Count)
            {
                playerSlots[i].SetNetworkPlayer(sortedNetworkPlayers[i]);
            }
            else
            {
                playerSlots[i].RemoveNetworkPlayer();
            }
        }
    }
}
