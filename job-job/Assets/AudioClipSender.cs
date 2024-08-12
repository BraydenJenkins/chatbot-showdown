using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class AudioClipSender : NetworkBehaviour {
    public AudioSource audioSource; // Reference to the AudioSource to play from

    public void SendAudioClip(AudioClip audioClip) {
        float[] audioSamples = new float[audioClip.samples * audioClip.channels];
        audioClip.GetData(audioSamples, 0);
        byte[] byteArray = FloatArrayToByteArray(audioSamples);

        // Send audio data to all clients except the sender
        SendAudioDataServerRpc(byteArray, audioClip.channels, audioClip.frequency);
    }

    [ServerRpc]
    private void SendAudioDataServerRpc(byte[] audioData, int channels, int frequency, ServerRpcParams rpcParams = default) {
        // Exclude the sending client from receiving the RPC
        ClientRpcParams clientRpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams {
                TargetClientIds = NetworkManager.Singleton.ConnectedClientsList
                    .Where(client => client.ClientId != rpcParams.Receive.SenderClientId)
                    .Select(client => client.ClientId).ToArray()
            }
        };

        // Send the data to all other clients
        ReceiveAudioDataClientRpc(audioData, channels, frequency, clientRpcParams);
    }

    [ClientRpc]
    private void ReceiveAudioDataClientRpc(byte[] audioData, int channels, int frequency, ClientRpcParams rpcParams = default) {
        float[] audioSamples = ByteArrayToFloatArray(audioData);
        AudioClip audioClip = AudioClip.Create("ReceivedClip", audioSamples.Length / channels, channels, frequency, false);
        audioClip.SetData(audioSamples, 0);

        // Play the received audio clip using the assigned AudioSource
        if (audioSource != null) {
            audioSource.clip = audioClip;
            audioSource.Play();
        } else {
            // Fallback: Play clip at a point in the world
            AudioSource.PlayClipAtPoint(audioClip, Vector3.zero);
        }
    }

    private byte[] FloatArrayToByteArray(float[] floatArray) {
        int len = floatArray.Length * 4;
        byte[] byteArray = new byte[len];
        Buffer.BlockCopy(floatArray, 0, byteArray, 0, len);
        return byteArray;
    }

    private float[] ByteArrayToFloatArray(byte[] byteArray) {
        float[] floatArray = new float[byteArray.Length / 4];
        Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
        return floatArray;
    }
}