using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;

[System.Serializable]
public class TTSRequest
{
    public string model;
    public string input;
    public string voice;
    public float speed;
    
    public TTSRequest(string model, string input, string voice, float speed)
    {
        this.model = model;
        this.input = input;
        this.voice = voice;
        this.speed = speed;
    }
}

public class TextToSpeechManager : MonoBehaviour
{
    [Header("TTS Settings (Slovene)")]
    public string openAIApiKey = "";
    public string voice = "alloy"; // Best for Slovene: alloy or shimmer
    public string model = "tts-1"; // tts-1 or tts-1-hd
    public float speed = 0.85f; // Slightly slower for Slovene clarity (0.25 to 4.0)
    public bool debugTTS = true;
    
    [Header("Audio Playback")]
    public AudioSource audioSource;
    public float volume = 0.8f;
    
    [Header("Optimization")]
    public bool useStreaming = false; // Future: stream audio as it generates
    public bool allowConnectionWarmup = true; // Predictive connection warming
    
    private const string TTS_API_URL = "https://api.openai.com/v1/audio/speech";
    
    public delegate void SpeechComplete();
    public event SpeechComplete OnSpeechComplete;
    
    // NEW: Connection warming
    private bool isWarmedUp = false;
    private float lastWarmupTime = 0f;
    private const float WARMUP_COOLDOWN = 30f; // Re-warm after 30s
    
    void Start()
    {
        SetupAudioSource();
        
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            openAIApiKey = EnvironmentLoader.GetEnvironmentVariable("OPENAI_API_KEY");
        }
        
        if (debugTTS)
        {
            Debug.Log("🎤 TTS Manager initialized (Slovene)");
            Debug.Log($"   Voice: {voice}");
            Debug.Log($"   Speed: {speed}");
            Debug.Log($"   Model: {model}");
        }
    }
    
    void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 15f;
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
    }
    
    public void SpeakText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogError("No text provided for TTS!");
            return;
        }
        
        if (string.IsNullOrEmpty(openAIApiKey) || openAIApiKey.StartsWith("your-api"))
        {
            Debug.LogWarning("Using fallback TTS (no API key)");
            StartCoroutine(FallbackSpeech(text));
            return;
        }
        
        if (debugTTS)
            Debug.Log($"🗣️ Converting to speech (SL): \"{text}\"");
        
        StartCoroutine(SendToTTSAPI(text));
    }
    
    // NEW: Warm up API connection (called predictively while user is recording)
    public void WarmUpConnection()
    {
        if (!allowConnectionWarmup) return;
        
        // Don't warm up too frequently
        if (Time.time - lastWarmupTime < WARMUP_COOLDOWN)
        {
            if (debugTTS)
                Debug.Log("🔥 TTS already warmed up recently");
            return;
        }
        
        if (debugTTS)
            Debug.Log("🔥 Warming up TTS connection...");
        
        StartCoroutine(WarmupCoroutine());
    }
    
    IEnumerator WarmupCoroutine()
    {
        // Send a minimal request to wake up the API
        string silentText = "."; // Minimal text
        
        TTSRequest requestData = new TTSRequest(model, silentText, voice, speed);
        string jsonRequest = JsonUtility.ToJson(requestData);
        
        UnityWebRequest webRequest = new UnityWebRequest(TTS_API_URL, "POST");
        byte[] jsonToSend = Encoding.UTF8.GetBytes(jsonRequest);
        webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);
        
        // Set a short timeout for warmup
        webRequest.timeout = 5;
        
        yield return webRequest.SendWebRequest();
        
        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            isWarmedUp = true;
            lastWarmupTime = Time.time;
            if (debugTTS)
                Debug.Log("✅ TTS connection warmed up successfully");
        }
        else
        {
            if (debugTTS)
                Debug.Log("⚠️ TTS warmup failed (not critical)");
        }
        
        webRequest.Dispose();
    }
    
    IEnumerator SendToTTSAPI(string text)
    {
        TTSRequest requestData = new TTSRequest(model, text, voice, speed);
        string jsonRequest = JsonUtility.ToJson(requestData);
        
        if (debugTTS)
        {
            Debug.Log($"📤 TTS Request:");
            Debug.Log($"   Model: {model}");
            Debug.Log($"   Voice: {voice}");
            Debug.Log($"   Speed: {speed}");
            Debug.Log($"   Text length: {text.Length} chars");
        }
        
        UnityWebRequest webRequest = new UnityWebRequest(TTS_API_URL, "POST");
        byte[] jsonToSend = Encoding.UTF8.GetBytes(jsonRequest);
        webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);
        
        if (debugTTS)
            Debug.Log("📡 Sending request to OpenAI TTS API...");
        
        yield return webRequest.SendWebRequest();
        
        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            byte[] audioData = webRequest.downloadHandler.data;
            
            if (audioData != null && audioData.Length > 0)
            {
                if (debugTTS)
                    Debug.Log($"✅ Received audio data: {audioData.Length / 1024f:F1} KB");
                
                yield return StartCoroutine(PlayAudioData(audioData));
            }
            else
            {
                Debug.LogError("Received empty audio data from TTS API");
                yield return StartCoroutine(FallbackSpeech(text));
            }
        }
        else
        {
            Debug.LogError($"TTS API request failed: {webRequest.error}");
            Debug.LogError($"Response code: {webRequest.responseCode}");
            yield return StartCoroutine(FallbackSpeech(text));
        }
        
        webRequest.Dispose();
    }
    
    IEnumerator PlayAudioData(byte[] audioData)
    {
        string tempPath = System.IO.Path.Combine(Application.persistentDataPath, "tts_temp.mp3");
        bool success = false;
        
        // Save audio data to file
        try
        {
            System.IO.File.WriteAllBytes(tempPath, audioData);
            if (debugTTS)
                Debug.Log($"💾 Saved TTS audio to: {tempPath}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save TTS audio: " + e.Message);
            StartCoroutine(FallbackSpeech("File save error"));
            yield break;
        }
        
        // Load and play audio
        string fileURL = "file://" + tempPath;
        UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileURL, AudioType.MPEG);
        
        if (debugTTS)
            Debug.Log("🔊 Loading audio clip from file...");
        
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            
            if (clip != null)
            {
                if (debugTTS)
                    Debug.Log($"▶️ Playing TTS audio - Length: {clip.length:F2}s");
                
                // Play audio
                audioSource.clip = clip;
                audioSource.Play();
                
                // Handle lip sync if OVRLipSync is present
                OVRLipSyncContext ovrContext = FindObjectOfType<OVRLipSyncContext>();
                if (ovrContext != null)
                {
                    float[] samples = new float[clip.samples * clip.channels];
                    clip.GetData(samples, 0);
                    StartCoroutine(SyncedLipSync(samples, clip.frequency, ovrContext));
                }
                
                // Wait for playback to complete
                yield return new WaitForSeconds(clip.length + 0.5f);
                
                success = true;
                OnSpeechComplete?.Invoke();
            }
            else
            {
                Debug.LogError("Failed to create AudioClip from TTS data");
                StartCoroutine(FallbackSpeech("Audio conversion failed"));
            }
        }
        else
        {
            Debug.LogError("Failed to load TTS audio: " + www.error);
            StartCoroutine(FallbackSpeech("Audio loading failed"));
        }
        
        www.Dispose();
        
        // Cleanup temp file
        if (System.IO.File.Exists(tempPath))
        {
            try
            {
                System.IO.File.Delete(tempPath);
                if (debugTTS)
                    Debug.Log("🗑️ Cleaned up temporary audio file");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not delete temp file: " + e.Message);
            }
        }
        
        if (!success)
        {
            OnSpeechComplete?.Invoke();
        }
    }
    
    IEnumerator SyncedLipSync(float[] samples, int frequency, OVRLipSyncContext ovrContext)
    {
        int samplesPerFrame = frequency / 100; // 100 FPS
        int currentIndex = 0;
        
        while (audioSource.isPlaying && currentIndex < samples.Length)
        {
            int remainingSamples = Mathf.Min(samplesPerFrame, samples.Length - currentIndex);
            float[] chunk = new float[remainingSamples];
            System.Array.Copy(samples, currentIndex, chunk, 0, remainingSamples);
            
            ovrContext.ProcessAudioSamples(chunk, frequency);
            
            currentIndex += samplesPerFrame;
            yield return new WaitForSeconds(0.01f);
        }
        
        if (debugTTS)
            Debug.Log("👄 Lip sync completed");
    }
    
    IEnumerator FallbackSpeech(string text)
    {
        Debug.Log("=== FALLBACK TTS (Slovene) ===");
        Debug.Log($"🗿 SKULPTURA GOVORI: \"{text}\"");
        
        PlaySimpleBeep();
        
        float speechDuration = Mathf.Max(2f, text.Length * 0.08f);
        yield return new WaitForSeconds(speechDuration);
        
        if (debugTTS)
            Debug.Log("✅ Fallback speech completed");
        
        OnSpeechComplete?.Invoke();
    }
    
    void PlaySimpleBeep()
    {
        AudioClip beep = CreateBeepSound(200f, 0.8f);
        audioSource.PlayOneShot(beep, 0.3f);
    }
    
    AudioClip CreateBeepSound(float frequency, float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        
        AudioClip clip = AudioClip.Create("SculptureBeep", samples, 1, sampleRate, false);
        float[] data = new float[samples];
        
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float amplitude = Mathf.Sin(t * Mathf.PI);
            data[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / sampleRate) * amplitude * 0.2f;
        }
        
        clip.SetData(data, 0);
        return clip;
    }
    
    public void StopSpeaking()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            if (debugTTS)
                Debug.Log("🛑 TTS playback stopped");
        }
    }
    
    public bool IsSpeaking() => audioSource.isPlaying;
    
    [ContextMenu("Test TTS (Slovene)")]
    public void TestTTS()
    {
        SpeakText("Pozdravljen, smrtnik. To je preizkus sistema za pretvorbo besedila v govor v slovenščini.");
    }
    
    [ContextMenu("Test TTS Warmup")]
    public void TestWarmup()
    {
        WarmUpConnection();
    }
    
    public void SetAPIKey(string newAPIKey)
    {
        openAIApiKey = newAPIKey;
        Debug.Log("TTS API key updated");
    }
}