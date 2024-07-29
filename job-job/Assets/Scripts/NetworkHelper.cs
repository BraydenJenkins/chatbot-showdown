using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class NetworkHelper : MonoBehaviour
{
    [SerializeField] private CanvasGroup mainCanvas;

    [SerializeField] private GameObject gameRoot;

    private void Awake()
    {
        // hide game root since we are not connected yet

        gameRoot.SetActive(false);
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
            Debug.Log("We have connected");
            // hide main canvas and open up to main scene
            mainCanvas.interactable = false;
            mainCanvas.blocksRaycasts = false;
            mainCanvas.DOFade(0, 1f).OnComplete(() =>
            {
                mainCanvas.gameObject.SetActive(false);
            });

            gameRoot.SetActive(true);
        }
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log("We have disconnected");
            // reload scene
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }
}
