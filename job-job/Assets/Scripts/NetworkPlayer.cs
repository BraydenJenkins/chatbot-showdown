using System.Collections;
using System.Collections.Generic;
using ConversationAPI;
using Mono.CSharp;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class NetworkPlayer : NetworkBehaviour
{
    // references
    [SerializeField] private AvatarDatabase avatarDatabase;
    private ARAI.Avatar[] avatarInstances;
    private NavMeshAgent navMeshAgent;
    private Coroutine randomWalkCoroutine;
    [SerializeField] private TMP_Text playerNameText;
    private PlayerManager playerManager;

    // network vars

    public NetworkVariable<FixedString64Bytes> playerName = new NetworkVariable<FixedString64Bytes>("Name", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> avatarIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Vector3> targetPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<int> lobbyState = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // job job
    public NetworkVariable<FixedString512Bytes> question = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString512Bytes> answer = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<FixedString512Bytes> fragments = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // roles
    public NetworkVariable<FixedString512Bytes> roleQuestion = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString512Bytes> roleAnswer = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<FixedString512Bytes> adjectiveQuestion = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString512Bytes> adjectiveAnswer = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<FixedString512Bytes> freeQuestion = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString512Bytes> freeAnswer = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<FixedString512Bytes> roleOptions = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString512Bytes> adjectiveOptions = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString512Bytes> freeOptions = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString512Bytes> bot = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<int> activityIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> myTurn = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // we used to use activityIndex to start the activity, but now we just use it to set the activity
    // so maybe we use something like turn order to start the activity instead
    private bool activityStarted = false;

    public NetworkVariable<FixedString4096Bytes> currentConversation = new NetworkVariable<FixedString4096Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString512Bytes> currentBotPrompt = new NetworkVariable<FixedString512Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> currentAvatarIndex = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> currentConversationIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<ulong> votedPlayer = new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<int> votes = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private RolesManager rolesManager;


    private void Awake()
    {
        avatarInstances = new ARAI.Avatar[avatarDatabase.avatars.Length];
        for (int i = 0; i < avatarDatabase.avatars.Length; i++)
        {
            avatarInstances[i] = Instantiate(avatarDatabase.avatars[i], transform);
            avatarInstances[i].gameObject.SetActive(false);
            // disable all renderers so it doesn't show up in scene until we need it
            avatarInstances[i].GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
        }

        SetNameActive(false);

    }


    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();

        // do some setup if this is the local player

        Debug.Log("NetworkPlayer Start");
        Debug.Log("IsLocalPlayer: " + IsLocalPlayer);

        if (!IsLocalPlayer)
            return;

        // link to the player manager
        playerManager = FindObjectOfType<PlayerManager>();
        playerManager.LinkNetworkPlayer(this);

        // testing nav mesh
        // randomWalkCoroutine = StartCoroutine(RandomWalkCoroutine());
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("OnNetworkSpawn: " + playerName.Value);

        // Set avatar based on avatarIndex.Value
        SetAvatar(avatarIndex.Value);

        avatarIndex.OnValueChanged += SetAvatar;

        SetNameTMP(playerName.Value);
        playerName.OnValueChanged += SetNameTMP;

        // set the lobby state
        // 1 is joined, 2 is ready, 3 is playing
        if (IsOwner)
            lobbyState.Value = 1;

        targetPosition.OnValueChanged += (previous, current) =>
        {
            navMeshAgent.SetDestination(current);
        };

        // job job
        question.OnValueChanged += OnQuestionChanged;
        answer.OnValueChanged += OnAnswerChanged;
        fragments.OnValueChanged += OnFragmentsChanged;

        // roles
        roleQuestion.OnValueChanged += OnFirstQuestionChanged;
        adjectiveQuestion.OnValueChanged += OnRoleQuestionChanged;
        freeQuestion.OnValueChanged += OnRoleQuestionChanged;

        roleOptions.OnValueChanged += OnRoleOptionsChanged;
        // adjectiveOptions.OnValueChanged += OnAdjectiveOptionsChanged;
        // freeOptions.OnValueChanged += OnAdjectiveOptionsChanged;

        activityIndex.OnValueChanged += OnActivityChanged;
        myTurn.OnValueChanged += OnMyTurnChanged;

        currentConversation.OnValueChanged += OnCurrentConversationChanged;
        currentBotPrompt.OnValueChanged += OnCurrentBotPromptChanged;
        currentAvatarIndex.OnValueChanged += SetConversationAvatar;

        currentConversationIndex.OnValueChanged += OnConversationIndexChanged;

        // try to find the roles manager 
        // in a better set up, the roles manager wouldn't be spawned until that game is selected
        // but for now, we'll just keep it in the scene so it is always available
        rolesManager = FindObjectOfType<RolesManager>();
        if (rolesManager != null)
        {
            rolesManager.timerStart.OnValueChanged += OnTimerChanged;

            rolesManager.playerTurnOrder.OnValueChanged += OnTurnOrderChanged;

            rolesManager.votingState.OnValueChanged += OnVotingChanged;
        }
        else
        {
            Debug.LogWarning("RolesManager not found, timer will not be updated");
        }

    }

    private void SetConversationAvatar(int previousValue, int newValue)
    {
        Debug.Log("Setting conversation avatar to " + newValue);
        if (IsLocalPlayer)
        {
            playerManager.Roles_SetConversationAvatar(newValue);
        }
    }

    private void OnCurrentBotPromptChanged(FixedString512Bytes previousValue, FixedString512Bytes newValue)
    {
        Debug.Log("Current bot prompt changed from " + previousValue + " to " + newValue);
        if (IsLocalPlayer)
        {
            playerManager.Roles_SetBotPrompt(newValue.ToString());
        }
    }

    #region Job Job

    private void OnQuestionChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        Debug.Log("Question changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            playerManager.JJ_SetQuestion(current.ToString());
        }
    }

    private void OnAnswerChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        Debug.Log("Answer changed from " + previous + " to " + current);
    }

    private void OnFragmentsChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        Debug.Log("Fragments changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            playerManager.JJ_SetFragments(current.ToString());
        }
    }


    #endregion


    #region Roles

    private void OnTimerChanged(long previous, long current)
    {
        Debug.Log("Timer changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            int timerDuration = rolesManager.GetTimerDuration();
            playerManager.Roles_SetTimer(current, timerDuration);
        }
    }

    private void OnFirstQuestionChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        Debug.Log("First question changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            // role question is the first question, so treat this as beginning of a new activity
            playerManager.Roles_StartNewRoundAndReset();
            activityStarted = false;
            playerManager.Roles_SetQuestion(current.ToString());
        }
    }

    private void OnRoleQuestionChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        Debug.Log("Role question changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            playerManager.Roles_SetQuestion(current.ToString());
        }
    }

    private void OnRoleOptionsChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        Debug.Log("Role options changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            playerManager.Roles_SetOptions(current.ToString());
        }
    }
    private void OnAdjectiveOptionsChanged(FixedString512Bytes previous, FixedString512Bytes current)
    {
        Debug.Log("Adjective options changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            playerManager.Roles_SetOptions(current.ToString());
        }
    }

    private void OnActivityChanged(int previous, int current)
    {
        Debug.Log("Activity changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            // playerManager.Roles_StartActivity(current);
            playerManager.Roles_SetActivity(current);
        }
    }

    private void OnTurnOrderChanged(FixedString512Bytes prev, FixedString512Bytes current)
    {
        Debug.Log("Turn order changed: " + current.ToString());

        string turnOrder = current.ToString();

        if (string.IsNullOrEmpty(turnOrder) || turnOrder.Length == 0)
        {
            Debug.Log("Turn order is empty");
            return;
        }

        if (IsLocalPlayer)
        {
            playerManager.Roles_SetTurnOrder(current.ToString());
        }

        // turn order is changed when the activity starts
        // TODO: we could either alter turn order each turn so index 0 is always current player
        // or we could just keep track of the current player index so we don't have to network the whole list each time
        // probably the latter
        // huh, but what if a player leaves? we'd have to update the whole list anyway...
        // actually, we could just skip that player's turn using the index...

        // screw it, let's just do the whole list.
        // then we can't say turn order changes exclusively when the activity starts
        // so we will start the activity when turn order changes if the activity is not started
        if (IsLocalPlayer && !activityStarted)
        {
            playerManager.Roles_StartActivity();
            activityStarted = true;
        }
    }

    private void OnMyTurnChanged(bool previous, bool current)
    {
        Debug.Log("My turn changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            playerManager.Roles_SetMyTurn(current);
        }
    }

    private void OnCurrentConversationChanged(FixedString4096Bytes previous, FixedString4096Bytes current)
    {
        Debug.Log("Current conversation changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            // get conversation from json
            Conversation conversation = JsonUtility.FromJson<Conversation>(current.ToString());
            playerManager.Roles_SetConversation(conversation);
        }
    }

    private void OnConversationIndexChanged(int previous, int current)
    {
        Debug.Log("Conversation index changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {

            playerManager.Roles_SetConversationIndex(current);
        }
    }

    public void AdvanceConversationOnServer()
    {
        if (rolesManager == null)
            rolesManager = FindObjectOfType<RolesManager>();

        rolesManager.AdvanceConversationRpc(OwnerClientId);
    }

    public void EndGameOnServer()
    {
        if (rolesManager == null)
            rolesManager = FindObjectOfType<RolesManager>();

        rolesManager.EndGameRpc();
    }


    private void OnVotingChanged(int previous, int current)
    {
        Debug.Log("Voting state changed from " + previous + " to " + current);
        if (IsLocalPlayer)
        {
            playerManager.Roles_SetVotingState(current);
        }
    }

    #endregion

    #region Avatars and Customization
    public void SetAvatar(int index)
    {
        Debug.Log("Setting avatar for " + playerName.Value + " to " + index);

        // for some reason, stickman is not being set to inactive (index 0)

        // lets set it to inactive here to see if that fixes it
        avatarInstances[0].gameObject.SetActive(false);
        // that fixed it. just gonna leave it like this for now

        // Set the avatar based on the index
        avatarInstances[index].gameObject.SetActive(true);
    }

    public void ShowAvatar()
    {
        avatarInstances[avatarIndex.Value].GetComponentInChildren<SkinnedMeshRenderer>().enabled = true;
    }

    public void SetAvatar(int prev, int current)
    {
        // Set the previous avatar to inactive
        avatarInstances[prev].gameObject.SetActive(false);

        SetAvatar(current);
    }

    public void SetNameActive(bool active)
    {
        playerNameText.gameObject.SetActive(active);
    }


    public void SetNameTMP(FixedString64Bytes name)
    {
        playerNameText.text = name.ToString();
    }
    public void SetNameTMP(FixedString64Bytes prev, FixedString64Bytes current)
    {
        SetNameTMP(current);
    }

    #endregion

    private IEnumerator RandomWalkCoroutine()
    {
        while (true)
        {
            targetPosition.Value = transform.position + new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5));

            yield return new WaitForSeconds(5);
        }
    }

    [Rpc(SendTo.Owner)]
    public void SetTargetPositionRpc(Vector3 position)
    {
        if (randomWalkCoroutine != null)
            StopCoroutine(randomWalkCoroutine);

        targetPosition.Value = position;
    }

    [Rpc(SendTo.Owner)]
    public void ResetVoteRpc()
    {
        votedPlayer.Value = ulong.MaxValue;
    }

}
