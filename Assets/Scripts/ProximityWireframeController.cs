using UnityEngine;

public class ProximityWireframeController : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The wireframe model with ProximityWireframe shader")]
    public GameObject wireframeModel;
    
    [Tooltip("Material using the ProximityWireframe shader")]
    public Material proximityWireframeMaterial;
    
    [Header("Hand Detection")]
    [Tooltip("Left hand transform")]
    public Transform leftHand;
    
    [Tooltip("Right hand transform")]
    public Transform rightHand;
    
    [Header("Proximity Settings")]
    [Tooltip("How far from hands the wireframe reveals")]
    public float revealRadius = 1.5f;
    
    [Tooltip("How soft the edge of the reveal is")]
    public float fadeDistance = 0.5f;
    
    [Tooltip("Maximum distance for hand detection")]
    public float maxHandDistance = 3f;
    
    [Header("Reveal Modes")]
    [Tooltip("Always show some wireframe (0-1)")]
    [Range(0f, 1f)]
    public float globalReveal = 0f;
    
    [Tooltip("Automatically enable when player gets close")]
    public bool enableOnProximity = true;
    
    [Tooltip("Distance to auto-enable")]
    public float autoEnableDistance = 2f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showGizmos = true;
    
    // Private variables
    private Material materialInstance;
    private Camera playerCamera;
    private bool systemActive = false;
    
    // Shader property IDs (for performance)
    private int handPosition1ID;
    private int handPosition2ID;
    private int revealRadiusID;
    private int fadeDistanceID;
    private int globalRevealID;
    
    void Start()
    {
        SetupSystem();
        CacheShaderProperties();
        FindPlayerCamera();
        AutoFindHands();
        
        if (showDebugInfo)
        {
            Debug.Log("🖐️ Proximity Wireframe Controller initialized");
            Debug.Log($"   Wireframe Model: {(wireframeModel ? wireframeModel.name : "Not Set")}");
            Debug.Log($"   Left Hand: {(leftHand ? leftHand.name : "Not Set")}");
            Debug.Log($"   Right Hand: {(rightHand ? rightHand.name : "Not Set")}");
            Debug.Log($"   Reveal Radius: {revealRadius}m");
        }
    }
    
    void SetupSystem()
    {
        // Auto-find wireframe model if not set
        if (wireframeModel == null)
        {
            wireframeModel = GameObject.Find("lowpoly");
            if (wireframeModel != null && showDebugInfo)
            {
                Debug.Log($"🔍 Auto-found wireframe model: {wireframeModel.name}");
            }
        }
        
        if (wireframeModel == null)
        {
            Debug.LogError("❌ No wireframe model found!");
            return;
        }
        
        // Get or create material instance
        Renderer renderer = wireframeModel.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("❌ No renderer on wireframe model!");
            return;
        }
        
        if (proximityWireframeMaterial != null)
        {
            // Use our custom material
            materialInstance = new Material(proximityWireframeMaterial);
            renderer.material = materialInstance;
        }
        else
        {
            // Use existing material (assume it's the right shader)
            materialInstance = renderer.material;
        }
        
        // Start with wireframe visible but no reveal
        wireframeModel.SetActive(true);
        
        if (showDebugInfo)
        {
            Debug.Log($"✅ System setup complete");
            Debug.Log($"   Material: {materialInstance.name}");
            Debug.Log($"   Shader: {materialInstance.shader.name}");
        }
    }
    
    void CacheShaderProperties()
    {
        // Cache shader property IDs for better performance
        handPosition1ID = Shader.PropertyToID("_HandPosition1");
        handPosition2ID = Shader.PropertyToID("_HandPosition2");
        revealRadiusID = Shader.PropertyToID("_RevealRadius");
        fadeDistanceID = Shader.PropertyToID("_FadeDistance");
        globalRevealID = Shader.PropertyToID("_GlobalReveal");
    }
    
    void FindPlayerCamera()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
    }
    
    void AutoFindHands()
    {
        if (leftHand == null || rightHand == null)
        {
            // Look for common hand names
            string[] leftHandNames = { "LeftHand", "Left Hand", "HandPresence_L", "LeftHandPresence", "Left Controller" };
            string[] rightHandNames = { "RightHand", "Right Hand", "HandPresence_R", "RightHandPresence", "Right Controller" };
            
            foreach (string name in leftHandNames)
            {
                GameObject found = GameObject.Find(name);
                if (found != null)
                {
                    leftHand = found.transform;
                    if (showDebugInfo) Debug.Log($"🔍 Auto-found left hand: {name}");
                    break;
                }
            }
            
            foreach (string name in rightHandNames)
            {
                GameObject found = GameObject.Find(name);
                if (found != null)
                {
                    rightHand = found.transform;
                    if (showDebugInfo) Debug.Log($"🔍 Auto-found right hand: {name}");
                    break;
                }
            }
        }
    }
    
    void Update()
    {
        if (materialInstance == null) return;
        
        CheckSystemActivation();
        UpdateShaderProperties();
    }
    
    void CheckSystemActivation()
    {
        if (!enableOnProximity)
        {
            systemActive = true;
            return;
        }
        
        // Check if player is close enough to activate system
        if (playerCamera != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerCamera.transform.position);
            bool shouldBeActive = distanceToPlayer <= autoEnableDistance;
            
            if (shouldBeActive != systemActive)
            {
                systemActive = shouldBeActive;
                
                if (showDebugInfo)
                {
                    Debug.Log($"🔄 Proximity system {(systemActive ? "activated" : "deactivated")} - Distance: {distanceToPlayer:F2}m");
                }
            }
        }
    }
    
    void UpdateShaderProperties()
    {
        // Update reveal radius and fade distance
        materialInstance.SetFloat(revealRadiusID, revealRadius);
        materialInstance.SetFloat(fadeDistanceID, fadeDistance);
        materialInstance.SetFloat(globalRevealID, systemActive ? globalReveal : 0f);
        
        // Update hand positions
        UpdateHandPosition(leftHand, handPosition1ID);
        UpdateHandPosition(rightHand, handPosition2ID);
    }
    
    void UpdateHandPosition(Transform hand, int propertyID)
    {
        if (hand != null && systemActive)
        {
            // Check if hand is close enough to sculpture
            float distanceToSculpture = Vector3.Distance(transform.position, hand.position);
            bool handActive = distanceToSculpture <= maxHandDistance;
            
            Vector4 handData = new Vector4(
                hand.position.x,
                hand.position.y,
                hand.position.z,
                handActive ? 1f : 0f
            );
            
            materialInstance.SetVector(propertyID, handData);
        }
        else
        {
            // Hand not available or system inactive
            materialInstance.SetVector(propertyID, Vector4.zero);
        }
    }
    
    // Public methods for external control
    public void SetRevealRadius(float radius)
    {
        revealRadius = Mathf.Max(0.1f, radius);
    }
    
    public void SetFadeDistance(float distance)
    {
        fadeDistance = Mathf.Max(0.1f, distance);
    }
    
    public void SetGlobalReveal(float reveal)
    {
        globalReveal = Mathf.Clamp01(reveal);
    }
    
    public void ForceActivate()
    {
        systemActive = true;
        Debug.Log("🔧 Proximity wireframe force activated");
    }
    
    public void ForceDeactivate()
    {
        systemActive = false;
        Debug.Log("🔧 Proximity wireframe force deactivated");
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        // Draw auto-enable radius
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, autoEnableDistance);
        
        // Draw max hand distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxHandDistance);
        
        // Draw hand reveal radius
        if (Application.isPlaying && systemActive)
        {
            if (leftHand != null)
            {
                float dist = Vector3.Distance(transform.position, leftHand.position);
                if (dist <= maxHandDistance)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(leftHand.position, revealRadius);
                }
            }
            
            if (rightHand != null)
            {
                float dist = Vector3.Distance(transform.position, rightHand.position);
                if (dist <= maxHandDistance)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(rightHand.position, revealRadius);
                }
            }
        }
    }
    
    // Context menu methods
    [ContextMenu("Force Activate")]
    public void TestForceActivate()
    {
        ForceActivate();
    }
    
    [ContextMenu("Force Deactivate")]
    public void TestForceDeactivate()
    {
        ForceDeactivate();
    }
    
    [ContextMenu("Test Full Reveal")]
    public void TestFullReveal()
    {
        SetGlobalReveal(1f);
        Debug.Log("🔥 Testing full wireframe reveal");
    }
    
    [ContextMenu("Test No Reveal")]
    public void TestNoReveal()
    {
        SetGlobalReveal(0f);
        Debug.Log("🔥 Testing no wireframe reveal");
    }
    
    [ContextMenu("Find Hands")]
    public void ManualFindHands()
    {
        AutoFindHands();
    }
    
    void OnDestroy()
    {
        // Clean up material instance
        if (materialInstance != null && materialInstance != proximityWireframeMaterial)
        {
            DestroyImmediate(materialInstance);
        }
    }
    
    // Getters
    public bool IsSystemActive() => systemActive;
    public float GetRevealRadius() => revealRadius;
    public bool HasBothHands() => leftHand != null && rightHand != null;
}