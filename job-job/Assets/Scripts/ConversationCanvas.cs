using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ConversationAPI;
using UnityEngine.UI;


public class ConversationCanvas : MonoBehaviour
{
    [SerializeField] private TMP_Text conversationText;

    private List<int> messageLengths;
    private int currentMessageIndex = 0;

    public void SetConversation(Conversation conversation)
    {
        List<string> roles = new List<string>();

        // get roles from conversation (there should be only two)
        foreach (var part in conversation.messages)
        {
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
        // agent is left aligned

        Dictionary<string, string> roleAlignments = new Dictionary<string, string>
        {
            { roles[0], "left" },
            { roles[1], "right" }
        };

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
        }

        conversationText.text = fullText;

        currentMessageIndex = 0;

        Debug.Log("New conversation began.");

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
        animationRoutine = StartCoroutine(AnimateConversation());

        currentMessageIndex++;
    }



    private IEnumerator AnimateConversation()
    {
        // add to maxVisibleCharacters one by one until we reach the end of the current message
        int endLength = conversationText.maxVisibleCharacters + messageLengths[currentMessageIndex];
        while (conversationText.maxVisibleCharacters < endLength)
        {
            conversationText.maxVisibleCharacters++;
            yield return new WaitForSeconds(0.05f);
        }

        animationRoutine = null;
    }

}
