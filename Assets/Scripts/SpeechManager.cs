using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public enum ConversationState
{
    Idle,           // No one in zone
    Ready,          // Person in zone, ready to record
    Recording,      // Currently recording
    Processing,     // Transcribing and getting LLM response
    Speaking        // God is responding
}

public class SpeechManager : MonoBehaviour
{
    [Header("Recording Settings")]
    public float maxRecordingTime = 5f; // Reduced from 10f for faster interactions
    public int sampleRate = 44100;
    public bool debugRecording = true;
    public int selectedMicrophoneIndex = 0;
    
    [Header("Silence Detection")]
    public bool autoStopOnSilence = true;
    public float silenceThreshold = 0.001f;
    public float silenceDuration = 1.5f; // Stop after 1.5s of silence
    
    [Header("VR Controller Input")]
    public XRNode controllerHand = XRNode.RightHand;
    public bool useTrigger = true;
    public bool useGrip = false;
    public bool usePrimaryButton = false;
    
    [Header("Audio Feedback")]
    public AudioClip[] welcomeSounds;
    public AudioClip[] thinkingSounds;
    [Range(0f, 1f)]
    public float bufferSoundVolume = 0.7f;
    public bool useBufferSounds = false;

    [Header("Audio Clip Loading")]
    public bool loadFromResources = true;
    public string welcomeSoundsFolder = "Audio/Welcome";
    public string thinkingSoundsFolder = "Audio/Thinking";
    
    [Header("Speech-to-Text")]
    public StreamingSpeechToTextManager speechToTextManager; // NEW: Streaming STT
    
    [Header("LLM Integration")]
    public LLMManager llmManager;
    
    [Header("Text-to-Speech")]
    public TextToSpeechManager textToSpeechManager;
    
    [Header("Optimization")]
    public bool usePredictiveTTS = true; // Warm up TTS while recording
    public float warmupTriggerPercent = 0.7f; // Start warmup at 70% of recording
    
    [Header("Debug - Keyboard Override")]
    public bool allowKeyboardTesting = true;
    
    // State management
    private ConversationState currentState = ConversationState.Idle;
    private bool isPersonInZone = false;
    
    // Recording variables
    private AudioClip recordedClip;
    private string microphoneDevice;
    private bool isRecording = false;
    public AudioSource mainAudioSource;
    public AudioSource bufferAudioSource;
    private bool microphonePermissionGranted = false;
    private bool isPlayingBufferSound = false;
    
    // VR Input
    private UnityEngine.XR.InputDevice targetController;
    private bool wasButtonPressed = false;
    private bool hasValidController = false;
    private float lastControllerCheckTime = 0f;
    
    // NEW: Audio cache for preloading
    private Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();
    private bool audioPreloaded = false;
    
    // NEW: Predictive TTS
    private bool ttsWarmedUp = false;

    void Start()
    {
        PreloadAudioClips(); // NEW: Preload all audio at startup
        StartCoroutine(InitializeSystem());
    }
    
    // NEW: Preload all audio clips into cache
    void PreloadAudioClips()
    {
        Debug.Log("🎵 Preloading audio clips...");
        
        if (loadFromResources)
        {
            LoadAudioClipsFromResources();
        }
        
        // Cache welcome sounds
        if (welcomeSounds != null)
        {
            foreach (var clip in welcomeSounds)
            {
                if (clip != null)
                {
                    audioCache[$"welcome_{clip.name}"] = clip;
                }
            }
        }
        
        // Cache thinking sounds
        if (thinkingSounds != null)
        {
            foreach (var clip in thinkingSounds)
            {
                if (clip != null)
                {
                    audioCache[$"thinking_{clip.name}"] = clip;
                }
            }
        }
        
        audioPreloaded = true;
        Debug.Log($"✅ Preloaded {audioCache.Count} audio clips");
    }

    void LoadAudioClipsFromResources()
    {
        if (!loadFromResources) return;
        
        Debug.Log("📂 Loading audio clips from Resources subfolders...");
        
        try
        {
            // Load welcome sounds
            AudioClip[] loadedWelcomeSounds = Resources.LoadAll<AudioClip>(welcomeSoundsFolder);
            if (loadedWelcomeSounds.Length > 0)
            {
                welcomeSounds = loadedWelcomeSounds;
                Debug.Log($"✅ Loaded {welcomeSounds.Length} welcome sounds");
            }
            
            // Load thinking sounds
            AudioClip[] loadedThinkingSounds = Resources.LoadAll<AudioClip>(thinkingSoundsFolder);
            if (loadedThinkingSounds.Length > 0)
            {
                thinkingSounds = loadedThinkingSounds;
                Debug.Log($"✅ Loaded {thinkingSounds.Length} thinking sounds");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error loading audio clips: {e.Message}");
        }
    }
    
    IEnumerator InitializeSystem()
    {
        yield return StartCoroutine(RequestMicrophonePermissionSilently());
        
        InitializeMicrophone();
        SetupAudio();
        SetupSpeechToText();
        InitializeVRInput();
        
        ChangeState(ConversationState.Idle);
        Debug.Log("✅ Optimized Speech Manager initialized (Slovene + Streaming)");
    }
    
    IEnumerator RequestMicrophonePermissionSilently()
    {
        if (Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            microphonePermissionGranted = true;
            Debug.Log("Microphone permission already granted");
            yield break;
        }
        
        Application.RequestUserAuthorization(UserAuthorization.Microphone);
        
        float timeout = 10f;
        float elapsed = 0f;
        
        while (!Application.HasUserAuthorization(UserAuthorization.Microphone) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        microphonePermissionGranted = Application.HasUserAuthorization(UserAuthorization.Microphone);
        Debug.Log(microphonePermissionGranted ? "Microphone permission granted" : "Microphone permission denied");
    }
    
    void InitializeMicrophone()
    {
        Debug.Log("Available microphones:");
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            Debug.Log($"  {i}: {Microphone.devices[i]}");
        }
        
        if (Microphone.devices.Length > 0)
        {
            if (selectedMicrophoneIndex < Microphone.devices.Length)
            {
                microphoneDevice = Microphone.devices[selectedMicrophoneIndex];
            }
            else
            {
                microphoneDevice = Microphone.devices[0];
                Debug.LogWarning($"Selected microphone index {selectedMicrophoneIndex} invalid, using device 0");
            }
            
            Debug.Log($"Selected microphone: {microphoneDevice}");
        }
        else
        {
            Debug.LogError("No microphone devices found!");
        }
    }

    void SetupAudio()
    {
        Transform lipSyncTransform = transform.Find("lipSync");
        if (lipSyncTransform != null)
        {
            AudioSource existingAudioSource = lipSyncTransform.GetComponent<AudioSource>();
            if (existingAudioSource != null)
            {
                mainAudioSource = existingAudioSource;
                Debug.Log("✅ Using existing AudioSource from lipSync child");
            }
            else
            {
                mainAudioSource = lipSyncTransform.gameObject.AddComponent<AudioSource>();
                Debug.Log("➕ Added AudioSource to existing lipSync child");
            }
        }
        else
        {
            AudioSource childAudioSource = GetComponentInChildren<AudioSource>();
            if (childAudioSource != null)
            {
                mainAudioSource = childAudioSource;
                Debug.Log($"✅ Using existing AudioSource from child: {childAudioSource.name}");
            }
            else
            {
                mainAudioSource = GetComponent<AudioSource>();
                if (mainAudioSource == null)
                {
                    mainAudioSource = gameObject.AddComponent<AudioSource>();
                    Debug.Log("➕ Created new AudioSource on main sculpture");
                }
            }
        }
        
        mainAudioSource.spatialBlend = 1.0f;
        mainAudioSource.volume = 0.8f;
        mainAudioSource.playOnAwake = false;
        mainAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        mainAudioSource.minDistance = 1f;
        mainAudioSource.maxDistance = 15f;
        
        if (useBufferSounds)
        {
            bufferAudioSource = mainAudioSource;
            Debug.Log("🔗 Buffer sounds will use the same AudioSource as main audio");
        }
        
        Debug.Log($"🎵 Audio setup complete - AudioSource on: {mainAudioSource.gameObject.name}");
    }

    void SetupSpeechToText()
    {
        // NEW: Use streaming STT manager
        if (speechToTextManager == null)
        {
            speechToTextManager = GetComponent<StreamingSpeechToTextManager>();
            if (speechToTextManager == null)
            {
                speechToTextManager = gameObject.AddComponent<StreamingSpeechToTextManager>();
                Debug.Log("➕ Added StreamingSpeechToTextManager");
            }
        }
        speechToTextManager.OnTranscriptionComplete += HandleTranscription;

        if (llmManager == null)
        {
            llmManager = GetComponent<LLMManager>();
            if (llmManager == null)
            {
                llmManager = gameObject.AddComponent<LLMManager>();
            }
        }
        llmManager.OnResponseReceived += HandleLLMResponse;

        if (textToSpeechManager == null)
        {
            textToSpeechManager = GetComponent<TextToSpeechManager>();
            if (textToSpeechManager == null)
            {
                textToSpeechManager = gameObject.AddComponent<TextToSpeechManager>();
            }
        }
        textToSpeechManager.OnSpeechComplete += HandleSpeechComplete;
    }
    
    void InitializeVRInput()
    {
        List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesAtXRNode(controllerHand, devices);
        
        if (devices.Count > 0)
        {
            targetController = devices[0];
            hasValidController = true;
            Debug.Log($"✅ Found VR controller: {targetController.name} on {controllerHand}");
        }
        else
        {
            hasValidController = false;
            Debug.LogWarning($"❌ No VR controller found on {controllerHand}. Will keep trying...");
        }
        
        lastControllerCheckTime = Time.time;
    }

    void Update()
    {
        if (!hasValidController && Time.time - lastControllerCheckTime > 2f)
        {
            InitializeVRInput();
        }

        EnsureProperStateWhenInZone();

        if (currentState == ConversationState.Ready || currentState == ConversationState.Recording)
        {
            HandleInput();
        }
    }
    
    void EnsureProperStateWhenInZone()
    {
        if (isPersonInZone && currentState == ConversationState.Idle)
        {
            Debug.LogWarning("🔄 Player in zone but state was Idle - forcing to Ready state");
            
            if (hasValidController)
            {
                ChangeState(ConversationState.Ready);
                Debug.Log("🎮 Controller found - ready for input");
            }
            else
            {
                InitializeVRInput();
                if (hasValidController)
                {
                    ChangeState(ConversationState.Ready);
                    Debug.Log("🎮 Controller reconnected - ready for input");
                }
            }
        }
    }
    
    void HandleInput()
    {
        bool buttonPressed = false;
        
        if (hasValidController && targetController.isValid)
        {
            if (useTrigger)
            {
                targetController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out buttonPressed);
            }
            else if (useGrip)
            {
                targetController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out buttonPressed);
            }
            else if (usePrimaryButton)
            {
                targetController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out buttonPressed);
            }
        }
        else if (hasValidController)
        {
            hasValidController = false;
            Debug.LogWarning("VR Controller connection lost");
        }
        
        if (allowKeyboardTesting)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                buttonPressed = buttonPressed || keyboard.spaceKey.isPressed;
            }
            
            var mouse = Mouse.current;
            if (mouse != null)
            {
                buttonPressed = buttonPressed || mouse.leftButton.isPressed;
            }
        }
        
        if (buttonPressed && !wasButtonPressed)
        {
            OnButtonPressed();
        }
        else if (!buttonPressed && wasButtonPressed)
        {
            OnButtonReleased();
        }
        
        wasButtonPressed = buttonPressed;
    }
    
    void OnButtonPressed()
    {
        if (currentState == ConversationState.Ready)
        {
            StartRecording();
        }
    }
    
    void OnButtonReleased()
    {
        if (currentState == ConversationState.Recording)
        {
            StopRecording();
        }
    }
    
    public void OnPlayerEnteredZone()
    {
        Debug.Log("=== PLAYER ENTERED CONVERSATION ZONE ===");
        isPersonInZone = true;
        
        if (currentState == ConversationState.Idle)
        {
            if (!hasValidController)
            {
                InitializeVRInput();
            }
            
            if (hasValidController)
            {
                ChangeState(ConversationState.Ready);
                StartCoroutine(DelayedWelcomeSound());
                Debug.Log($"🎮 Ready for VR input on {controllerHand} controller");
            }
            else
            {
                Debug.LogError("❌ No VR controller found - will keep trying...");
            }
        }
    }

    IEnumerator DelayedWelcomeSound()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (bufferAudioSource == null)
        {
            SetupAudio();
        }
        
        PlayWelcomeSound();
    }
    
    public void OnPlayerExitedZone()
    {
        Debug.Log("=== PLAYER LEFT CONVERSATION ZONE ===");
        isPersonInZone = false;
        
        if (isRecording)
        {
            ForceStopRecording();
        }
        StopBufferSound();
        
        ChangeState(ConversationState.Idle);
    }
    
    void ChangeState(ConversationState newState)
    {
        Debug.Log($"State changed: {currentState} → {newState}");
        currentState = newState;
    }
    
    void StartRecording()
    {
        if (!microphonePermissionGranted || string.IsNullOrEmpty(microphoneDevice))
        {
            Debug.LogError("Cannot start recording - microphone not ready");
            return;
        }
        
        if (isRecording) return;
        
        Debug.Log("🎤 Starting recording...");
        
        if (recordedClip != null)
        {
            DestroyImmediate(recordedClip);
            recordedClip = null;
        }
        
        recordedClip = Microphone.Start(microphoneDevice, false, (int)maxRecordingTime, sampleRate);
        
        if (recordedClip != null)
        {
            isRecording = true;
            ChangeState(ConversationState.Recording);
            
            // NEW: Start predictive TTS warmup
            if (usePredictiveTTS)
            {
                StartCoroutine(PredictiveTTSWarmup());
            }
            
            // Start timeout and silence detection
            StartCoroutine(RecordingTimeoutCoroutine());
            
            if (autoStopOnSilence)
            {
                StartCoroutine(MonitorSilence());
            }
        }
        else
        {
            Debug.LogError("Failed to start microphone recording");
        }
    }
    
    // NEW: Predictive TTS - warm up connection while user is talking
    IEnumerator PredictiveTTSWarmup()
    {
        float warmupTime = maxRecordingTime * warmupTriggerPercent;
        yield return new WaitForSeconds(warmupTime);
        
        if (isRecording && !ttsWarmedUp && textToSpeechManager != null)
        {
            Debug.Log("🔥 Warming up TTS connection (predictive)");
            textToSpeechManager.WarmUpConnection();
            ttsWarmedUp = true;
        }
    }
    
    // NEW: Auto-stop on silence detection
    IEnumerator MonitorSilence()
    {
        float silenceTimer = 0f;
        
        while (isRecording)
        {
            float volume = GetCurrentMicVolume();
            
            if (volume < silenceThreshold)
            {
                silenceTimer += Time.deltaTime;
                if (silenceTimer >= silenceDuration)
                {
                    Debug.Log("🔇 Auto-stopping on silence");
                    StopRecording();
                    yield break;
                }
            }
            else
            {
                silenceTimer = 0f;
            }
            
            yield return null;
        }
    }
    
    float GetCurrentMicVolume()
    {
        if (recordedClip == null) return 0f;
        
        int micPosition = Microphone.GetPosition(microphoneDevice);
        if (micPosition <= 0) return 0f;
        
        // Sample last 1024 samples
        int sampleWindow = Mathf.Min(1024, micPosition);
        float[] samples = new float[sampleWindow];
        recordedClip.GetData(samples, Mathf.Max(0, micPosition - sampleWindow));
        
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }
        
        return sum / samples.Length;
    }
    
    void StopRecording()
    {
        if (!isRecording) return;
        
        Debug.Log("🛑 Stopping recording...");
        
        int micPosition = Microphone.GetPosition(microphoneDevice);
        Microphone.End(microphoneDevice);
        isRecording = false;
        ttsWarmedUp = false; // Reset for next recording
        
        if (recordedClip != null && micPosition > 0)
        {
            AudioClip trimmedClip = AudioClip.Create("TrimmedRecording", micPosition, recordedClip.channels, recordedClip.frequency, false);
            float[] samples = new float[micPosition * recordedClip.channels];
            recordedClip.GetData(samples, 0);
            trimmedClip.SetData(samples, 0);
            
            DestroyImmediate(recordedClip);
            recordedClip = trimmedClip;
            
            Debug.Log($"📼 Recorded {recordedClip.length:F2} seconds of audio");
            
            ChangeState(ConversationState.Processing);
            ProcessRecordedSpeech();
        }
        else
        {
            Debug.LogWarning("No audio recorded");
            ChangeState(ConversationState.Ready);
        }
    }
    
    void ForceStopRecording()
    {
        if (isRecording)
        {
            Microphone.End(microphoneDevice);
            isRecording = false;
            ttsWarmedUp = false;
            
            if (recordedClip != null)
            {
                DestroyImmediate(recordedClip);
                recordedClip = null;
            }
        }
    }
    
    IEnumerator RecordingTimeoutCoroutine()
    {
        yield return new WaitForSeconds(maxRecordingTime);
        
        if (isRecording)
        {
            Debug.Log("⏱️ Recording timed out");
            StopRecording();
        }
    }
    
    void ProcessRecordedSpeech()
    {
        if (recordedClip == null)
        {
            Debug.LogError("No speech to process!");
            ChangeState(ConversationState.Ready);
            return;
        }
        
        float[] samples = new float[recordedClip.samples * recordedClip.channels];
        recordedClip.GetData(samples, 0);
        
        float maxVolume = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            maxVolume = Mathf.Max(maxVolume, Mathf.Abs(samples[i]));
        }
        
        if (maxVolume < 0.0001f)
        {
            Debug.LogWarning("No meaningful audio detected");
            ChangeState(ConversationState.Ready);
            return;
        }
        
        Debug.Log("📤 Sending speech for transcription (Slovene)...");
        PlayThinkingSound();
        
        if (speechToTextManager != null)
        {
            speechToTextManager.TranscribeAudio(recordedClip);
        }
        else
        {
            Debug.LogError("SpeechToTextManager not found!");
            ChangeState(ConversationState.Ready);
        }
    }
    
    void HandleTranscription(string transcribedText)
    {
        Debug.Log($"📝 Transkripcija (SL): \"{transcribedText}\"");
        
        if (string.IsNullOrEmpty(transcribedText) || string.IsNullOrWhiteSpace(transcribedText))
        {
            Debug.LogWarning("Empty transcription");
            ChangeState(ConversationState.Ready);
            return;
        }
        
        if (llmManager != null)
        {
            // LLM will process in parallel with thinking sound
            llmManager.GetResponse(transcribedText);
        }
        else
        {
            Debug.LogError("LLMManager not found!");
            ChangeState(ConversationState.Ready);
        }
    }

    void HandleLLMResponse(string aiResponse)
    {
        Debug.Log($"🗿 Bog odgovarja (SL): \"{aiResponse}\"");
        
        // Wait for buffer sound to finish before starting TTS
        StartCoroutine(WaitForBufferThenSpeak(aiResponse));
    }

    IEnumerator WaitForBufferThenSpeak(string response)
    {
        while (isPlayingBufferSound)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        StopBufferSound();
        ChangeState(ConversationState.Speaking);
        
        if (textToSpeechManager != null)
        {
            textToSpeechManager.SpeakText(response);
        }
        else
        {
            Debug.LogError("TextToSpeechManager not found!");
            ChangeState(ConversationState.Ready);
        }
    }
    
    void HandleSpeechComplete()
    {
        Debug.Log("✅ God finished speaking - ready for next question");
        StopBufferSound();
        ChangeState(ConversationState.Ready);
    }
    
    // Buffer Sound Methods - Using preloaded clips
    void PlayWelcomeSound()
    {
        if (!useBufferSounds || welcomeSounds == null || welcomeSounds.Length == 0) return;
        
        AudioClip welcomeClip = welcomeSounds[Random.Range(0, welcomeSounds.Length)];
        PlayBufferSound(welcomeClip);
    }
    
    void PlayThinkingSound()
    {
        if (!useBufferSounds || thinkingSounds == null || thinkingSounds.Length == 0) return;
        
        AudioClip thinkingClip = thinkingSounds[Random.Range(0, thinkingSounds.Length)];
        PlayBufferSound(thinkingClip);
    }
    
    void PlayBufferSound(AudioClip clip)
    {
        if (!useBufferSounds || clip == null || bufferAudioSource == null) return;
        
        StopBufferSound();
        
        bufferAudioSource.clip = clip;
        bufferAudioSource.volume = bufferSoundVolume;
        bufferAudioSource.Play();
        isPlayingBufferSound = true;
        
        StartCoroutine(StopBufferSoundWhenFinished(clip.length));
    }
    
    void StopBufferSound()
    {
        if (bufferAudioSource == null || !isPlayingBufferSound) return;
        bufferAudioSource.Stop();
        isPlayingBufferSound = false;
    }
    
    IEnumerator StopBufferSoundWhenFinished(float clipLength)
    {
        yield return new WaitForSeconds(clipLength);
        if (isPlayingBufferSound)
        {
            isPlayingBufferSound = false;
        }
    }
    
    void OnDestroy()
    {
        StopBufferSound();
        ForceStopRecording();
        
        if (recordedClip != null)
        {
            DestroyImmediate(recordedClip);
        }
        
        if (speechToTextManager != null)
            speechToTextManager.OnTranscriptionComplete -= HandleTranscription;
        if (llmManager != null)
            llmManager.OnResponseReceived -= HandleLLMResponse;
        if (textToSpeechManager != null)
            textToSpeechManager.OnSpeechComplete -= HandleSpeechComplete;
    }
    
    // Public methods
    public ConversationState GetCurrentState() => currentState;
    public bool IsInConversation() => isPersonInZone && currentState != ConversationState.Idle;
    public bool HasValidInput() => hasValidController;
    public string GetInputStatus() => hasValidController ? $"VR Controller ({controllerHand})" : "Searching for VR Controller...";
}