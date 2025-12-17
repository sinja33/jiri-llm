using UnityEngine;

public class SculptureInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionDistance = 50f;
    public bool showDebugInfo = true;
    public bool use3DDistance = false; // Option to use full 3D distance instead of 2D
    
    [Header("Visual Feedback")]
    public GameObject interactionPrompt;
    public Light[] ambientLights; // Optional: lights that change based on conversation state
    
    [Header("Speech System")]
    public SpeechManager speechManager;
    
    [Header("Scaling System")]
    public SculptureScaling sculptureScaling;
    
    private Transform playerTransform;
    private bool isPlayerNearby = false;
    private AudioSource audioSource;
    
    void Start()
    {
        FindPlayer();
        SetupAudio();
        SetupScaling();
        Debug.Log($"Sculpture interaction system initialized - Zone radius: {interactionDistance}m");
    }
    
    void FindPlayer()
    {
        // Try multiple methods to find the player/camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            // Look for any camera tagged as MainCamera
            GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCameraObj != null)
            {
                mainCam = mainCameraObj.GetComponent<Camera>();
            }
        }
        
        if (mainCam == null)
        {
            // Fallback: find any camera
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0)
                mainCam = cameras[0];
        }
        
        if (mainCam != null)
        {
            playerTransform = mainCam.transform;
            Debug.Log($"Found player camera: {mainCam.name} at position: {mainCam.transform.position}");
        }
        else
        {
            Debug.LogError("No camera found! Make sure there's a camera in the scene.");
        }
    }
    
    void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 15f;
        audioSource.volume = 0.8f;
    }
    
    void SetupScaling()
    {
        // Auto-find or create the scaling component
        if (sculptureScaling == null)
        {
            sculptureScaling = GetComponent<SculptureScaling>();
            if (sculptureScaling == null)
            {
                sculptureScaling = gameObject.AddComponent<SculptureScaling>();
                Debug.Log("Added SculptureScaling component automatically");
            }
        }
        
        // Ensure scaling distance is larger than interaction distance
        if (sculptureScaling.scalingDistance <= interactionDistance)
        {
            sculptureScaling.scalingDistance = interactionDistance * 2f;
            Debug.LogWarning($"Scaling distance was too small, automatically set to {sculptureScaling.scalingDistance}m");
        }
    }
    
    void Update()
    {
        if (playerTransform != null)
        {
            CheckPlayerProximity();
            UpdateVisualFeedback();
        }
        else
        {
            // Try to find player again if we lost reference
            FindPlayer();
        }
    }
    
    void CheckPlayerProximity()
    {
        Vector3 sculpturePos = transform.position;
        Vector3 playerPos = playerTransform.position;
        
        float distance;
        
        if (use3DDistance)
        {
            // Use full 3D distance
            distance = Vector3.Distance(sculpturePos, playerPos);
        }
        else
        {
            // Use 2D distance (ignore Y axis/height difference)
            Vector2 sculpturePos2D = new Vector2(sculpturePos.x, sculpturePos.z);
            Vector2 playerPos2D = new Vector2(playerPos.x, playerPos.z);
            distance = Vector2.Distance(sculpturePos2D, playerPos2D);
        }
        
        bool wasNearby = isPlayerNearby;
        isPlayerNearby = distance <= interactionDistance;
        
        // Player entered interaction zone
        if (isPlayerNearby && !wasNearby)
        {
            Debug.Log($"*** PLAYER ENTERED CONVERSATION ZONE! Distance: {distance:F2}m ***");
            OnPlayerEnterZone();
        }
        // Player left interaction zone
        else if (!isPlayerNearby && wasNearby)
        {
            Debug.Log($"*** PLAYER LEFT CONVERSATION ZONE! Distance: {distance:F2}m ***");
            OnPlayerExitZone();
        }
        
        // Debug information - show every second instead of every frame
        if (showDebugInfo && Time.frameCount % 60 == 0) // Once per second at 60fps
        {
            string distanceType = use3DDistance ? "3D" : "2D";
            Debug.Log($"Player distance ({distanceType}): {distance:F2}m | Zone radius: {interactionDistance}m | In zone: {isPlayerNearby}");
            
            if (sculptureScaling != null)
            {
                Debug.Log($"Scaling info: {GetScalingInfo()}");
            }
        }
        
        // Visual debug - draw line to player
        if (showDebugInfo)
        {
            Color lineColor = isPlayerNearby ? Color.green : Color.red;
            Debug.DrawLine(transform.position, playerTransform.position, lineColor);
        }
    }
    
    void OnPlayerEnterZone()
    {
        // Notify speech manager
        if (speechManager != null)
        {
            speechManager.OnPlayerEnteredZone();
        }
        else
        {
            Debug.LogWarning("SpeechManager not assigned!");
        }
        
        // Show interaction prompt if available
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(true);
        }
        
        // Optional: Play entrance audio cue
        PlayEntranceSound();
    }
    
    void OnPlayerExitZone()
    {
        // Notify speech manager
        if (speechManager != null)
        {
            speechManager.OnPlayerExitedZone();
        }
        
        // Hide interaction prompt
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }
        
        // Optional: Play exit audio cue
        PlayExitSound();
    }
    
    void UpdateVisualFeedback()
    {
        if (speechManager == null || ambientLights == null || ambientLights.Length == 0) 
            return;
        
        // Change lighting based on conversation state
        var currentState = speechManager.GetCurrentState();
        
        foreach (Light light in ambientLights)
        {
            if (light == null) continue;
            
            switch (currentState)
            {
                case ConversationState.Idle:
                    light.color = Color.white;
                    light.intensity = 0.5f;
                    break;
                    
                case ConversationState.Ready:
                    light.color = Color.cyan;
                    light.intensity = 0.8f + Mathf.Sin(Time.time * 2f) * 0.2f; // Gentle pulsing
                    break;
                    
                case ConversationState.Recording:
                    light.color = Color.red;
                    light.intensity = 1.0f + Mathf.Sin(Time.time * 6f) * 0.3f; // Faster pulsing
                    break;
                    
                case ConversationState.Processing:
                    light.color = Color.yellow;
                    light.intensity = 1.0f;
                    break;
                    
                case ConversationState.Speaking:
                    light.color = Color.green;
                    light.intensity = 1.2f + Mathf.Sin(Time.time * 4f) * 0.3f; // Active pulsing
                    break;
            }
        }
    }
    
    void PlayEntranceSound()
    {
        // Optional: Play a subtle sound when someone enters the zone
        if (audioSource != null)
        {
            // You can assign audio clips in the inspector and play them here
            // audioSource.PlayOneShot(entranceClip);
        }
    }
    
    void PlayExitSound()
    {
        // Optional: Play a subtle sound when someone leaves
        if (audioSource != null)
        {
            // audioSource.PlayOneShot(exitClip);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw the interaction zone
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        
        // Draw different colors based on state if speech manager is available
        if (speechManager != null && Application.isPlaying)
        {
            var state = speechManager.GetCurrentState();
            switch (state)
            {
                case ConversationState.Idle:
                    Gizmos.color = Color.gray;
                    break;
                case ConversationState.Ready:
                    Gizmos.color = Color.cyan;
                    break;
                case ConversationState.Recording:
                    Gizmos.color = Color.red;
                    break;
                case ConversationState.Processing:
                    Gizmos.color = Color.yellow;
                    break;
                case ConversationState.Speaking:
                    Gizmos.color = Color.green;
                    break;
            }
            
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
        
        // Draw scaling zone if available
        if (sculptureScaling != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, sculptureScaling.scalingDistance);
        }
    }
    
    // Public methods for external systems
    public bool IsPlayerInZone()
    {
        return isPlayerNearby;
    }
    
    public float GetDistanceToPlayer()
    {
        if (playerTransform == null) return float.MaxValue;
        
        Vector3 sculpturePos = transform.position;
        Vector3 playerPos = playerTransform.position;
        
        if (use3DDistance)
        {
            return Vector3.Distance(sculpturePos, playerPos);
        }
        else
        {
            Vector2 sculpturePos2D = new Vector2(sculpturePos.x, sculpturePos.z);
            Vector2 playerPos2D = new Vector2(playerPos.x, playerPos.z);
            return Vector2.Distance(sculpturePos2D, playerPos2D);
        }
    }
    
    // Method to get current scaling info
    public string GetScalingInfo()
    {
        if (sculptureScaling == null) return "No scaling component";
        
        float distance = GetDistanceToPlayer();
        float currentScale = sculptureScaling.GetCurrentScale();
        float targetScale = sculptureScaling.GetTargetScale();
        
        return $"Distance: {distance:F1}m | Scale: {currentScale:F2} → {targetScale:F2}";
    }
    
    // Debug method to test zone entry/exit
    [ContextMenu("Test Zone Entry")]
    public void TestZoneEntry()
    {
        OnPlayerEnterZone();
    }
    
    [ContextMenu("Test Zone Exit")]  
    public void TestZoneExit()
    {
        OnPlayerExitZone();
    }
}