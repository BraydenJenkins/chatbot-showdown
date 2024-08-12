using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using DG.Tweening;
using TMPro;
using Netcode.Transports.PhotonRealtime;
using UnityEngine.UI;

public class NetworkHelper : MonoBehaviour
{
    [SerializeField] private CanvasGroup mainCanvas, joinCanvas, hostCanvas, avatarCanvas, loadingCanvas;
    [SerializeField] private float transitionDuration = 0.5f;

    [SerializeField] private GameObject gameRoot;

    [SerializeField] private TMP_InputField roomCodeInput;
    public int roomCodeLength = 4;
    [SerializeField] private TMP_Text lobbyRoomCodeText;

    private PhotonRealtimeTransport photonTransport;


    private enum ConnectionStatus
    {
        NotConnected,
        AttemptingRoomJoin,
        AttemptingRoomCreation,
        AttemptingHostWithUniqueRoomCode,
        Connected
    }
    private ConnectionStatus connectionStatus;


    private void Awake()
    {
        // hide game root since we are not connected yet

        gameRoot.SetActive(false);

        SetCanvasGroup(mainCanvas, true, 0);
        SetCanvasGroup(joinCanvas, false, 0);
        // SetCanvasGroup(hostCanvas, false, 0);
        SetCanvasGroup(avatarCanvas, false, 0);
        SetCanvasGroup(loadingCanvas, false, 0);


        roomCodeInput.characterLimit = roomCodeLength;

        connectionStatus = ConnectionStatus.NotConnected;
        photonTransport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as PhotonRealtimeTransport;
    }


    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;

        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
    }

    private void OnDestroy()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;

        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
    }

    public void OnClientConnectedCallback(ulong clientId)
    {
        if (NetworkManager.Singleton.IsClient && NetworkManager.Singleton.LocalClientId == clientId)
        {
            lobbyRoomCodeText.text = photonTransport.RoomName;

            string connectMessage = "Client connected";
            switch (connectionStatus)
            {
                case ConnectionStatus.NotConnected:
                    connectMessage += " but they weren't trying to connect? Interesting!";
                    Debug.LogError("Unexpected behavior during client connection.");
                    break;
                case ConnectionStatus.AttemptingRoomJoin:
                    connectMessage += " to room " + photonTransport.RoomName;
                    break;
                case ConnectionStatus.AttemptingRoomCreation:
                    connectMessage += " as host to room " + photonTransport.RoomName;
                    break;
                case ConnectionStatus.AttemptingHostWithUniqueRoomCode:
                    connectMessage += " as host with unique room code";
                    break;
                case ConnectionStatus.Connected:
                    connectMessage += " while already connected to " + photonTransport.RoomName;
                    Debug.LogError("Unexpected behavior during client connection.");
                    break;
            }
            connectMessage += ".";
            Debug.Log(connectMessage);

            if (connectionStatus == ConnectionStatus.AttemptingRoomCreation)
            {
                // we were trying to create a room, but we connected to a room that already exists
                // we should quit this room and try again
                NetworkManager.Singleton.Shutdown();
                StartRoom();
                return;
            }

            connectionStatus = ConnectionStatus.Connected;


            Debug.Log("We have connected");

            SetCanvasGroup(loadingCanvas, false, transitionDuration);
            SetCanvasGroup(avatarCanvas, true, transitionDuration);

            // gameRoot.SetActive(true);
        }
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log("We have disconnected");

            string disconnectMessage = "Client disconnected";

            switch (connectionStatus)
            {
                case ConnectionStatus.NotConnected:
                    disconnectMessage += " but they weren't connected? Interesting!";
                    Debug.LogError("Unexpected behavior during client connection.");
                    break;
                case ConnectionStatus.AttemptingRoomJoin:
                    disconnectMessage += " while attempting to join room " + photonTransport.RoomName;
                    break;
                case ConnectionStatus.AttemptingRoomCreation:
                    disconnectMessage += " while attempting to create room " + photonTransport.RoomName;
                    break;
                case ConnectionStatus.AttemptingHostWithUniqueRoomCode:
                    disconnectMessage += " while attempting to host with unique room code";
                    Debug.LogError("Unexpected behavior during host attempt.");
                    break;
                case ConnectionStatus.Connected:
                    disconnectMessage += " while connected to room " + photonTransport.RoomName;
                    break;
            }
            disconnectMessage += ".";
            Debug.Log(disconnectMessage);

            if (connectionStatus == ConnectionStatus.AttemptingRoomJoin)
            {
                // couldn't connect while trying to join room, it must not exist
                // go back to join screen
                SetCanvasGroup(loadingCanvas, false, transitionDuration);
                SetCanvasGroup(joinCanvas, true, transitionDuration);
            }
            else if (connectionStatus == ConnectionStatus.Connected)
            {
                // we were connected, but now we're not
                // restart the scene just in case
                // reload scene
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            else if (connectionStatus == ConnectionStatus.AttemptingRoomCreation)
            {
                // couldn't connect while trying to create a room, this means the room doesn't exist yet, so we can host.
                connectionStatus = ConnectionStatus.AttemptingHostWithUniqueRoomCode;
                Debug.Log("Attempting to host with unique room code");
                // dang, shutting down destroys all scene-placed network objects, so we can't.
                NetworkManager.Singleton.Shutdown(true);
                // StartHost();
                StartCoroutine(StartHostAfterShutdown());
            }

        }
    }

    public void QuitRoom()
    {
        NetworkManager.Singleton.Shutdown(true);
    }

    private IEnumerator StartHostAfterShutdown()
    {
        while (NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }
        StartHost();
    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }

    public void JoinRoom()
    {
        string roomCode = roomCodeInput.text.ToUpper();
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as PhotonRealtimeTransport;
        transport.RoomName = roomCode;

        Debug.Log("Joining room: " + roomCode);

        SetCanvasGroup(joinCanvas, false, transitionDuration);
        SetCanvasGroup(loadingCanvas, true, transitionDuration);

        connectionStatus = ConnectionStatus.AttemptingRoomJoin;

        NetworkManager.Singleton.StartClient();
    }

    public void StartRoom()
    {
        string roomCode = GenerateRandomRoomCode();

        // we're going to try joining the room as a client, and if we fail, we'll start a new room
        // if we are successful, we'll quit the room and start a new room with a new room code
        // this ensures that we don't have to worry about room code collisions
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as PhotonRealtimeTransport;
        transport.RoomName = roomCode;

        Debug.Log("Starting room: " + roomCode);

        // SetCanvasGroup(hostCanvas, false, transitionDuration);
        SetCanvasGroup(mainCanvas, false, transitionDuration);
        SetCanvasGroup(loadingCanvas, true, transitionDuration);

        connectionStatus = ConnectionStatus.AttemptingRoomCreation;
        // dang, shutting down destroys all scene-placed network objects, so we can't do this start client trick.
        // NetworkManager.Singleton.StartClient();

        // so we will just straight up start the host and hope for a unique room code :/
        connectionStatus = ConnectionStatus.AttemptingHostWithUniqueRoomCode;
        NetworkManager.Singleton.StartHost();
    }


    private string GenerateRandomRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
        var random = new System.Random();
        var roomCode = new char[roomCodeLength];
        for (int i = 0; i < roomCodeLength; i++)
        {
            roomCode[i] = chars[random.Next(chars.Length)];
        }
        // we not sanitizing the room code, yolo
        // TODO: probably should sanitize the room code
        return new string(roomCode);
    }

    private void SetCanvasGroup(CanvasGroup canvasGroup, bool active, float fadeDuration = 0)
    {
        // Debug.Log("Setting canvas group: " + canvasGroup.name + " to " + active);
        canvasGroup.DOFade(active ? 1 : 0, fadeDuration);
        canvasGroup.interactable = active;
        canvasGroup.blocksRaycasts = active;

        if (active)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasGroup.GetComponent<RectTransform>());
            // get all content size fitters and rebuild them
            // idgaf if this is inefficient, I'm tired of content size fitters not updating
            var contentSizeFitters = canvasGroup.GetComponentsInChildren<ContentSizeFitter>();
            foreach (var contentSizeFitter in contentSizeFitters)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentSizeFitter.GetComponent<RectTransform>());
            }
        }
    }
}
