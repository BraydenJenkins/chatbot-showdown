using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using ConversationAPI;

public class PlayerManager : MonoBehaviour
{
    private NetworkPlayer networkPlayer;

    //  stores references to avatar ui
    [SerializeField] private PagingScrollRect avatarSelectionScrollRect;
    [SerializeField] private TMP_Text playerNameText;

    [SerializeField] private float transitionDuration = 0.5f;

    public void LinkNetworkPlayer(NetworkPlayer player)
    {
        networkPlayer = player;

        networkPlayer.avatarIndex.Value = avatarSelectionScrollRect.CurrentPageIndex;
        networkPlayer.playerName.Value = playerNameText.text;
    }

    private void Awake()
    {
        JJ_Awake();
        Roles_Awake();
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

    [SerializeField] private CanvasGroup rolesQuestionPanel, roleOptionsPanel, rolesWaitingPanel, conversationPanel;

    // questions
    [SerializeField] private TMP_Text rolesQuestionText;
    [SerializeField] private TMP_InputField roleAnswerInputField;

    // bot creation
    [SerializeField] private RectTransform roleOptionsArea;
    [SerializeField] private TMP_Text roleOptionsText;

    // activity
    [SerializeField] private GameObject baristaActivity;
    [SerializeField] private ConversationCanvas conversationCanvas;

    private void Roles_Awake()
    {
        SetCanvasGroup(rolesCanvas, false);
        SetCanvasGroup(rolesQuestionPanel, false);
        SetCanvasGroup(roleOptionsPanel, false);
        SetCanvasGroup(rolesWaitingPanel, false);
        SetCanvasGroup(conversationPanel, false);

        // ridiculous input field workarounds (jfc unity)
        roleAnswerInputField.onEndEdit.AddListener(Roles_OnEndEdit);
        roleAnswerInputField.onTouchScreenKeyboardStatusChanged.AddListener(Roles_OnTouchScreenKeyboardStatusChanged);
    }

    public void Roles_SetQuestion(string question)
    {
        SetCanvasGroup(rolesCanvas, true, transitionDuration);
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
            var button = Instantiate(wordButtonPrefab, roleOptionsArea);
            button.GetComponentInChildren<TMP_Text>().text = optionsArray[i];
            string option = optionsArray[i];
            button.onClick.AddListener(() =>
            {
                roleOptionsText.text += option + " ";
            });
        }

        SetCanvasGroup(rolesQuestionPanel, false, transitionDuration);
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
        networkPlayer.bot.Value = roleOptionsText.text;

        SetCanvasGroup(roleOptionsPanel, false, transitionDuration);
        SetCanvasGroup(rolesWaitingPanel, true, transitionDuration);
    }

    public void Roles_SetActivity(int index)
    {
        // TODO: support multiple activities

        SetCanvasGroup(rolesWaitingPanel, false, transitionDuration);
        baristaActivity.SetActive(true);
    }

    public void Roles_SetConversation(Conversation conversation)
    {
        SetCanvasGroup(conversationPanel, true, transitionDuration);

        conversationCanvas.SetConversation(conversation);


    }

    #endregion


    private void SetCanvasGroup(CanvasGroup canvasGroup, bool active, float fadeDuration = 0)
    {
        canvasGroup.DOFade(active ? 1 : 0, fadeDuration);
        canvasGroup.interactable = active;
        canvasGroup.blocksRaycasts = active;
    }
}
