using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using ConversationAPI;
using System;

public class PlayerManager : MonoBehaviour
{
    private NetworkPlayer networkPlayer;

    //  stores references to avatar ui
    // [SerializeField] private PagingScrollRect avatarSelectionScrollRect;
    [SerializeField] private int avatarIndex;
    [SerializeField] private TMP_Text playerNameText;

    [SerializeField] private CanvasGroup mainCanvas;

    [SerializeField] private Button avatarButtonPrefab;
    [SerializeField] private RectTransform avatarButtonArea;
    // TODO: swap to avatar database
    [SerializeField] private AvatarDatabase avatarDatabase;
    [SerializeField] private RectTransform avatarsArea;
    [SerializeField] private GameObject chooseCharacterText;
    private ARAI.Avatar[] avatarInstances;
    [SerializeField] private float avatarScale = 1f;
    [SerializeField] private Vector3 avatarOffset = Vector3.zero;
    [SerializeField] private Vector3 avatarRotationOffset = Vector3.zero;
    [SerializeField] private RuntimeAnimatorController avatarSelectionController;
    private AvatarButton[] avatarButtons;

    [SerializeField] private Button joinGameButton;
    private bool avatarSelected = false;
    private bool gameJoined = false;

    [SerializeField] private CanvasGroup lobbyCanvas;
    [SerializeField] private Image backgroundColorImage;
    [SerializeField] private Color lobbyBackgroundColor;


    [SerializeField] private float transitionDuration = 0.5f;

    public void LinkNetworkPlayer(NetworkPlayer player)
    {
        networkPlayer = player;

        networkPlayer.avatarIndex.Value = avatarIndex;
        networkPlayer.playerName.Value = playerNameText.text;
    }

    public void SetAvatarIndex(int index)
    {
        avatarIndex = index;

        chooseCharacterText.SetActive(false);

        // set all avatars to inactive
        for (int i = 0; i < avatarInstances.Length; i++)
        {
            avatarInstances[i].GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
        }
        // set selected avatar to active
        avatarInstances[index].GetComponentInChildren<SkinnedMeshRenderer>().enabled = true;

        // set all buttons to deselected
        for (int i = 0; i < avatarButtons.Length; i++)
        {
            avatarButtons[i].SetDeselected();
        }
        // set selected button to selected
        avatarButtons[index].SetSelected();

        networkPlayer.avatarIndex.Value = avatarIndex;

        avatarSelected = true;
    }

    public void JoinGame()
    {
        gameJoined = true;
        networkPlayer.playerName.Value = playerNameText.text;
        networkPlayer.lobbyState.Value = 2;

        SetCanvasGroup(lobbyCanvas, true, transitionDuration);
        backgroundColorImage.DOColor(lobbyBackgroundColor, transitionDuration);
    }

    private void Avatars_Awake()
    {
        avatarInstances = new ARAI.Avatar[avatarDatabase.avatars.Length];

        avatarButtons = new AvatarButton[avatarDatabase.avatars.Length];

        // populate avatars
        for (int i = 0; i < avatarDatabase.avatars.Length; i++)
        {


            // insantiate avatar in avatars area
            var avatar = Instantiate(avatarDatabase.avatars[i], avatarsArea);
            avatar.transform.localScale = Vector3.one * avatarScale;
            avatar.GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
            avatar.transform.localPosition = avatarOffset;
            avatar.transform.localEulerAngles = avatarRotationOffset;
            avatar.GetComponent<Animator>().runtimeAnimatorController = avatarSelectionController;
            avatarInstances[i] = avatar;

            var button = Instantiate(avatarButtonPrefab, avatarButtonArea);
            // button.GetComponentInChildren<Image>().sprite = avatarDatabase.avatars[i].avatarImage;
            button.GetComponent<AvatarButton>().avatarImage.sprite = avatarDatabase.avatars[i].avatarImage;
            int index = i;
            button.onClick.AddListener(() =>
            {
                SetAvatarIndex(index);
            });

            avatarButtons[i] = button.GetComponent<AvatarButton>();

        }

        joinGameButton.interactable = false;
        joinGameButton.onClick.AddListener(JoinGame);
    }

    private void Avatars_Update()
    {
        if (gameJoined)
            return;

        if (avatarSelected)
        {
            // Debug.Log(playerNameText.text.Length);
            joinGameButton.interactable = playerNameText.text.Length > 2;
        }
    }

    private void OnDestroy()
    {
        joinGameButton.onClick.RemoveListener(JoinGame);
    }

    private void Awake()
    {
        Avatars_Awake();
        JJ_Awake();
        Roles_Awake();
    }

    private void Update()
    {
        Roles_Update();
        Avatars_Update();
    }

    #region Job Job

    // job job
    [Header("Job Job")]
    [SerializeField] private CanvasGroup jobjobCanvas;
    [SerializeField] private GameObject questionPanel, waitingPanel, fragmentsPanel;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_InputField answerInputField;

    [SerializeField] private RectTransform wordArea;
    [SerializeField] private Button wordButtonPrefab;
    [SerializeField] private TMP_Text fragmentsTMP;

    private void JJ_Awake()
    {
        jobjobCanvas.alpha = 0;
        jobjobCanvas.interactable = false;
        jobjobCanvas.blocksRaycasts = false;
        questionPanel.SetActive(false);
        waitingPanel.SetActive(false);
        fragmentsPanel.SetActive(false);
    }

    public void JJ_SetQuestion(string question)
    {
        jobjobCanvas.DOFade(1, 1f);
        jobjobCanvas.interactable = true;
        jobjobCanvas.blocksRaycasts = true;
        questionPanel.SetActive(true);

        questionText.text = question;

    }

    public void JJ_SubmitAnswer()
    {
        networkPlayer.answer.Value = answerInputField.text;
        questionPanel.SetActive(false);
        waitingPanel.SetActive(true);
    }

    public void JJ_SetFragments(string fragments)
    {
        // remove non-alphanumeric characters
        // fragments = System.Text.RegularExpressions.Regex.Replace(fragments, @"[^0-9a-zA-Z]+", " ");
        // split fragments by space, create buttons for each fragment
        var fragmentArray = fragments.Split(' ');
        for (int i = 0; i < fragmentArray.Length; i++)
        {
            if (string.IsNullOrEmpty(fragmentArray[i]))
            {
                continue;
            }
            var button = Instantiate(wordButtonPrefab, wordArea);
            button.GetComponentInChildren<TMP_Text>().text = fragmentArray[i];
            string fragment = fragmentArray[i];
            button.onClick.AddListener(() =>
            {
                fragmentsTMP.text += fragment + " ";
            });
        }

        waitingPanel.SetActive(false);
        fragmentsPanel.SetActive(true);
    }

    public void JJ_DeleteFragment()
    {
        // remove last word from fragmentsTMP
        var fragments = fragmentsTMP.text.TrimEnd().Split(' ');
        if (fragments.Length > 0)
        {
            fragmentsTMP.text = string.Join(" ", fragments, 0, fragments.Length - 1) + " ";
        }
    }

    public void JJ_SubmitFragments()
    {

    }

    #endregion

    #region ROLES

    // roles
    [Header("Roles")]
    [SerializeField] private CanvasGroup rolesCanvas;

    [SerializeField] private CanvasGroup rolesBackground;
    [SerializeField] private CanvasGroup rolesQuestionPanel, roleOptionsPanel, rolesWaitingPanel, conversationPanel, questionIntroPanel, promptIntroPanel;

    // questions
    [SerializeField] private float questionIntroDelay = 2f;
    [SerializeField] private TMP_Text rolesQuestionText;
    [SerializeField] private TMP_InputField roleAnswerInputField;
    int questionsAnswered = 0;
    [SerializeField] private TMP_Text questionIntroText;
    [SerializeField] private Image questionTimer;
    private bool timerRunning = false;
    private long timerStart;
    private long timerEnd;
    private long currentTimerTime;

    // bot creation
    [SerializeField] private RectTransform roleOptionsArea;
    [SerializeField] private TMP_Text roleOptionsText;
    [SerializeField] private TMP_Text roleOptionsActivityText;

    [SerializeField] private int expectedOptionsReceived = 2;
    private int optionsReceived = 0;
    private bool activityReceived = false;

    // activity
    [SerializeField] private CanvasGroup activityIntroCanvas, activityGoalCanvas;
    [SerializeField] private TMP_Text activityText, activityGoalText, playerTurnText, currentPromptText;
    [SerializeField] private ActivityDatabase activityDatabase;
    [SerializeField] private AvatarDatabase cpuAvatarDatabase;
    [SerializeField] private GameObject activityParent;
    private RolesActivity currentActivity;
    [SerializeField] private ConversationCanvas conversationCanvas;
    [SerializeField] private CanvasGroup conversationCanvasGroup;
    [SerializeField] private Button nextLineButton;
    private bool myTurn = false;

    [SerializeField] private Transform playerAvatarParent;
    private ARAI.Avatar[] conversationAvatarInstances;
    [SerializeField] private Transform cpuAvatarParent;
    private ARAI.Avatar[] cpuAvatarInstances;
    [SerializeField] private float conversationAvatarScale = 1f;


    // voting
    [SerializeField] private CanvasGroup votingCanvas;
    [SerializeField] private VoteButton[] voteButtons;
    [SerializeField] private CanvasGroup votingTitleCanvas, votingScoresCanvas;
    [SerializeField] private Button nextRoundButton;
    [SerializeField] private TMP_Text nextRoundTMP;


    private void Roles_Awake()
    {
        SetCanvasGroup(rolesCanvas, false);
        SetCanvasGroup(rolesBackground, true);
        SetCanvasGroup(rolesQuestionPanel, false);
        SetCanvasGroup(questionIntroPanel, false);
        SetCanvasGroup(promptIntroPanel, false);
        SetCanvasGroup(roleOptionsPanel, false);
        SetCanvasGroup(rolesWaitingPanel, false);
        SetCanvasGroup(conversationPanel, false);
        SetCanvasGroup(activityIntroCanvas, false);
        SetCanvasGroup(activityGoalCanvas, false);
        SetCanvasGroup(conversationCanvasGroup, false);
        SetCanvasGroup(votingCanvas, false);
        SetCanvasGroup(votingTitleCanvas, false);
        SetCanvasGroup(votingScoresCanvas, false);
        playerTurnText.transform.parent.GetComponent<Image>().DOFade(0, 0);

        nextLineButton.gameObject.SetActive(false);

        questionsAnswered = 0;

        PopulatePlayerAvatarSlot();
        PopulateCPUAvatarSlot();


        // ridiculous input field workarounds (jfc unity)
        roleAnswerInputField.onEndEdit.AddListener(Roles_OnEndEdit);
        roleAnswerInputField.onTouchScreenKeyboardStatusChanged.AddListener(Roles_OnTouchScreenKeyboardStatusChanged);
    }

    private void PopulatePlayerAvatarSlot()
    {
        // instantiate one of each avatar in the player avatar slot, and set all to false
        conversationAvatarInstances = new ARAI.Avatar[avatarDatabase.avatars.Length];
        for (int i = 0; i < avatarDatabase.avatars.Length; i++)
        {
            var avatar = Instantiate(avatarDatabase.avatars[i], playerAvatarParent);
            conversationAvatarInstances[i] = avatar;
            avatar.transform.localScale = Vector3.one * conversationAvatarScale;
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localEulerAngles = Vector3.zero;
            avatar.GetComponent<Animator>().runtimeAnimatorController = avatarSelectionController;
            avatar.gameObject.SetActive(false);
        }
    }

    private void PopulateCPUAvatarSlot()
    {
        // instantiate one of each avatar in the player avatar slot, and set all to false
        cpuAvatarInstances = new ARAI.Avatar[cpuAvatarDatabase.avatars.Length];
        for (int i = 0; i < cpuAvatarDatabase.avatars.Length; i++)
        {
            var avatar = Instantiate(cpuAvatarDatabase.avatars[i], cpuAvatarParent);
            cpuAvatarInstances[i] = avatar;
            avatar.transform.localScale = Vector3.one * conversationAvatarScale;
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localEulerAngles = Vector3.zero;
            avatar.GetComponent<Animator>().runtimeAnimatorController = avatarSelectionController;
            avatar.gameObject.SetActive(false);
        }
    }

    private int currentConversationAvatar = -1;

    public void Roles_SetConversationAvatar(int index)
    {
        // // set all avatars to inactive
        // for (int i = 0; i < conversationAvatarInstances.Length; i++)
        // {
        //     conversationAvatarInstances[i].gameObject.SetActive(false);
        // }
        // // set selected avatar to active
        // conversationAvatarInstances[index].gameObject.SetActive(true);

        currentConversationAvatar = index;
    }

    private void SwapConversationAvatar()
    {
        // set all avatars to inactive
        for (int i = 0; i < conversationAvatarInstances.Length; i++)
        {
            conversationAvatarInstances[i].gameObject.SetActive(false);
        }
        // set selected avatar to active
        if (currentConversationAvatar >= 0 && currentConversationAvatar < conversationAvatarInstances.Length)
        {
            conversationAvatarInstances[currentConversationAvatar].gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("Invalid conversation avatar index: " + currentConversationAvatar);
        }
    }
    private void SwapCPUAvatar()
    {
        // set all avatars to inactive
        for (int i = 0; i < cpuAvatarInstances.Length; i++)
        {
            cpuAvatarInstances[i].gameObject.SetActive(false);
        }
        // set selected avatar to active
        cpuAvatarInstances[currentActivity.avatarIndex].gameObject.SetActive(true);
    }

    private void Roles_Update()
    {
        if (timerRunning)
        {
            currentTimerTime += (long)(Time.deltaTime * 1000);
            // Debug.Log("Timer: " + (float)(currentTimerTime - timerStart) + " / " + (float)(timerEnd - timerStart));
            questionTimer.fillAmount = (float)(currentTimerTime - timerStart) / (float)(timerEnd - timerStart);
            if (currentTimerTime >= timerEnd)
            {
                questionTimer.fillAmount = 1;
                timerRunning = false;
            }
        }

        nextLineButton.interactable = nextLineInteractableBase && !conversationCanvas.animatingConversation;
    }

    public void Roles_SetQuestion(string question)
    {
        // going to treat this as beginning of the whole game too (first time it is called, that is)
        SetCanvasGroup(mainCanvas, false, transitionDuration);

        // in case this was called after the first activity, we need to also reset the activity and all other screens
        activityParent.SetActive(false);
        SetCanvasGroup(votingCanvas, false, transitionDuration);
        SetCanvasGroup(votingTitleCanvas, false, transitionDuration);
        SetCanvasGroup(votingScoresCanvas, false, transitionDuration);


        questionsAnswered++;
        string questionIntro = GetOrdinal(questionsAnswered) + " question";
        questionIntro = questionIntro.ToUpper();
        questionIntroText.text = questionIntro;

        SetCanvasGroup(rolesCanvas, true, transitionDuration);

        StartCoroutine(Roles_ShowQuestionAfterDelay(question, questionIntroDelay));
    }

    private string GetOrdinal(int num)
    {
        // wait I am dumb, the mockup just shows "first question", then "next question" for the rest
        // so we don't need to do this ordinal stuff
        // but I'll leave it here just in case we need it later

        if (num == 1) return "first";
        return "next";


        if (num <= 0) return num.ToString();

        // hardcoding the first few (probably enough for our purposes)
        // the rest will just be "th" or whatever (so like 11th, 12th, 13th, 14th, etc.)
        if (num == 1)
        {
            return "first";
        }
        if (num == 2)
        {
            return "second";
        }
        if (num == 3)
        {
            return "third";
        }
        if (num == 4)
        {
            return "fourth";
        }
        if (num == 5)
        {
            return "fifth";
        }


        switch (num % 100)
        {
            case 11:
            case 12:
            case 13:
                return num + "th";
        }

        switch (num % 10)
        {
            case 1:
                return num + "st";
            case 2:
                return num + "nd";
            case 3:
                return num + "rd";
            default:
                return num + "th";
        }
    }

    public void Roles_SetTimer(long timerStart, int duration)
    {
        // timerStart is unix timestamp in milliseconds
        // duration is in seconds
        // so timer should be completely full at timerStart + duration * 1000
        // and empty at timerStart
        this.timerStart = timerStart + (long)(questionIntroDelay * 1000) + (long)(transitionDuration * 1000);
        timerEnd = timerStart + (duration * 1000) - (long)(questionIntroDelay * 1000);
        currentTimerTime = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        // what I am realizing now is, if there is latency, the player's timer visual won't start at the beginning of the bar, which might be confusing and or frustrating
        // so all we really care about it the end time, and we just set the timer start time to the current time
        this.timerStart = currentTimerTime + (long)(questionIntroDelay * 1000) + (long)(transitionDuration * 1000 * 0.5f);
        timerRunning = true;
    }

    private IEnumerator Roles_ShowQuestionAfterDelay(string question, float delay)
    {
        SetCanvasGroup(questionIntroPanel, true, transitionDuration);
        SetCanvasGroup(rolesQuestionPanel, false, transitionDuration);
        yield return new WaitForSeconds(delay);
        SetCanvasGroup(questionIntroPanel, false, transitionDuration);
        SetCanvasGroup(rolesQuestionPanel, true, transitionDuration);

        // set question text
        rolesQuestionText.text = question;

        // clear the input field
        roleAnswerInputField.text = "";
    }

    private void Roles_OnTouchScreenKeyboardStatusChanged(TouchScreenKeyboard.Status status)
    {
        if (status == TouchScreenKeyboard.Status.Done)
        {
            Roles_SubmitAnswer();
        }
    }
    private void Roles_OnEndEdit(string text)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetButtonDown("Submit"))
        {
            Roles_SubmitAnswer();
        }
    }

    public void Roles_SubmitAnswer()
    {
        Debug.Log("[PlayerManager]: Submitting role answer: " + roleAnswerInputField.text);
        networkPlayer.roleAnswer.Value = roleAnswerInputField.text;
        networkPlayer.adjectiveAnswer.Value = roleAnswerInputField.text;

        roleAnswerInputField.text = "";
        roleAnswerInputField.ActivateInputField();
    }

    public void Roles_SetOptions(string options)
    {

        var optionsArray = options.Split(';');
        for (int i = 0; i < optionsArray.Length; i++)
        {
            if (string.IsNullOrEmpty(optionsArray[i]))
            {
                continue;
            }
            // split again on spaces
            var words = optionsArray[i].Split(' ');
            for (int j = 0; j < words.Length; j++)
            {
                if (string.IsNullOrEmpty(words[j]))
                {
                    continue;
                }
                var button = Instantiate(wordButtonPrefab, roleOptionsArea);
                button.GetComponentInChildren<TMP_Text>().text = words[j];
                string option = words[j];
                button.onClick.AddListener(() =>
                {
                    roleOptionsText.text += option + " ";
                });
            }
        }

        // shuffle child index in roleOptionsArea
        for (int i = 0; i < roleOptionsArea.childCount; i++)
        {
            roleOptionsArea.GetChild(i).SetSiblingIndex(UnityEngine.Random.Range(0, roleOptionsArea.childCount));
        }


        // SetCanvasGroup(rolesQuestionPanel, false, transitionDuration);
        // SetCanvasGroup(promptIntroPanel, true, transitionDuration);
        // // SetCanvasGroup(roleOptionsPanel, true, transitionDuration);
        // StartCoroutine(ShowRoleOptionsAfterDelay(questionIntroDelay));

        optionsReceived++;

        Roles_CheckOptionsAndActivitySet();
    }

    public void Roles_SetActivity(int index)
    {
        activityReceived = true;

        var activity = activityDatabase.activities[index];

        string activityDescription = activity.activityDescription;
        // add a newline on the last space before 25 characters if the description is too long
        if (activityDescription.Length > 30)
        {
            int lastSpace = activityDescription.Substring(0, 30).LastIndexOf(' ');
            if (lastSpace > 0)
            {
                activityDescription = activityDescription.Substring(0, lastSpace) + "\n" + activityDescription.Substring(lastSpace + 1);
            }
        }



        roleOptionsActivityText.text = activityDescription;
        activityText.text = activity.activityDescription;
        activityGoalText.text = activity.activityDescription;

        // instantiate the activity onto the game object
        currentActivity = Instantiate(activity, activityParent.transform);

        Roles_CheckOptionsAndActivitySet();
    }

    private void Roles_CheckOptionsAndActivitySet()
    {
        if (optionsReceived >= expectedOptionsReceived && activityReceived)
        {
            SetCanvasGroup(rolesQuestionPanel, false, transitionDuration);
            SetCanvasGroup(promptIntroPanel, true, transitionDuration);
            StartCoroutine(ShowRoleOptionsAfterDelay(questionIntroDelay));
        }
    }

    private IEnumerator ShowRoleOptionsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetCanvasGroup(promptIntroPanel, false, transitionDuration);
        SetCanvasGroup(roleOptionsPanel, true, transitionDuration);
    }

    public void Roles_DeleteFragment()
    {
        // remove last word from fragmentsTMP
        var fragments = roleOptionsText.text.TrimEnd().Split(' ');
        if (fragments.Length > 0)
        {
            roleOptionsText.text = string.Join(" ", fragments, 0, fragments.Length - 1) + " ";
        }
    }

    public void Roles_SubmitBot()
    {
        SetCanvasGroup(roleOptionsPanel, false, transitionDuration);
        SetCanvasGroup(rolesWaitingPanel, true, transitionDuration);

        networkPlayer.bot.Value = roleOptionsText.text;
    }

    public void Roles_StartActivity()
    {
        // TODO: support multiple activities

        SetCanvasGroup(rolesWaitingPanel, false, transitionDuration);
        Debug.Log("Starting activity: ");
        // baristaActivity.SetActive(true);

        activityParent.SetActive(true);

        // show all player avatars
        var networkPlayers = FindObjectsOfType<NetworkPlayer>();
        foreach (var player in networkPlayers)
        {
            player.ShowAvatar();
            player.SetNameActive(true);
        }

        StartCoroutine(Roles_ShowActivityFlow());


    }

    private IEnumerator Roles_ShowActivityFlow()
    {
        playerAvatarParent.transform.DOLocalMoveX(-300, 0);
        cpuAvatarParent.transform.DOLocalMoveX(300, 0);
        SetCanvasGroup(activityIntroCanvas, true, transitionDuration);
        yield return new WaitForSeconds(questionIntroDelay);
        SetCanvasGroup(activityIntroCanvas, false, transitionDuration);
        SetCanvasGroup(activityGoalCanvas, true, transitionDuration);
        yield return new WaitForSeconds(questionIntroDelay);
        SetCanvasGroup(activityGoalCanvas, false, transitionDuration);

        SetCanvasGroup(conversationPanel, true, transitionDuration);




        // TODO: decide how to handle the background / ar and when to toggle
        // right now, I'm building for AR first, so when the activity is set, we'll just toggle the background off
        // SetCanvasGroup(rolesBackground, false, transitionDuration);

        StartCoroutine(ShowPlayerTurn());
    }

    public void Roles_SetTurnOrder(string order)
    {
        Debug.Log("[PlayerManager]: turn order set");
        // turn order is a list of ulongs
        List<ulong> turnOrder = RolesManager.GetTurnOrder(order);
        // current player is turnOrder[0]

        // get player name from NetworkPlayer
        var networkPlayers = FindObjectsOfType<NetworkPlayer>();
        foreach (var player in networkPlayers)
        {
            if (player.OwnerClientId == turnOrder[0])
            {
                playerTurnText.text = player.playerName.Value.ToString();
            }
        }

        // if this is the very first turn, don't do that coroutine since it is done in ShowActivityFlow

        if (turnOrder.Count != networkPlayers.Length)
        {
            StartCoroutine(ShowPlayerTurn());
        }


    }

    bool nextLineInteractableBase = false;

    private IEnumerator ShowPlayerTurn()
    {
        Debug.Log("Show player turn");
        nextLineInteractableBase = false;
        SetCanvasGroup(conversationCanvasGroup, false, transitionDuration);
        playerTurnText.DOFade(1, transitionDuration);
        playerTurnText.transform.parent.GetComponent<Image>().DOFade(1, transitionDuration);
        playerAvatarParent.transform.DOLocalMoveX(-300, 1);
        cpuAvatarParent.transform.DOLocalMoveX(300, 1);
        yield return new WaitForSeconds(1);
        SwapConversationAvatar();
        SwapCPUAvatar();
        playerAvatarParent.transform.DOLocalMoveX(-93, 1);
        cpuAvatarParent.transform.DOLocalMoveX(93, 1);
        yield return new WaitForSeconds(1);
        playerTurnText.DOFade(0, transitionDuration);
        playerTurnText.transform.parent.GetComponent<Image>().DOFade(0, transitionDuration);
        SetCanvasGroup(conversationCanvasGroup, true, transitionDuration);
        nextLineInteractableBase = true;

    }

    public void Roles_SetConversation(Conversation conversation)
    {
        // SetCanvasGroup(conversationPanel, true, transitionDuration);

        conversationCanvas.SetConversation(conversation);
    }

    public void Roles_SetConversationIndex(int index)
    {
        conversationCanvas.SetConversationIndex(index);
    }

    public void Roles_SetMyTurn(bool turn)
    {
        myTurn = turn;

        nextLineButton.gameObject.SetActive(turn);
    }

    public void Roles_SetBotPrompt(string prompt)
    {
        currentPromptText.text = prompt;
    }

    public void Roles_AdvanceConversation()
    {
        if (myTurn)
        {
            networkPlayer.AdvanceConversationOnServer();
        }
    }

    public void Roles_SetVotingState(int state)
    {
        switch (state)
        {
            case 0:
                // voting not started
                break;
            case 1:
                // voting started
                Roles_GoToVoting();
                break;
            case 2:
                // voting results
                Roles_EndVotingAndShowResults();
                break;
        }
    }

    public void Roles_GoToVoting()
    {
        SetCanvasGroup(rolesBackground, true, transitionDuration);
        SetCanvasGroup(conversationCanvasGroup, false, transitionDuration);
        SetCanvasGroup(conversationPanel, false, transitionDuration);
        SetCanvasGroup(votingCanvas, true, transitionDuration);
        SetCanvasGroup(votingTitleCanvas, true, transitionDuration);
        SetCanvasGroup(votingScoresCanvas, false);

        // tween avatar slots out
        playerAvatarParent.transform.DOLocalMoveX(-300, 1);
        cpuAvatarParent.transform.DOLocalMoveX(300, 1);
        // if you are on wide screen this doesn't work, lol
        // screw it, I'll fix it later
        // TODO: fix this

        // set up voting buttons

        // turn off all buttons
        for (int i = 0; i < voteButtons.Length; i++)
        {
            voteButtons[i].gameObject.SetActive(false);
        }

        var networkPlayers = FindObjectsOfType<NetworkPlayer>();
        for (int i = 0; i < networkPlayers.Length; i++)
        {
            NetworkPlayer player = networkPlayers[i];
            if (player.OwnerClientId != networkPlayer.OwnerClientId)
            {
                var voteButton = voteButtons[i];
                voteButton.SetNetworkPlayer(player);
                voteButton.gameObject.SetActive(true);
                var button = voteButton.GetComponent<Button>();
                ulong OwnerClientId = player.OwnerClientId;
                button.onClick.AddListener(() =>
                {
                    Roles_VoteForPlayer(OwnerClientId);
                    voteButton.OnClick();
                });
            }
            else
            {
                var voteButton = voteButtons[i];
                voteButton.SetNetworkPlayer(player);
                voteButton.gameObject.SetActive(true);
                var button = voteButton.GetComponent<Button>();
                ulong OwnerClientId = player.OwnerClientId;
                voteButton.SetInteractable(false);
            }
        }
    }

    public void Roles_VoteForPlayer(ulong playerId)
    {
        // remove all vote listeners
        foreach (var button in voteButtons)
        {
            button.GetComponent<Button>().onClick.RemoveAllListeners();
        }

        networkPlayer.votedPlayer.Value = playerId;
    }

    public void Roles_EndVotingAndShowResults()
    {
        foreach (var button in voteButtons)
        {
            button.ShowTotalVotes();
        }

        StartCoroutine(Roles_ShowScoresAfterDelay(3));
    }

    private IEnumerator Roles_ShowScoresAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Roles_ShowScores();
        yield return new WaitForSeconds(3);
        Roles_ShowTotalScores();
    }

    public void Roles_ShowScores()
    {
        SetCanvasGroup(votingScoresCanvas, true, transitionDuration);
        SetCanvasGroup(votingTitleCanvas, false, transitionDuration);

        // go through all players and denote first, second, third.
        var networkPlayers = FindObjectsOfType<NetworkPlayer>();
        Array.Sort(networkPlayers, (a, b) => b.votes.Value.CompareTo(a.votes.Value));

        foreach (var button in voteButtons)
        {
            button.ShowNewScores(Array.IndexOf(networkPlayers, button.GetNetworkPlayer()) + 1);
        }

        nextRoundButton.interactable = false;

        bool scoreLimitReached = false;
        foreach (var player in networkPlayers)
        {
            if (player.score.Value >= 10)
            {
                scoreLimitReached = true;
                break;
            }
        }

        if (scoreLimitReached)
        {
            nextRoundTMP.text = "Finish game";
            nextRoundButton.onClick.AddListener(Roles_EndGame);

        }
        else
        {
            nextRoundTMP.text = "Start next round";
            nextRoundButton.onClick.AddListener(Roles_StartNextActivity);
        }

    }

    private void Roles_EndGame()
    {
        nextRoundButton.onClick.RemoveAllListeners();
        networkPlayer.EndGameOnServer();
    }

    private void Roles_StartNextActivity()
    {
        nextRoundButton.onClick.RemoveAllListeners();

        var rolesManager = FindObjectOfType<RolesManager>();
        rolesManager.SendRoleQuestions();
    }

    private void Roles_ShowTotalScores()
    {
        foreach (var button in voteButtons)
        {
            button.ShowTotalScore();
        }

        nextRoundButton.interactable = true;
    }



    #endregion


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
