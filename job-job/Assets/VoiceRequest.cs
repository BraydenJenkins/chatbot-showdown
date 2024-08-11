using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async;
using ElevenLabs.Models;
using ElevenLabs.Voices;
using ElevenLabs;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class VoiceRequest : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClipSender audioClipSender;
    [SerializeField]
    private ElevenLabsConfiguration configuration;

    public CharacterProfile[] characterProfiles;

    private readonly Queue<AudioClip> streamClipQueue = new();
    private ElevenLabsClient api;

    private void OnValidate() {
        if (characterProfiles.Length > 0) {
            foreach (CharacterProfile cp in characterProfiles)
                if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SendVoiceRequest("Hey hi", 0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SendVoiceRequest("Hey hi", 1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SendVoiceRequest("Hey hi", 2);
    }

    private async void Start() {
        //OnValidate();

        try {
            api = new ElevenLabsClient(configuration) {
                EnableDebug = false
            };
            // assign voice if missing
            if (characterProfiles.Length > 0) {
                foreach (CharacterProfile cp in characterProfiles)
                    if (cp.voice == null) cp.voice = (await api.VoicesEndpoint.GetAllVoicesAsync(destroyCancellationToken)).FirstOrDefault();
            }
            
            //streamClipQueue.Clear();
            //var streamQueueCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            //PlayStreamQueue(streamQueueCts.Token);                    
        } catch (Exception e) {
            Debug.LogError(e);
        }
    }

    public async void SendVoiceRequest(string msg, int index) {
        Debug.Log("Sending voice request");
        var defaultVoiceSettings = await api.VoicesEndpoint.GetDefaultVoiceSettingsAsync();
        var voiceClip = await api.TextToSpeechEndpoint.TextToSpeechAsync(msg, characterProfiles[index].voice, defaultVoiceSettings);

        audioSource.PlayOneShot(voiceClip.AudioClip);
        audioClipSender.SendAudioClip(voiceClip.AudioClip);
    }    

   
}


[System.Serializable]
public class CharacterProfile {
    public Voice voice;

    public CharacterProfile(Voice voice)
    {
        this.voice = voice;
    }
}