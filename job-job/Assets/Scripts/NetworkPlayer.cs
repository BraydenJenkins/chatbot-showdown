using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    // references

    [SerializeField] private Avatar[] avatars;


    // network vars

    public NetworkVariable<string> playerName = new NetworkVariable<string>();
    public NetworkVariable<int> avatarIndex = new NetworkVariable<int>();

    // local vars

    // index of chosen avatar, set from UI on the client
    private int chosenAvatarIndex = 0;


    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // avatar index is set from UI
            avatarIndex.Value = chosenAvatarIndex;
        }

        // Set avatar based on avatarIndex.Value
        SetAvatar(avatarIndex.Value);
    }


    public void SetAvatar(int index)
    {
        // Set the avatar based on the index
        avatars[index].gameObject.SetActive(true);
    }
}
