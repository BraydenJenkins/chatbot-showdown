using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ConversationAPI;
using UnityEngine.UI;
using DG.Tweening;
using System;
using ElevenLabs.Voices;
using Unity.Netcode;
using QFSW.QC.Parsers;


public class ConversationCanvas : MonoBehaviour
{
    [SerializeField] private TMP_Text conversationText;

    private List<int> messageLengths;
    private int currentMessageIndex = 0;

    public bool animatingConversation = false;

    [SerializeField] private Image leftTriangle, rightTriangle;

    private List<bool> messageIsPlayer;

    [SerializeField] private AvatarDatabase avatarDatabase, cpuDatabase;
    [SerializeField] private Voice defaultVoice;

    [SerializeField] private Transform playerAvatarTransform, cpuAvatarTransform;

    private List<string> conversationAnimations;

    public void SetConversation(Conversation conversation)
    {
        List<string> roles = new List<string>();

        int playerRoleIndex = -1;

        // get roles from conversation (there should be only two)
        for (int i = 0; i < conversation.messages.Length; i++)
        {
            Message part = conversation.messages[i];
            if (part.role.ToLower() == "player")
            {
                playerRoleIndex = i;
            }

            if (!roles.Contains(part.role))
            {
                roles.Add(part.role);
            }
        }

        if (roles.Count != 2)
        {
            Debug.LogError("ConversationCanvas: conversation does not have two roles");
            return;
        }



        // TODO: do we reliably know who starts the conversation?
        // for now, assume the agent (not player) starts the conversation

        // actually, one of the roles SHOULD be "player"
        // so we can just use that to know who goes first and set up alignment

        // player is on the left, agent is on the right

        Dictionary<string, string> roleAlignments = new Dictionary<string, string>();

        messageIsPlayer = new List<bool>();
        conversationAnimations = new List<string>();

        // if no player found, we will just assume the first role is the agent
        if (playerRoleIndex == -1)
        {
            Debug.LogError("ConversationCanvas: conversation does not have a player role");
            roleAlignments.Add(roles[0], "right");
            roleAlignments.Add(roles[1], "left");
        }
        else
        {
            if (playerRoleIndex == 0)
            {
                roleAlignments.Add(roles[0], "left");
                roleAlignments.Add(roles[1], "right");
            }
            else
            {
                roleAlignments.Add(roles[0], "right");
                roleAlignments.Add(roles[1], "left");
            }
        }

        messageLengths = new List<int>();
        conversationText.maxVisibleCharacters = 0;
        string fullText = "";
        foreach (var part in conversation.messages)
        {
            string alignment = "<align=\"" + roleAlignments[part.role] + "\">";
            string message = "\n" + alignment + part.role + " : " + part.content;
            fullText += message;
            int length = message.Length - alignment.Length;
            messageLengths.Add(length);
            messageIsPlayer.Add(roleAlignments[part.role] == "left");
            conversationAnimations.Add(part.animation);
        }

        conversationText.text = fullText;

        currentMessageIndex = 0;

        Debug.Log("New conversation began.");

        // rightTriangle.DOColor(Color.white, 0.25f);
        // leftTriangle.DOColor(Color.clear, 0.25f);

        LayoutRebuilder.ForceRebuildLayoutImmediate(conversationText.transform.parent.GetComponent<RectTransform>());
    }

    private Coroutine animationRoutine;

    public void SetConversationIndex(int newIndex)
    {
        if (newIndex < 0 || newIndex > messageLengths.Count)
        {
            return;
        }

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        if (newIndex == currentMessageIndex + 1)
        {
            Debug.Log("Advancing conversation by one index.");
            // expected behavior, we are attempting to advance the conversation by one message
            AdvanceConversation();
        }
        else if (newIndex > currentMessageIndex + 1)
        {
            Debug.Log("Skipping ahead in the conversation from " + currentMessageIndex + " to " + newIndex);
            // looks like we are trying to skip ahead in the conversation
            // so just show the full message up to that point.
            int endLength = 0;
            for (int i = 0; i <= newIndex; i++)
            {
                endLength += messageLengths[i];
            }
            conversationText.maxVisibleCharacters = endLength;
            currentMessageIndex = newIndex;
        }
        else
        {
            Debug.Log("Going back in the conversation from " + currentMessageIndex + " to " + newIndex);
            // we are trying to go back in the conversation
            // so just show the full message up to that point.

            // actually, if newIndex is zero then clear out the conversation
            if (newIndex == 0)
            {
                conversationText.maxVisibleCharacters = 0;
                currentMessageIndex = 0;
                return;
            }

            int endLength = 0;
            for (int i = 0; i <= newIndex; i++)
            {
                endLength += messageLengths[i];
            }
            conversationText.maxVisibleCharacters = endLength;
            currentMessageIndex = newIndex;
        }
    }

    private int GetIndexOfAnimation(string animationName)
    {
        // ['idle', 'annoyed shake', 'defeat', 'nod yes', 'pointing', 'salute', 'shake fist', 'shake head no', 'strong taunt', 'mean taunt', 'wave', 'whatever']
        string[] anims = new string[] { "idle", "annoyed shake", "defeat", "nod yes", "pointing", "salute", "shake fist", "shake head no", "strong taunt", "mean taunt", "wave", "whatever" };
        for (int i = 0; i < anims.Length; i++)
        {
            if (anims[i] == animationName)
            {
                return i;
            }
        }
        return 0;
    }

    public void AdvanceConversation()
    {
        // conversationText.maxVisibleCharacters += messageLengths[currentMessageIndex];
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            // if we stopped the coroutine early, we need to set the maxVisibleCharacters to the end of the current message
            int endLength = 0;
            for (int i = 0; i <= currentMessageIndex; i++)
            {
                endLength += messageLengths[i];
            }
            conversationText.maxVisibleCharacters = endLength;


        }

        Voice voice = defaultVoice;

        // hacky, but gonna get voice from avatar database using the transform (finding which child is active) lol

        try
        {
            if (messageIsPlayer[currentMessageIndex])
            {
                var children = playerAvatarTransform.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    if (child == playerAvatarTransform)
                    {
                        continue;
                    }
                    if (child.gameObject.activeSelf)
                    {
                        voice = avatarDatabase.avatars[child.GetSiblingIndex()].voice;
                        var animator = child.GetComponent<Animator>();
                        animator.SetInteger("AnimIndex", GetIndexOfAnimation(conversationAnimations[currentMessageIndex]));
                        StartCoroutine(EndAnimation(animator));
                        Debug.Log("Found player voice: " + voice.Name + ", sibling index: " + child.GetSiblingIndex() + ", child name: " + child.name);
                        break;
                    }
                }
            }
            else
            {
                var children = cpuAvatarTransform.GetComponentsInChildren<Transform>();
                foreach (var child in children)
                {
                    if (child == cpuAvatarTransform)
                    {
                        continue;
                    }
                    if (child.gameObject.activeSelf)
                    {
                        voice = cpuDatabase.avatars[child.GetSiblingIndex()].voice;
                        var animator = child.GetComponent<Animator>();
                        animator.SetInteger("AnimIndex", GetIndexOfAnimation(conversationAnimations[currentMessageIndex]));
                        StartCoroutine(EndAnimation(animator));
                        Debug.Log("Found cpu voice: " + voice.Name + ", sibling index: " + child.GetSiblingIndex() + ", child name: " + child.name);
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error getting voice: " + e.Message);
        }


        animationRoutine = StartCoroutine(AnimateConversation(voice));

        // swap the triangles
        if (messageIsPlayer[currentMessageIndex])
        {
            rightTriangle.DOColor(Color.clear, 0);
            leftTriangle.DOColor(Color.white, 0);
        }
        else
        {
            rightTriangle.DOColor(Color.white, 0);
            leftTriangle.DOColor(Color.clear, 0);
        }

        currentMessageIndex++;
    }

    private IEnumerator EndAnimation(Animator animator)
    {
        yield return new WaitForSeconds(0.25f);
        animator.SetInteger("AnimIndex", 0);
    }


    private IEnumerator AnimateConversation(Voice voice)
    {
        animatingConversation = true;
        // add to maxVisibleCharacters one by one until we reach the end of the current message
        int endLength = conversationText.maxVisibleCharacters + messageLengths[currentMessageIndex];

        string message = conversationText.GetParsedText().Substring(conversationText.maxVisibleCharacters, messageLengths[currentMessageIndex]);

        // remove the role from the message
        message = message.Substring(message.IndexOf(":") + 1).Trim();

        Debug.Log("Message: " + message);

        if (voiceRequest == null)
        {
            voiceRequest = FindAnyObjectByType<VoiceRequest>();
        }

        if (voiceRequest != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                StartCoroutine(SendVoiceRequest(message, voice));
            }

        }

        while (conversationText.maxVisibleCharacters < endLength)
        {
            conversationText.maxVisibleCharacters++;

            yield return new WaitForSeconds(0.03f);
        }

        animatingConversation = false;

        animationRoutine = null;
    }

    private IEnumerator SendVoiceRequest(string msg, Voice voice)
    {
        Debug.Log("Sending voice request: " + msg);
        voiceRequest.SendVoiceRequest(msg, voice);
        yield return null;
    }

    private VoiceRequest voiceRequest;

}
