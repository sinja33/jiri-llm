using UnityEngine;

public class GrabCanvasController : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("The grab instruction canvas (world space)")]
    public Canvas grabCanvas;
    
    [Header("Zone Settings")]
    [Tooltip("Distance to activate grab canvas")]
    public float activationDistance = 2f;
    
    [Tooltip("Use 2D distance (ignore height) or full 3D")]
    public bool use2DDistance = true;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showGizmos = true;
    
    // Private variables
    private Camera playerCamera;
    private bool isCanvasActive = false;
    private bool seedGrabbed = false;
    
    void Start()
    {
        // Find player camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
        
        // Start with canvas hidden
        if (grabCanvas != null)
        {
            grabCanvas.gameObject.SetActive(false);
        }
        
        if (showDebugInfo)
        {
            Debug.Log("🌱 Grab Canvas Controller initialized");
            Debug.Log($"   Canvas: {(grabCanvas ? grabCanvas.name : "Not Set")}");
            Debug.Log($"   Activation Distance: {activationDistance}m");
        }
    }
    
    void Update()
    {
        if (playerCamera == null || grabCanvas == null || seedGrabbed) return;
        
        CheckPlayerProximity();
    }
    
    void CheckPlayerProximity()
    {
        Vector3 seedPos = transform.position;
        Vector3 playerPos = playerCamera.transform.position;
        
        float distance;
        
        if (use2DDistance)
        {
            // Use 2D distance (ignore Y axis/height)
            Vector2 seedPos2D = new Vector2(seedPos.x, seedPos.z);
            Vector2 playerPos2D = new Vector2(playerPos.x, playerPos.z);
            distance = Vector2.Distance(seedPos2D, playerPos2D);
        }
        else
        {
            // Use full 3D distance
            distance = Vector3.Distance(seedPos, playerPos);
        }
        
        bool shouldShowCanvas = distance <= activationDistance;
        
        // Only change state if needed
        if (shouldShowCanvas && !isCanvasActive)
        {
            ShowGrabCanvas();
        }
        else if (!shouldShowCanvas && isCanvasActive)
        {
            HideGrabCanvas();
        }
    }
    
    void ShowGrabCanvas()
    {
        if (grabCanvas != null && !seedGrabbed)
        {
            grabCanvas.gameObject.SetActive(true);
            isCanvasActive = true;
            
            if (showDebugInfo)
                Debug.Log("🖐️ Grab canvas activated - player in range");
        }
    }
    
    void HideGrabCanvas()
    {
        if (grabCanvas != null)
        {
            grabCanvas.gameObject.SetActive(false);
            isCanvasActive = false;
            
            if (showDebugInfo)
                Debug.Log("🖐️ Grab canvas deactivated - player out of range");
        }
    }
    
    // === PUBLIC METHOD FOR XR GRAB INTERACTABLE ===
    
    public void OnSeedGrabbed()
    {
        seedGrabbed = true;
        HideGrabCanvas();
        
        if (showDebugInfo)
            Debug.Log("🌱 Seed grabbed - grab canvas permanently hidden");
    }
    
    // === PUBLIC METHODS FOR MANUAL CONTROL ===
    
    public void ForceShowCanvas()
    {
        if (!seedGrabbed)
            ShowGrabCanvas();
    }
    
    public void ForceHideCanvas()
    {
        HideGrabCanvas();
    }
    
    public void ResetSeedState()
    {
        seedGrabbed = false;
        if (showDebugInfo)
            Debug.Log("🔄 Seed state reset - canvas can show again");
    }
    
    // === DEBUG VISUALIZATION ===
    
    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        // Draw activation zone
        Gizmos.color = isCanvasActive ? Color.green : Color.yellow;
        
        if (use2DDistance)
        {
            // Draw circle on ground plane
            DrawWireCircle(transform.position, activationDistance);
        }
        else
        {
            // Draw 3D sphere
            Gizmos.DrawWireSphere(transform.position, activationDistance);
        }
        
        // Draw line to player if in range
        if (playerCamera != null && Application.isPlaying)
        {
            float distance = use2DDistance ? 
                Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z), 
                    new Vector2(playerCamera.transform.position.x, playerCamera.transform.position.z)
                ) :
                Vector3.Distance(transform.position, playerCamera.transform.position);
            
            Color lineColor = distance <= activationDistance ? Color.green : Color.red;
            Gizmos.color = lineColor;
            Gizmos.DrawLine(transform.position, playerCamera.transform.position);
        }
    }
    
    void DrawWireCircle(Vector3 center, float radius)
    {
        int segments = 32;
        float angleStep = 360f / segments;
        
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    // === CONTEXT MENU FOR TESTING ===
    
    [ContextMenu("Test Show Canvas")]
    public void TestShowCanvas()
    {
        ForceShowCanvas();
    }
    
    [ContextMenu("Test Hide Canvas")]
    public void TestHideCanvas()
    {
        ForceHideCanvas();
    }
    
    [ContextMenu("Test Seed Grabbed")]
    public void TestSeedGrabbed()
    {
        OnSeedGrabbed();
    }
    
    [ContextMenu("Reset Seed State")]
    public void TestResetSeed()
    {
        ResetSeedState();
    }
    
    // === GETTERS ===
    
    public bool IsCanvasActive() => isCanvasActive;
    public bool IsSeedGrabbed() => seedGrabbed;
    public float GetDistanceToPlayer()
    {
        if (playerCamera == null) return float.MaxValue;
        
        if (use2DDistance)
        {
            return Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(playerCamera.transform.position.x, playerCamera.transform.position.z)
            );
        }
        else
        {
            return Vector3.Distance(transform.position, playerCamera.transform.position);
        }
    }
}