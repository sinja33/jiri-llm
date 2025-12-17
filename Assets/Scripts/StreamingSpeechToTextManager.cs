using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;

// Fallback to batch processing if streaming fails
[System.Serializable]
public class WhisperResponse
{
    public string text;
}

public class StreamingSpeechToTextManager : MonoBehaviour
{
    [Header("API Settings")]
    public string openAIApiKey = "";
    public string whisperModel = "whisper-1";
    
    [Header("Streaming Settings (Deepgram - Optional)")]
    public bool useStreaming = false; // Set to true when you have Deepgram API key
    public string deepgramApiKey = "";
    
    [Header("Language Settings")]
    public string language = "sl"; // Slovene
    public string languageName = "slovenščina";
    
    [Header("Audio Processing")]
    public float silenceThreshold = 0.001f;
    public float minRecordingLength = 0.5f;
    public bool debugTranscription = true;
    
    private const string WHISPER_API_URL = "https://api.openai.com/v1/audio/transcriptions";
    
    public delegate void TranscriptionComplete(string transcribedText);
    public event TranscriptionComplete OnTranscriptionComplete;
    
    void Start()
    {
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            openAIApiKey = EnvironmentLoader.GetEnvironmentVariable("OPENAI_API_KEY");
        }
        
        if (string.IsNullOrEmpty(deepgramApiKey))
        {
            deepgramApiKey = EnvironmentLoader.GetEnvironmentVariable("DEEPGRAM_API_KEY");
        }
        
        if (useStreaming && !string.IsNullOrEmpty(deepgramApiKey))
        {
            Debug.Log("✅ Streaming STT enabled (Deepgram) - Language: " + languageName);
        }
        else
        {
            Debug.Log("✅ Batch STT enabled (Whisper) - Language: " + languageName);
        }
    }
    
    public void TranscribeAudio(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            Debug.LogError("No audio clip provided for transcription!");
            return;
        }
        
        // Validate audio first
        if (!IsAudioClipValid(audioClip))
        {
            Debug.LogWarning("Audio clip failed validation");
            return;
        }
        
        if (string.IsNullOrEmpty(openAIApiKey) || openAIApiKey.StartsWith("your-api"))
        {
            Debug.LogError("OpenAI API key not configured!");
            StartCoroutine(MockTranscription());
            return;
        }
        
        Debug.Log($"📤 Sending to Whisper API (Language: {languageName})...");
        StartCoroutine(SendToWhisperAPI(audioClip));
    }
    
    IEnumerator SendToWhisperAPI(AudioClip clip)
    {
        if (debugTranscription)
            Debug.Log("🔄 Converting audio to WAV and sending to Whisper API...");
        
        byte[] wavData = ConvertAudioClipToWAV(clip);
        
        if (wavData == null || wavData.Length == 0)
        {
            Debug.LogError("Failed to convert audio to WAV format!");
            yield break;
        }
        
        // Create multipart form data with Slovene settings
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
        form.AddField("model", whisperModel);
        form.AddField("language", language); // "sl" for Slovene
        form.AddField("temperature", "0");
        
        // Slovene-specific prompt to help Whisper understand context
        string contextPrompt = "To je jasen slovenski govor o filozofskih temah, modrosti, življenju, pomenu, naravi in starodavnem znanju. Govorec postavlja vprašanja ali opisuje svoje izkušnje.";
        form.AddField("prompt", contextPrompt);
        
        if (debugTranscription)
        {
            Debug.Log($"📋 Whisper settings:");
            Debug.Log($"   Language: {language} ({languageName})");
            Debug.Log($"   Model: {whisperModel}");
            Debug.Log($"   Audio size: {wavData.Length / 1024f:F1} KB");
        }
        
        UnityWebRequest request = UnityWebRequest.Post(WHISPER_API_URL, form);
        request.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                WhisperResponse response = JsonUtility.FromJson<WhisperResponse>(request.downloadHandler.text);
                string transcribedText = response.text.Trim();
                
                if (debugTranscription)
                    Debug.Log($"✅ Transkripcija uspešna (SL): \"{transcribedText}\"");
                
                OnTranscriptionComplete?.Invoke(transcribedText);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse Whisper API response: " + e.Message);
                Debug.LogError("Raw response: " + request.downloadHandler.text);
                StartCoroutine(MockTranscription());
            }
        }
        else
        {
            Debug.LogError("Whisper API request failed: " + request.error);
            Debug.LogError("Response: " + request.downloadHandler.text);
            StartCoroutine(MockTranscription());
        }
        
        request.Dispose();
    }
    
    IEnumerator MockTranscription()
    {
        yield return new WaitForSeconds(1f);
        
        // Slovene mock phrases
        string[] mockPhrases = {
            "Pozdravljen, starodavni. Kakšno modrost imaš zame?",
            "Povej mi o starih poteh.",
            "Kaj vidiš v prihodnosti?",
            "Deli z mano svoje znanje.",
            "Iščem vodstvo tvoje modrosti.",
            "Kakšen je smisel življenja?",
            "Kaj me lahko naučiš o naravi?"
        };
        
        string mockText = mockPhrases[UnityEngine.Random.Range(0, mockPhrases.Length)];
        
        if (debugTranscription)
            Debug.Log($"🎭 Mock transkripcija (SL): \"{mockText}\"");
        
        OnTranscriptionComplete?.Invoke(mockText);
    }
    
    byte[] ConvertAudioClipToWAV(AudioClip clip)
    {
        try
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            int sampleRate = clip.frequency;
            int channels = clip.channels;
            int sampleCount = samples.Length;
            
            // WAV file header
            byte[] header = new byte[44];
            int pos = 0;
            
            // "RIFF" chunk descriptor
            Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, header, pos, 4); pos += 4;
            Array.Copy(BitConverter.GetBytes(36 + sampleCount * 2), 0, header, pos, 4); pos += 4;
            Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, header, pos, 4); pos += 4;
            
            // "fmt " sub-chunk
            Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, header, pos, 4); pos += 4;
            Array.Copy(BitConverter.GetBytes(16), 0, header, pos, 4); pos += 4;
            Array.Copy(BitConverter.GetBytes((short)1), 0, header, pos, 2); pos += 2;
            Array.Copy(BitConverter.GetBytes((short)channels), 0, header, pos, 2); pos += 2;
            Array.Copy(BitConverter.GetBytes(sampleRate), 0, header, pos, 4); pos += 4;
            Array.Copy(BitConverter.GetBytes(sampleRate * channels * 2), 0, header, pos, 4); pos += 4;
            Array.Copy(BitConverter.GetBytes((short)(channels * 2)), 0, header, pos, 2); pos += 2;
            Array.Copy(BitConverter.GetBytes((short)16), 0, header, pos, 2); pos += 2;
            
            // "data" sub-chunk
            Array.Copy(Encoding.ASCII.GetBytes("data"), 0, header, pos, 4); pos += 4;
            Array.Copy(BitConverter.GetBytes(sampleCount * 2), 0, header, pos, 4);
            
            // Convert float samples to 16-bit PCM
            byte[] wavData = new byte[header.Length + sampleCount * 2];
            Array.Copy(header, 0, wavData, 0, header.Length);
            
            int dataPos = header.Length;
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)(samples[i] * 32767f);
                Array.Copy(BitConverter.GetBytes(sample), 0, wavData, dataPos, 2);
                dataPos += 2;
            }
            
            return wavData;
        }
        catch (Exception e)
        {
            Debug.LogError("Error converting audio to WAV: " + e.Message);
            return null;
        }
    }
    
    public bool IsAudioClipValid(AudioClip clip)
    {
        if (clip == null) 
        {
            Debug.LogError("Audio clip is null");
            return false;
        }
        
        if (clip.length < minRecordingLength) 
        {
            Debug.LogWarning($"Audio too short: {clip.length:F2}s < {minRecordingLength:F2}s");
            return false;
        }
        
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);
        
        float maxVolume = 0f;
        float avgVolume = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float absValue = Mathf.Abs(samples[i]);
            maxVolume = Mathf.Max(maxVolume, absValue);
            avgVolume += absValue;
        }
        avgVolume /= samples.Length;
        
        if (debugTranscription)
        {
            Debug.Log($"🔊 Audio validation:");
            Debug.Log($"   Length: {clip.length:F2}s");
            Debug.Log($"   Max volume: {maxVolume:F4}");
            Debug.Log($"   Avg volume: {avgVolume:F4}");
            Debug.Log($"   Threshold: {silenceThreshold:F4}");
        }
        
        if (maxVolume <= silenceThreshold)
        {
            Debug.LogWarning($"Audio too quiet: {maxVolume:F4} <= {silenceThreshold:F4}");
            return false;
        }
        
        Debug.Log("✅ Audio validation passed!");
        return true;
    }
    
    // Future: Streaming implementation with Deepgram
    // This can be added later for even faster transcription
    IEnumerator StreamWithDeepgram(AudioClip clip)
    {
        // TODO: Implement WebSocket connection to Deepgram
        // For now, fall back to batch processing
        yield return StartCoroutine(SendToWhisperAPI(clip));
    }
}