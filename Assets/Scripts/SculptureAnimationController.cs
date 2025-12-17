using UnityEngine;
using System.Collections;

public class SculptureAnimationController : MonoBehaviour
{
    [Header("Animation Control")]
    public Animator sculptureAnimator;
    public bool debugAnimations = true;
    
    [Header("Connected Systems")]
    public SpeechManager speechManager;
    public SculptureInteraction sculptureInteraction;
    public LLMManager llmManager;
    
    [Header("Blend Shape Integration")]
    public SkinnedMeshRenderer faceRenderer;
    public string eyeBlinkBlendShapeName = "Eye_Blink";
    public int eyeBlinkBlendShapeIndex = -1; // Auto-detected or manually set
    [Range(2f, 8f)]
    public float minBlinkInterval = 2f;
    [Range(4f, 10f)]
    public float maxBlinkInterval = 6f;
    [Range(5f, 15f)]
    public float blinkSpeed = 8f;
    public bool enableBlinking = true;
    
    [Header("uLipSync Integration")]
    public uLipSync.uLipSync uLipSyncComponent;
    public bool useULipSyncBlink = false; // Set to true if you want to integrate with uLipSync
    public bool useLateUpdateOverride = true; // Force blink in LateUpdate to override uLipSync
    
    [Header("Animation Settings")]
    [Range(0.1f, 3f)]
    public float animationSpeed = 1f;
    [Range(0f, 1f)]
    public float emotionIntensity = 0.5f;
    
    // Animation parameter names (matching your Animator Controller exactly)
    private readonly string PARAM_IS_PLAYER_NEAR = "IsPlayerNear";
    private readonly string PARAM_IS_RECORDING = "IsRecording";  
    private readonly string PARAM_IS_THINKING = "IsThinking";
    private readonly string PARAM_IS_SPEAKING = "IsSpeaking";
    private readonly string PARAM_WELCOME = "Welcome";
    
    // Additional parameters you might want to add later
    private readonly string PARAM_EMOTION = "Emotion";
    private readonly string PARAM_BLINK_WEIGHT = "BlinkWeight";
    private readonly string PARAM_MOUTH_OPEN = "MouthOpen";
    
    // State tracking
    private ConversationState currentState = ConversationState.Idle;
    private ConversationState previousState = ConversationState.Idle;
    private bool wasPlayerNear = false;
    private float currentEmotion = 0.5f;
    
    // Blinking system
    private Coroutine blinkingCoroutine;
    private bool isBlinking = false;
    private float nextBlinkTime;
    private float blinkOverrideWeight = -1f; // -1 means no override, used for LateUpdate method
    
    void Start()
    {
        InitializeComponents();
        SetupAnimationEvents();
    }
    
    void InitializeComponents()
    {
        // Auto-find animator if not assigned
        if (sculptureAnimator == null)
        {
            sculptureAnimator = GetComponent<Animator>();
            if (sculptureAnimator == null)
            {
                sculptureAnimator = GetComponentInChildren<Animator>();
            }
        }
        
        // Auto-find face renderer for blend shapes
        if (faceRenderer == null)
        {
            faceRenderer = GetComponent<SkinnedMeshRenderer>();
            if (faceRenderer == null)
            {
                faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }
        }
        
        // Auto-find uLipSync component
        if (uLipSyncComponent == null)
        {
            uLipSyncComponent = GetComponentInChildren<uLipSync.uLipSync>();
        }
        
        // Initialize blend shape system
        InitializeBlendShapes();
        
        // Auto-find speech manager
        if (speechManager == null)
        {
            speechManager = FindObjectOfType<SpeechManager>();
        }
        
        // Auto-find sculpture interaction
        if (sculptureInteraction == null)
        {
            sculptureInteraction = FindObjectOfType<SculptureInteraction>();
        }
        
        // Auto-find LLM manager
        if (llmManager == null)
        {
            llmManager = FindObjectOfType<LLMManager>();
        }
        
        if (sculptureAnimator == null)
        {
            Debug.LogError("⚠️ No Animator found! Please assign an Animator with the SculptureAnimatorController.");
            enabled = false;
            return;
        }
        
        // Initialize animator parameters
        ResetAnimatorParameters();
        sculptureAnimator.speed = animationSpeed;
        
        if (debugAnimations)
        {
            Debug.Log("🎭 Animation Integration initialized");
            Debug.Log($"   Animator: {sculptureAnimator.name}");
            Debug.Log($"   Controller: {(sculptureAnimator.runtimeAnimatorController ? sculptureAnimator.runtimeAnimatorController.name : "None")}");
            Debug.Log($"   Face Renderer: {(faceRenderer ? faceRenderer.name : "None")}");
            Debug.Log($"   Eye Blink Index: {eyeBlinkBlendShapeIndex}");
            Debug.Log($"   uLipSync Component: {(uLipSyncComponent ? uLipSyncComponent.name : "None")}");
            Debug.Log($"   LateUpdate Override: {useLateUpdateOverride}");
        }
    }
    
    void SetupAnimationEvents()
    {
        // Subscribe to speech manager events if available
        if (speechManager != null)
        {
            // We'll monitor state changes in Update instead of events for simplicity
        }
        
        if (debugAnimations)
            Debug.Log("✅ Animation events set up");
    }
    
    void Update()
    {
        if (sculptureAnimator == null) return;
        
        UpdateConversationState();
        UpdatePlayerProximity();
        UpdateEmotionalState();
        UpdateProceduralAnimations();
        UpdateBlinking();
    }
    
    void LateUpdate()
    {
        // This runs after uLipSync has updated blend shapes
        if (useLateUpdateOverride && isBlinking && eyeBlinkBlendShapeIndex >= 0 && blinkOverrideWeight >= 0)
        {
            // Force our blink value even if uLipSync tried to override it
            faceRenderer.SetBlendShapeWeight(eyeBlinkBlendShapeIndex, blinkOverrideWeight);
        }
    }
    
    void UpdateConversationState()
    {
        if (speechManager == null) return;
        
        currentState = speechManager.GetCurrentState();
        
        // Handle state changes
        if (currentState != previousState)
        {
            HandleStateTransition(previousState, currentState);
            previousState = currentState;
        }
        
        // Update boolean parameters
        sculptureAnimator.SetBool(PARAM_IS_RECORDING, currentState == ConversationState.Recording);
        sculptureAnimator.SetBool(PARAM_IS_THINKING, currentState == ConversationState.Processing);
        sculptureAnimator.SetBool(PARAM_IS_SPEAKING, currentState == ConversationState.Speaking);
    }
    
    void HandleStateTransition(ConversationState from, ConversationState to)
    {
        if (debugAnimations)
            Debug.Log($"🎭 State transition: {from} → {to}");
        
        switch (to)
        {
            case ConversationState.Idle:
                SetEmotion(0.2f);
                break;
                
            case ConversationState.Ready:
                SetEmotion(0.4f);
                break;
                
            case ConversationState.Recording:
                SetEmotion(0.6f);
                if (debugAnimations) Debug.Log("🎤 Started recording - attentive pose");
                break;
                
            case ConversationState.Processing:
                SetEmotion(0.8f);
                if (debugAnimations) Debug.Log("🤔 Processing - thinking animation");
                break;
                
            case ConversationState.Speaking:
                SetEmotion(1.0f);
                if (debugAnimations) Debug.Log("🗣️ Speaking - animated response");
                StartSpeakingAnimation();
                break;
        }
    }
    
    void UpdatePlayerProximity()
    {
        if (sculptureInteraction == null) return;
        
        bool playerNear = sculptureInteraction.IsPlayerInZone();
        
        if (playerNear != wasPlayerNear)
        {
            sculptureAnimator.SetBool(PARAM_IS_PLAYER_NEAR, playerNear);
            
            if (playerNear)
            {
                TriggerWelcomeAnimation();
                if (debugAnimations)
                    Debug.Log("🚶‍♂️ Player entered zone - triggering Welcome animation");
            }
            else
            {
                // Player left - will transition through your Exit state
                if (debugAnimations)
                    Debug.Log("🚶‍♀️ Player left zone - transitioning to Exit");
            }
            
            wasPlayerNear = playerNear;
        }
    }
    
    void UpdateEmotionalState()
    {
        // Smoothly transition emotion (only if parameter exists)
        if (HasParameter(PARAM_EMOTION))
        {
            float targetEmotion = CalculateTargetEmotion();
            currentEmotion = Mathf.Lerp(currentEmotion, targetEmotion, Time.deltaTime * 2f);
            sculptureAnimator.SetFloat(PARAM_EMOTION, currentEmotion);
        }
    }
    
    float CalculateTargetEmotion()
    {
        // Base emotion on conversation state and player presence
        float baseEmotion = 0.3f;
        
        if (sculptureInteraction != null && sculptureInteraction.IsPlayerInZone())
        {
            baseEmotion = 0.5f; // More animated when someone is present
        }
        
        // Modify based on conversation state
        switch (currentState)
        {
            case ConversationState.Recording:
                return baseEmotion + 0.2f; // Attentive
            case ConversationState.Processing:
                return baseEmotion + 0.4f; // Thoughtful
            case ConversationState.Speaking:
                return baseEmotion + 0.5f; // Expressive
            default:
                return baseEmotion;
        }
    }
    
    void UpdateProceduralAnimations()
    {
        // Update procedural mouth movement during speaking (if you have this parameter)
        if (HasParameter(PARAM_MOUTH_OPEN) && currentState == ConversationState.Speaking)
        {
            // Simple mouth animation - you can make this more sophisticated
            float mouthMovement = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
            mouthMovement *= 0.4f; // Scale down
            sculptureAnimator.SetFloat(PARAM_MOUTH_OPEN, mouthMovement);
        }
        else if (HasParameter(PARAM_MOUTH_OPEN))
        {
            sculptureAnimator.SetFloat(PARAM_MOUTH_OPEN, 0f);
        }
        
        // You can add more procedural animations here
        // For example: breathing, subtle movements, etc.
    }
    
    // === BLINKING SYSTEM ===
    
    void InitializeBlendShapes()
    {
        if (faceRenderer == null)
        {
            if (debugAnimations)
                Debug.LogWarning("⚠️ No SkinnedMeshRenderer found for blend shapes");
            return;
        }
        
        if (faceRenderer.sharedMesh == null)
        {
            Debug.LogWarning("⚠️ SkinnedMeshRenderer has no mesh assigned");
            return;
        }
        
        // Try to find blend shape by name first
        if (!string.IsNullOrEmpty(eyeBlinkBlendShapeName))
        {
            eyeBlinkBlendShapeIndex = faceRenderer.sharedMesh.GetBlendShapeIndex(eyeBlinkBlendShapeName);
        }
        
        // Validate index
        if (eyeBlinkBlendShapeIndex < 0 || eyeBlinkBlendShapeIndex >= faceRenderer.sharedMesh.blendShapeCount)
        {
            if (debugAnimations)
            {
                Debug.LogWarning($"⚠️ Eye blink blend shape '{eyeBlinkBlendShapeName}' not found!");
                Debug.Log("Available blend shapes:");
                for (int i = 0; i < faceRenderer.sharedMesh.blendShapeCount; i++)
                {
                    Debug.Log($"  {i}: {faceRenderer.sharedMesh.GetBlendShapeName(i)}");
                }
            }
            eyeBlinkBlendShapeIndex = -1;
            enableBlinking = false;
            return;
        }
        
        // Start blinking system
        if (enableBlinking)
        {
            StartBlinking();
            if (debugAnimations)
            {
                string method = "direct control";
                if (useULipSyncBlink && uLipSyncComponent != null)
                    method = "uLipSync integration";
                else if (useLateUpdateOverride)
                    method = "LateUpdate override";
                    
                Debug.Log($"👁️ Eye blinking initialized using {method} - '{faceRenderer.sharedMesh.GetBlendShapeName(eyeBlinkBlendShapeIndex)}' (index {eyeBlinkBlendShapeIndex})");
            }
        }
    }
    
    void StartBlinking()
    {
        if (blinkingCoroutine != null)
        {
            StopCoroutine(blinkingCoroutine);
        }
        
        blinkingCoroutine = StartCoroutine(BlinkingLoop());
        nextBlinkTime = Time.time + Random.Range(minBlinkInterval, maxBlinkInterval);
    }
    
    void StopBlinking()
    {
        if (blinkingCoroutine != null)
        {
            StopCoroutine(blinkingCoroutine);
            blinkingCoroutine = null;
        }
        
        // Reset eyes to open
        if (eyeBlinkBlendShapeIndex >= 0 && faceRenderer != null)
        {
            faceRenderer.SetBlendShapeWeight(eyeBlinkBlendShapeIndex, 0);
            blinkOverrideWeight = -1f;
        }
    }
    
    void UpdateBlinking()
    {
        // Check if we need to start/stop blinking based on enable flag
        if (enableBlinking && blinkingCoroutine == null && eyeBlinkBlendShapeIndex >= 0)
        {
            StartBlinking();
        }
        else if (!enableBlinking && blinkingCoroutine != null)
        {
            StopBlinking();
        }
    }
    
    IEnumerator BlinkingLoop()
    {
        while (enableBlinking && eyeBlinkBlendShapeIndex >= 0)
        {
            // Wait for next blink time
            float waitTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            yield return new WaitForSeconds(waitTime);
            
            // Perform blink
            yield return StartCoroutine(DoBlink());
        }
    }
    
    IEnumerator DoBlink()
    {
        if (eyeBlinkBlendShapeIndex < 0 || faceRenderer == null) yield break;
        
        isBlinking = true;
        
        if (useULipSyncBlink && uLipSyncComponent != null)
        {
            // Use uLipSync's blend shape override system
            yield return StartCoroutine(DoULipSyncBlink());
        }
        else if (useLateUpdateOverride)
        {
            // Use LateUpdate override method to fight uLipSync
            yield return StartCoroutine(DoLateUpdateBlink());
        }
        else
        {
            // Direct blend shape control (original method)
            yield return StartCoroutine(DoDirectBlink());
        }
        
        isBlinking = false;
        blinkOverrideWeight = -1f; // Stop overriding
        
        // Update animator parameter if it exists
        if (HasParameter(PARAM_BLINK_WEIGHT))
        {
            sculptureAnimator.SetFloat(PARAM_BLINK_WEIGHT, 0f);
        }
    }
    
    IEnumerator DoULipSyncBlink()
    {
        // Get the uLipSync blend shape controller
        var blendShape = uLipSyncComponent.GetComponent<uLipSync.uLipSyncBlendShape>();
        if (blendShape != null)
        {
            // Animate blink using uLipSync's system
            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * blinkSpeed;
                float blinkAmount = Mathf.Sin(t * Mathf.PI); // 0 to 1 and back to 0
                
                // Set blink value through uLipSync (this varies by uLipSync version)
                if (blendShape.blendShapes != null)
                {
                    // Find the blink blend shape in uLipSync's list
                    for (int i = 0; i < blendShape.blendShapes.Count; i++)
                    {
                        if (blendShape.blendShapes[i].index == eyeBlinkBlendShapeIndex)
                        {
                            blendShape.blendShapes[i].weight = blinkAmount * 100f;
                            break;
                        }
                    }
                }
                
                yield return null;
            }
            
            // Reset blink
            if (blendShape.blendShapes != null)
            {
                for (int i = 0; i < blendShape.blendShapes.Count; i++)
                {
                    if (blendShape.blendShapes[i].index == eyeBlinkBlendShapeIndex)
                    {
                        blendShape.blendShapes[i].weight = 0f;
                        break;
                    }
                }
            }
        }
        else
        {
            // Fallback to LateUpdate method
            yield return StartCoroutine(DoLateUpdateBlink());
        }
    }
    
    IEnumerator DoLateUpdateBlink()
    {
        // Use override weight that gets applied in LateUpdate
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * blinkSpeed;
            blinkOverrideWeight = Mathf.Sin(t * Mathf.PI) * 100f;
            yield return null;
        }
        
        blinkOverrideWeight = 0f;
        yield return new WaitForSeconds(0.1f);
        // blinkOverrideWeight will be reset to -1 in DoBlink()
    }
    
    IEnumerator DoDirectBlink()
    {
        // Original direct blend shape control
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * blinkSpeed;
            float blinkAmount = Mathf.Sin(t * Mathf.PI) * 100f;
            faceRenderer.SetBlendShapeWeight(eyeBlinkBlendShapeIndex, blinkAmount);
            yield return null;
        }
        
        // Ensure eyes are fully open
        faceRenderer.SetBlendShapeWeight(eyeBlinkBlendShapeIndex, 0);
    }
    
    void StartSpeakingAnimation()
    {
        // This gets called when the sculpture starts speaking
        // You can add additional speaking-specific animations here
        StartCoroutine(SpeakingAnimationCoroutine());
    }
    
    IEnumerator SpeakingAnimationCoroutine()
    {
        // Add some variation to speaking animations
        while (currentState == ConversationState.Speaking)
        {
            // Random subtle animation variations
            float randomGesture = Random.Range(0.8f, 1.0f);
            SetEmotion(randomGesture);
            
            yield return new WaitForSeconds(Random.Range(1f, 3f));
        }
    }
    
    // Public methods for external control
    public void SetBlinkingEnabled(bool enabled)
    {
        enableBlinking = enabled;
    }
    
    public void TriggerManualBlink()
    {
        if (eyeBlinkBlendShapeIndex >= 0 && !isBlinking)
        {
            StartCoroutine(DoBlink());
        }
    }
    
    public void SetBlinkInterval(float minInterval, float maxInterval)
    {
        minBlinkInterval = Mathf.Max(1f, minInterval);
        maxBlinkInterval = Mathf.Max(minBlinkInterval + 1f, maxInterval);
        
        if (debugAnimations)
            Debug.Log($"👁️ Blink interval updated: {minBlinkInterval}s - {maxBlinkInterval}s");
    }
    
    public void TriggerWelcomeAnimation()
    {
        if (sculptureAnimator != null)
        {
            sculptureAnimator.SetTrigger(PARAM_WELCOME);
            if (debugAnimations)
                Debug.Log("👋 Welcome trigger activated");
        }
    }
    
    public void SetEmotion(float emotion)
    {
        emotionIntensity = Mathf.Clamp01(emotion);
        // Only set if you have this parameter in your animator
        if (HasParameter(PARAM_EMOTION))
        {
            sculptureAnimator.SetFloat(PARAM_EMOTION, emotionIntensity);
        }
    }
    
    public void SetAnimationSpeed(float speed)
    {
        animationSpeed = Mathf.Clamp(speed, 0.1f, 3f);
        if (sculptureAnimator != null)
        {
            sculptureAnimator.speed = animationSpeed;
        }
    }
    
    // Helper method to check if parameter exists
    bool HasParameter(string paramName)
    {
        if (sculptureAnimator == null) return false;
        
        foreach (var param in sculptureAnimator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
    
    void ResetAnimatorParameters()
    {
        if (sculptureAnimator == null) return;
        
        sculptureAnimator.SetBool(PARAM_IS_PLAYER_NEAR, false);
        sculptureAnimator.SetBool(PARAM_IS_RECORDING, false);
        sculptureAnimator.SetBool(PARAM_IS_THINKING, false);
        sculptureAnimator.SetBool(PARAM_IS_SPEAKING, false);
        
        // Only set optional parameters if they exist
        if (HasParameter(PARAM_EMOTION))
            sculptureAnimator.SetFloat(PARAM_EMOTION, 0.5f);
        if (HasParameter(PARAM_BLINK_WEIGHT))
            sculptureAnimator.SetFloat(PARAM_BLINK_WEIGHT, 0f);
        if (HasParameter(PARAM_MOUTH_OPEN))
            sculptureAnimator.SetFloat(PARAM_MOUTH_OPEN, 0f);
    }
    
    // Debug and testing methods
    [ContextMenu("Debug Blink System")]
    public void DebugBlinkSystem()
    {
        Debug.Log("=== EYE BLINK SYSTEM DEBUG ===");
        Debug.Log($"faceRenderer: {(faceRenderer != null ? faceRenderer.name : "NULL")}");
        Debug.Log($"eyeBlinkBlendShapeIndex: {eyeBlinkBlendShapeIndex}");
        Debug.Log($"eyeBlinkBlendShapeName: {eyeBlinkBlendShapeName}");
        Debug.Log($"enableBlinking: {enableBlinking}");
        Debug.Log($"isBlinking: {isBlinking}");
        Debug.Log($"blinkingCoroutine: {(blinkingCoroutine != null ? "Running" : "NULL")}");
        Debug.Log($"uLipSyncComponent: {(uLipSyncComponent != null ? uLipSyncComponent.name : "NULL")}");
        Debug.Log($"useULipSyncBlink: {useULipSyncBlink}");
        Debug.Log($"useLateUpdateOverride: {useLateUpdateOverride}");
        Debug.Log($"blinkOverrideWeight: {blinkOverrideWeight}");
        
        if (faceRenderer != null && faceRenderer.sharedMesh != null)
        {
            Debug.Log($"Total blend shapes: {faceRenderer.sharedMesh.blendShapeCount}");
            
            if (eyeBlinkBlendShapeIndex >= 0 && eyeBlinkBlendShapeIndex < faceRenderer.sharedMesh.blendShapeCount)
            {
                string actualName = faceRenderer.sharedMesh.GetBlendShapeName(eyeBlinkBlendShapeIndex);
                float currentWeight = faceRenderer.GetBlendShapeWeight(eyeBlinkBlendShapeIndex);
                Debug.Log($"Blend shape at index {eyeBlinkBlendShapeIndex}: '{actualName}' | Current weight: {currentWeight}");
            }
        }
        Debug.Log("================================");
    }
    
    [ContextMenu("Test Manual Blink")]
    public void TestManualBlink()
    {
        TriggerManualBlink();
    }
    
    [ContextMenu("Force Blink Test (Direct)")]
    public void TestDirectBlink()
    {
        if (faceRenderer != null && eyeBlinkBlendShapeIndex >= 0)
        {
            StartCoroutine(TestDirectBlinkCoroutine());
        }
    }
    
    private IEnumerator TestDirectBlinkCoroutine()
    {
        Debug.Log("=== TESTING DIRECT BLINK ===");
        for (float t = 0; t < 1; t += 0.1f)
        {
            float blinkAmount = Mathf.Sin(t * Mathf.PI) * 100f;
            faceRenderer.SetBlendShapeWeight(eyeBlinkBlendShapeIndex, blinkAmount);
            Debug.Log($"Set blend shape weight to: {blinkAmount}");
            yield return new WaitForSeconds(0.1f);
        }
        faceRenderer.SetBlendShapeWeight(eyeBlinkBlendShapeIndex, 0);
        Debug.Log("=== DIRECT BLINK TEST COMPLETE ===");
    }
    
    [ContextMenu("Toggle LateUpdate Override")]
    public void ToggleLateUpdateOverride()
    {
        useLateUpdateOverride = !useLateUpdateOverride;
        Debug.Log($"👁️ LateUpdate Override: {(useLateUpdateOverride ? "ON" : "OFF")}");
    }
    
    [ContextMenu("Test Welcome")]
    public void TestWelcome()
    {
        TriggerWelcomeAnimation();
        sculptureAnimator.SetBool(PARAM_IS_PLAYER_NEAR, true);
    }
    
    [ContextMenu("Test Recording")]
    public void TestRecording()
    {
        sculptureAnimator.SetBool(PARAM_IS_RECORDING, true);
        SetEmotion(0.6f);
    }
    
    [ContextMenu("Test Thinking")]
    public void TestThinking()
    {
        sculptureAnimator.SetBool(PARAM_IS_THINKING, true);
        SetEmotion(0.8f);
    }
    
    [ContextMenu("Test Speaking")]
    public void TestSpeaking()
    {
        sculptureAnimator.SetBool(PARAM_IS_SPEAKING, true);
        SetEmotion(1.0f);
        StartSpeakingAnimation();
    }
    
    [ContextMenu("Test Exit (Player Leaves)")]
    public void TestExit()
    {
        sculptureAnimator.SetBool(PARAM_IS_PLAYER_NEAR, false);
        SetEmotion(0.2f);
    }
    
    [ContextMenu("Reset All")]
    public void ResetAll()
    {
        ResetAnimatorParameters();
        StopAllCoroutines();
    }
    
    [ContextMenu("Toggle Blinking")]
    public void ToggleBlinking()
    {
        SetBlinkingEnabled(!enableBlinking);
        Debug.Log($"👁️ Blinking {(enableBlinking ? "enabled" : "disabled")}");
    }
    
    [ContextMenu("List Blend Shapes")]
    public void ListBlendShapes()
    {
        if (faceRenderer == null || faceRenderer.sharedMesh == null)
        {
            Debug.Log("No mesh renderer or mesh found");
            return;
        }
        
        Debug.Log($"📋 Available blend shapes on {faceRenderer.name}:");
        for (int i = 0; i < faceRenderer.sharedMesh.blendShapeCount; i++)
        {
            string name = faceRenderer.sharedMesh.GetBlendShapeName(i);
            float weight = faceRenderer.GetBlendShapeWeight(i);
            Debug.Log($"  {i}: {name} (current weight: {weight:F1})");
        }
    }
    
    [ContextMenu("Show Current State")]
    public void ShowCurrentState()
    {
        if (sculptureAnimator != null)
        {
            Debug.Log($"🎭 Current Animation State: {sculptureAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash}");
            Debug.Log($"   IsPlayerNear: {sculptureAnimator.GetBool(PARAM_IS_PLAYER_NEAR)}");
            Debug.Log($"   IsRecording: {sculptureAnimator.GetBool(PARAM_IS_RECORDING)}");
            Debug.Log($"   IsThinking: {sculptureAnimator.GetBool(PARAM_IS_THINKING)}");
            Debug.Log($"   IsSpeaking: {sculptureAnimator.GetBool(PARAM_IS_SPEAKING)}");
            
            if (eyeBlinkBlendShapeIndex >= 0)
            {
                Debug.Log($"👁️ Blinking: {(enableBlinking ? "enabled" : "disabled")}, Currently blinking: {isBlinking}");
                Debug.Log($"👁️ Override weight: {blinkOverrideWeight}");
            }
        }
    }
    
    // Public getters for debugging
    public ConversationState GetCurrentState() => currentState;
    public float GetCurrentEmotion() => currentEmotion;
    public bool IsPlayerNear() => wasPlayerNear;
    
    void OnDestroy()
    {
        StopBlinking();
    }
    
    void OnValidate()
    {
        // Update animation speed when changed in inspector
        if (sculptureAnimator != null)
        {
            sculptureAnimator.speed = animationSpeed;
        }
        
        // Clamp blink intervals
        minBlinkInterval = Mathf.Max(1f, minBlinkInterval);
        maxBlinkInterval = Mathf.Max(minBlinkInterval + 0.5f, maxBlinkInterval);
    }
}