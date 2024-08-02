using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;

namespace GeminiAPI
{
    [Serializable]
    public class GeminiContentPart
    {
        public string text;
    }

    [Serializable]
    public class GeminiContent
    {
        public GeminiContentPart[] parts;
    }

    [Serializable]
    public class GeminiCandidate
    {
        public GeminiContent content;
    }

    [Serializable]
    public class GeminiApiResponse
    {
        public GeminiCandidate[] candidates;
    }
}

public class ThinkerModule
{
    private string apiKey = "";
    private string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=";

    public ThinkerModule()
    {
        // load api key from env file (note, don't do this in production)
        Debug.Log("Loading Gemini API key from env variable");
        EnvVariable geminiKey = Resources.Load<EnvVariable>("geminiApiKey");
        if (geminiKey != null)
        {
            apiKey = geminiKey.value;
        }
        else
        {
            Debug.LogError("Failed to load Gemini API key from resources (geminiApiKey)");
        }
    }

    public async Task<string> GetCompletion(string prompt)
    {
        string fullUrl = url + apiKey;
        string jsonData = "{\"contents\": [{\"parts\":[{\"text\": \"" + prompt + "\"}]}]}";

        using (UnityWebRequest webRequest = new UnityWebRequest(fullUrl, "POST"))
        {
            byte[] bodyRaw = new System.Text.UTF8Encoding().GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            Debug.Log("Request: " + jsonData);

            var operation = webRequest.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error: " + webRequest.error);
                return null;
            }
            else
            {
                string response = webRequest.downloadHandler.text;
                Debug.Log("Response: " + response);

                // Parse the JSON response
                GeminiAPI.GeminiApiResponse apiResponse = JsonUtility.FromJson<GeminiAPI.GeminiApiResponse>(response);

                // Return the generated content
                if (apiResponse.candidates.Length > 0 && apiResponse.candidates[0].content.parts.Length > 0)
                {
                    return apiResponse.candidates[0].content.parts[0].text;
                }
                else
                {
                    Debug.LogError("No content generated");
                    return null;
                }
            }
        }
    }

    // // TESTING
    // IEnumerator GetCompletionCoroutine(string prompt)
    // {
    //     var task = GetCompletion(prompt);

    //     // Wait until the task is completed
    //     yield return new WaitUntil(() => task.IsCompleted);

    //     if (task.IsCompletedSuccessfully)
    //     {
    //         string result = task.Result;
    //         Debug.Log("Generated content: " + result);
    //         // Use the result here
    //     }
    //     else
    //     {
    //         Debug.LogError("Failed to get completion: " + task.Exception);
    //     }
    // }

    // private void Start()
    // {
    //     StartCoroutine(GetCompletionCoroutine("write a couplet about minions from despicable me"));
    // }
}
