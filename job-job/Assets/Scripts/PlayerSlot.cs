using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlot : MonoBehaviour
{
    private NetworkPlayer networkPlayer;

    [SerializeField] private AvatarDatabase avatarDatabase;

    [Header("UI References")]
    public Image avatarImage;
    public Image avatarImageCover;
    public TMP_Text playerNameTMP;

    public GameObject slotReady, slotJoined, slotOpen;

    public void SetNetworkPlayer(NetworkPlayer player)
    {
        networkPlayer = player;
        player.playerName.OnValueChanged += UpdatePlayerName;
        UpdatePlayerName("", player.playerName.Value);

        player.avatarIndex.OnValueChanged += UpdateAvatar;
        UpdateAvatar(-1, player.avatarIndex.Value);

        player.lobbyState.OnValueChanged += UpdateLobbyState;
        UpdateLobbyState(0, player.lobbyState.Value);
    }



    public void RemoveNetworkPlayer()
    {
        if (networkPlayer != null)
        {
            networkPlayer.playerName.OnValueChanged -= UpdatePlayerName;
            networkPlayer.avatarIndex.OnValueChanged -= UpdateAvatar;
            networkPlayer.lobbyState.OnValueChanged -= UpdateLobbyState;
        }

        networkPlayer = null;

        UpdatePlayerName("", "[Empty Slot]");
        UpdateAvatar(0, -1);

        slotReady.SetActive(false);
        slotJoined.SetActive(false);
        slotOpen.SetActive(true);
    }

    private void UpdateLobbyState(int previousValue, int newValue)
    {
        slotReady.SetActive(newValue == 2);
        slotJoined.SetActive(newValue == 1);
        slotOpen.SetActive(newValue == 0);
    }

    private void UpdateAvatar(int previousValue, int newValue)
    {
        if (newValue < -1 || newValue >= avatarDatabase.avatars.Length)
        {
            Debug.LogError("Invalid avatar index: " + newValue);
            return;
        }

        if (newValue == -1)
        {
            avatarImageCover.gameObject.SetActive(true);
            avatarImage.sprite = null;
            return;
        }

        avatarImageCover.gameObject.SetActive(false);
        avatarImage.sprite = avatarDatabase.avatars[newValue].avatarImage;
    }

    private void UpdatePlayerName(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        string name = newValue.ToString();
        Debug.Log("PlayerSlot: UpdatePlayerName: " + name);
        if (string.IsNullOrEmpty(name) || name == "" || name.Trim().Length == 0 || name.Trim().Length == 1)
        {
            name = "Player";
        }
        playerNameTMP.text = name;
    }




}
