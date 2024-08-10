using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class VoteButton : MonoBehaviour
{
    [SerializeField] private AvatarDatabase avatarDatabase;
    [SerializeField] private TMP_Text botPromptText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private AvatarButton avatarButton;
    [SerializeField] private Image plusOneImage;

    [SerializeField] private CanvasGroup totalVotesGroup;
    [SerializeField] private TMP_Text totalVotesText;

    [SerializeField] private Color uninteractableColor;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }


    private NetworkPlayer networkPlayer;

    public void SetNetworkPlayer(NetworkPlayer player)
    {
        networkPlayer = player;
        player.bot.OnValueChanged += UpdateBot;
        UpdateBot("", player.bot.Value);

        player.avatarIndex.OnValueChanged += UpdateAvatar;
        UpdateAvatar(-1, player.avatarIndex.Value);


    }

    public void OnClick()
    {
        avatarButton.SetSelected();

        backgroundImage.DOColor(Color.black, 0.25f);
        plusOneImage.DOFade(1, 0.25f);

        botPromptText.DOColor(Color.white, 0.25f);
    }

    public void SetInteractable(bool interactable)
    {
        button.interactable = interactable;
        backgroundImage.color = interactable ? Color.white : Color.grey;
    }

    public void ShowTotalVotes()
    {
        int votes = networkPlayer.votes.Value;
        totalVotesText.text = votes.ToString() + " VOTES";
        if (votes > 0)
        {
            totalVotesGroup.DOFade(1, 0.25f);
        }
        plusOneImage.DOFade(0, 0.25f);
    }


    private void OnDestroy()
    {
        if (networkPlayer != null)
        {
            networkPlayer.bot.OnValueChanged -= UpdateBot;
            networkPlayer.avatarIndex.OnValueChanged -= UpdateAvatar;
        }
    }

    private void UpdateAvatar(int previousValue, int newValue)
    {
        if (newValue < -1 || newValue >= avatarDatabase.avatars.Length)
        {
            Debug.LogError("Invalid avatar index: " + newValue);
            return;
        }

        avatarImage.sprite = avatarDatabase.avatars[newValue].avatarImage;
    }

    private void UpdateBot(FixedString512Bytes previousValue, FixedString512Bytes newValue)
    {
        botPromptText.text = newValue.ToString();
    }

}
