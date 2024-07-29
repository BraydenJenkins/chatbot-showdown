using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;

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


    // job job
    [Header("Job Job")]
    [SerializeField] private CanvasGroup jobjobCanvas;
    [SerializeField] private GameObject questionPanel, waitingPanel;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_InputField answerInputField;

    private void Awake()
    {
        jobjobCanvas.alpha = 0;
        jobjobCanvas.interactable = false;
        jobjobCanvas.blocksRaycasts = false;
        questionPanel.SetActive(false);
        waitingPanel.SetActive(false);
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
}
