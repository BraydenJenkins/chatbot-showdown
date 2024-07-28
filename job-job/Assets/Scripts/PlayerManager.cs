using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private NetworkPlayer networkPlayer;

    //  stores references to avatar ui
    [SerializeField] private PagingScrollRect avatarSelectionScrollRect;
    [SerializeField] private TMP_Text playerNameText;

    public void LinkNetworkPlayer(NetworkPlayer player)
    {
        networkPlayer = player;

        networkPlayer.avatarIndex.Value = avatarSelectionScrollRect.CurrentPageIndex;
        networkPlayer.playerName.Value = playerNameText.text;
    }
}
