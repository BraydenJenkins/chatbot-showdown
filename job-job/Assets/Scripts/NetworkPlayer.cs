using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class NetworkPlayer : NetworkBehaviour
{
    // references
    [SerializeField] private Avatar[] avatars;
    private NavMeshAgent navMeshAgent;
    [SerializeField] private TMP_Text playerNameText;

    // network vars

    public NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>("Name", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> avatarIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Vector3> targetPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);




    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();

        // do some setup if this is the local player

        Debug.Log("NetworkPlayer Start");
        Debug.Log("IsLocalPlayer: " + IsLocalPlayer);

        if (!IsLocalPlayer)
            return;

        // link to the player manager
        var playerManager = FindObjectOfType<PlayerManager>();
        playerManager.LinkNetworkPlayer(this);

        // testing nav mesh
        StartCoroutine(RandomWalkCoroutine());
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("OnNetworkSpawn: " + playerName.Value);

        // Set avatar based on avatarIndex.Value
        SetAvatar(avatarIndex.Value);

        avatarIndex.OnValueChanged += SetAvatar;

        SetNameTMP(playerName.Value);
        playerName.OnValueChanged += SetNameTMP;

        targetPosition.OnValueChanged += (previous, current) =>
        {
            navMeshAgent.SetDestination(current);
        };
    }


    public void SetAvatar(int index)
    {
        Debug.Log("Setting avatar for " + playerName.Value + " to " + index);
        // Set the avatar based on the index
        avatars[index].gameObject.SetActive(true);
    }

    public void SetAvatar(int prev, int current)
    {
        // Set the previous avatar to inactive
        avatars[prev].gameObject.SetActive(false);

        SetAvatar(current);
    }

    public void SetNameTMP(FixedString64Bytes name)
    {
        playerNameText.text = name.ToString();
    }
    public void SetNameTMP(FixedString64Bytes prev, FixedString64Bytes current)
    {
        SetNameTMP(current);
    }

    private IEnumerator RandomWalkCoroutine()
    {
        while (true)
        {
            targetPosition.Value = transform.position + new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5));

            yield return new WaitForSeconds(5);
        }
    }
}
