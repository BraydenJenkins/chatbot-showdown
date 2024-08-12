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

    [Header("Voting")]
    [SerializeField] private CanvasGroup votingGroup;
    [SerializeField] private TMP_Text botPromptText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private AvatarButton avatarButton;
    [SerializeField] private Image plusOneImage;

    [SerializeField] private CanvasGroup totalVotesGroup;
    [SerializeField] private TMP_Text totalVotesText;

    [SerializeField] private Color uninteractableColor;

    [Header("Scoring")]
    [SerializeField] private CanvasGroup scoringGroup;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image[] scoreBoxes;

    [SerializeField] private Color scoreBoxEmptyColor;
    [SerializeField] private Color scoreBoxFilledColor;
    [SerializeField] private Color scoreBoxNewColor;


    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        votingGroup.alpha = 1;
        scoringGroup.alpha = 0;
    }


    private NetworkPlayer networkPlayer;
    public NetworkPlayer GetNetworkPlayer()
    {
        return networkPlayer;
    }

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
        int votes = 0;
        if (networkPlayer != null)
        {
            votes = networkPlayer.votes.Value;
        }
        totalVotesText.text = votes.ToString() + " VOTES";
        if (votes > 0)
        {
            totalVotesGroup.DOFade(1, 0.25f);
        }
        plusOneImage.DOFade(0, 0.25f);
    }

    public void ShowNewScores(int place)
    {
        votingGroup.DOFade(0, 0.25f);
        scoringGroup.DOFade(1, 0.25f);

        // so we need to show the new votes first, then the previous scores, then the rest of the boxes are empty
        int votes = 0;
        int totalScore = 0;
        if (networkPlayer != null)
        {
            votes = networkPlayer.votes.Value;
            totalScore = networkPlayer.score.Value;
        }

        int previousScore = totalScore - votes;

        for (int i = 0; i < scoreBoxes.Length; i++)
        {
            if (i < votes)
            {
                scoreBoxes[i].color = scoreBoxNewColor;
            }
            else if (i < previousScore)
            {
                scoreBoxes[i].color = scoreBoxFilledColor;
            }
            else
            {
                scoreBoxes[i].color = scoreBoxEmptyColor;
            }
        }

        switch (place)
        {
            case 1:
                playerNameText.text = "1ST PLACE";
                break;
            case 2:
                playerNameText.text = "2ND PLACE";
                break;
            case 3:
                playerNameText.text = "3RD PLACE";
                break;
            default:
                playerNameText.text = "";
                break;
        }
    }

    public void ShowTotalScore()
    {
        string playerName = "";
        int totalScore = 0;
        if (networkPlayer != null)
        {
            playerName = networkPlayer.playerName.Value.ToString();
            totalScore = networkPlayer.score.Value;
        }

        playerNameText.text = playerName;

        for (int i = 0; i < scoreBoxes.Length; i++)
        {
            if (i < totalScore)
            {
                scoreBoxes[i].color = scoreBoxFilledColor;
            }
            else
            {
                scoreBoxes[i].color = scoreBoxEmptyColor;
            }
        }
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
