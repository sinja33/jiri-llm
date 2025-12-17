using UnityEngine;

public class SculptureWireframeToggle : MonoBehaviour
{
    [Header("Models")]
    [Tooltip("The low poly wireframe model (should start inactive)")]
    public GameObject lowPolyWireframe;
    
    [Tooltip("The high poly mesh objects (Kip, Kip1.001, etc.)")]
    public GameObject[] highPolyMeshes;
    
    [Header("Materials")]
    [Tooltip("The original material for high poly")]
    public Material originalMaterial;
    
    [Tooltip("Invisible material to hide high poly")]
    public Material invisibleMaterial;
    
    [Header("Detection Settings")]
    [Tooltip("Shape of the detection area")]
    public DetectionShape detectionShape = DetectionShape.Sphere;
    
    [Tooltip("Distance/size for sphere detection")]
    public float insideDistance = 1.5f;
    
    [Tooltip("Custom box size for box detection")]
    public Vector3 boxSize = new Vector3(2f, 3f, 2f);
    
    [Tooltip("Custom detection collider (if you want very specific shapes)")]
    public Collider customDetectionZone;
    
    [Tooltip("Use 2D distance (ignore height) or full 3D")]
    public bool use2DDistance = true;
    
    [Header("Fading Effect")]
    [Tooltip("How fast the transition happens")]
    public float fadeSpeed = 2f;
    
    [Tooltip("Smooth curve for fading (S-curve vs linear)")]
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Tooltip("Wireframe fade in delay (seconds)")]
    public float wireframeFadeInDelay = 0.1f;
    
    [Tooltip("High poly fade out delay (seconds)")]
    public float highPolyFadeOutDelay = 0.0f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showGizmos = true;
    
    // Private variables
    private Camera playerCamera;
    private bool isPlayerInside = false;
    private bool wasPlayerInside = false;
    private Material[][] originalMaterials; // Store original materials for each mesh
    private float currentWireframeAlpha = 0f;
    private float currentHighPolyAlpha = 1f;
    private float targetWireframeAlpha = 0f;
    private float targetHighPolyAlpha = 1f;
    private bool isTransitioning = false;
    
    public enum DetectionShape
    {
        Sphere,
        Box,
        CustomCollider
    }
    
    void Start()
    {
        FindPlayerCamera();
        
        // Auto-find wireframe if not assigned
        if (lowPolyWireframe == null)
        {
            AutoFindWireframeModel();
        }
        
        SetupMaterials();
        
        // Start in normal mode (high poly visible, wireframe hidden)
        currentWireframeAlpha = 0f;
        currentHighPolyAlpha = 1f;
        targetWireframeAlpha = 0f;
        targetHighPolyAlpha = 1f;
        
        if (lowPolyWireframe != null)
        {
            lowPolyWireframe.SetActive(false);
        }
        
        if (showDebugInfo)
        {
            Debug.Log("🔄 Sculpture Wireframe Toggle initialized");
            Debug.Log($"   Inside Distance: {insideDistance}m");
            Debug.Log($"   Detection Shape: {detectionShape}");
            Debug.Log($"   High Poly Meshes: {highPolyMeshes.Length}");
            Debug.Log($"   Low Poly Wireframe: {(lowPolyWireframe ? lowPolyWireframe.name : "Not Found")}");
        }
    }
    
    void FindPlayerCamera()
    {
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }
        
        if (playerCamera != null && showDebugInfo)
        {
            Debug.Log($"📷 Found player camera: {playerCamera.name}");
        }
        else
        {
            Debug.LogError("❌ No camera found for player detection!");
        }
    }
    
    void SetupMaterials()
    {
        // Store the original materials from each high poly mesh
        originalMaterials = new Material[highPolyMeshes.Length][];
        
        for (int i = 0; i < highPolyMeshes.Length; i++)
        {
            if (highPolyMeshes[i] != null)
            {
                Renderer renderer = highPolyMeshes[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    // Store the current materials
                    originalMaterials[i] = renderer.materials;
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"📦 Stored materials for {highPolyMeshes[i].name}:");
                        for (int m = 0; m < originalMaterials[i].Length; m++)
                        {
                            Debug.Log($"     Material {m}: {originalMaterials[i][m].name}");
                        }
                    }
                }
            }
        }
    }
    
    void Update()
    {
        if (playerCamera == null) return;
        
        CheckPlayerPosition();
        HandleModeSwitch();
    }
    
    void CheckPlayerPosition()
    {
        Vector3 sculpturePos = transform.position;
        Vector3 playerPos = playerCamera.transform.position;
        
        wasPlayerInside = isPlayerInside;
        
        // Check based on detection shape
        switch (detectionShape)
        {
            case DetectionShape.Sphere:
                isPlayerInside = CheckSphereDetection(sculpturePos, playerPos);
                break;
            case DetectionShape.Box:
                isPlayerInside = CheckBoxDetection(sculpturePos, playerPos);
                break;
            case DetectionShape.CustomCollider:
                isPlayerInside = CheckCustomColliderDetection(playerPos);
                break;
        }
        
        // Debug info every 2 seconds
        if (showDebugInfo && Time.frameCount % 120 == 0)
        {
            float distance = use2DDistance ? 
                Vector2.Distance(new Vector2(sculpturePos.x, sculpturePos.z), new Vector2(playerPos.x, playerPos.z)) :
                Vector3.Distance(sculpturePos, playerPos);
                
            Debug.Log($"📏 Player distance: {distance:F2}m | Detection: {detectionShape} | Inside: {isPlayerInside}");
        }
    }
    
    bool CheckSphereDetection(Vector3 sculpturePos, Vector3 playerPos)
    {
        float distance;
        
        if (use2DDistance)
        {
            Vector2 sculpturePos2D = new Vector2(sculpturePos.x, sculpturePos.z);
            Vector2 playerPos2D = new Vector2(playerPos.x, playerPos.z);
            distance = Vector2.Distance(sculpturePos2D, playerPos2D);
        }
        else
        {
            distance = Vector3.Distance(sculpturePos, playerPos);
        }
        
        return distance <= insideDistance;
    }
    
    bool CheckBoxDetection(Vector3 sculpturePos, Vector3 playerPos)
    {
        // Create a box around the sculpture
        Vector3 relativePos = playerPos - sculpturePos;
        
        if (use2DDistance)
        {
            // Check only X and Z (ignore Y)
            return Mathf.Abs(relativePos.x) <= boxSize.x * 0.5f && 
                   Mathf.Abs(relativePos.z) <= boxSize.z * 0.5f;
        }
        else
        {
            // Check all three dimensions
            return Mathf.Abs(relativePos.x) <= boxSize.x * 0.5f && 
                   Mathf.Abs(relativePos.y) <= boxSize.y * 0.5f && 
                   Mathf.Abs(relativePos.z) <= boxSize.z * 0.5f;
        }
    }
    
    bool CheckCustomColliderDetection(Vector3 playerPos)
    {
        if (customDetectionZone == null) return false;
        
        // Check if player position is inside the custom collider bounds
        return customDetectionZone.bounds.Contains(playerPos);
    }
    
    void HandleModeSwitch()
    {
        // Set target alphas based on player position
        if (isPlayerInside)
        {
            targetWireframeAlpha = 1f;
            targetHighPolyAlpha = 0f;
        }
        else
        {
            targetWireframeAlpha = 0f;
            targetHighPolyAlpha = 1f;
        }
        
        // Smooth transition
        UpdateFading();
        
        // Only log when player enters or exits (not during transition)
        if (isPlayerInside != wasPlayerInside)
        {
            if (showDebugInfo)
            {
                if (isPlayerInside)
                    Debug.Log("🔄 Player entered sculpture - fading to WIREFRAME mode");
                else
                    Debug.Log("🔄 Player exited sculpture - fading to NORMAL mode");
            }
            
            isTransitioning = true;
        }
    }
    
    void UpdateFading()
    {
        bool wireframeChanged = false;
        bool highPolyChanged = false;
        
        // Update wireframe alpha
        if (Mathf.Abs(currentWireframeAlpha - targetWireframeAlpha) > 0.01f)
        {
            currentWireframeAlpha = Mathf.Lerp(currentWireframeAlpha, targetWireframeAlpha, fadeSpeed * Time.deltaTime);
            wireframeChanged = true;
        }
        
        // Update high poly alpha
        if (Mathf.Abs(currentHighPolyAlpha - targetHighPolyAlpha) > 0.01f)
        {
            currentHighPolyAlpha = Mathf.Lerp(currentHighPolyAlpha, targetHighPolyAlpha, fadeSpeed * Time.deltaTime);
            highPolyChanged = true;
        }
        
        // Apply fading with curve
        if (wireframeChanged)
        {
            float curvedAlpha = fadeCurve.Evaluate(currentWireframeAlpha);
            ApplyWireframeAlpha(curvedAlpha);
        }
        
        if (highPolyChanged)
        {
            float curvedAlpha = fadeCurve.Evaluate(currentHighPolyAlpha);
            ApplyHighPolyAlpha(curvedAlpha);
        }
        
        // Check if transition is complete
        if (isTransitioning && 
            Mathf.Abs(currentWireframeAlpha - targetWireframeAlpha) < 0.01f && 
            Mathf.Abs(currentHighPolyAlpha - targetHighPolyAlpha) < 0.01f)
        {
            isTransitioning = false;
            
            if (showDebugInfo)
                Debug.Log($"✅ Transition complete - Wireframe: {currentWireframeAlpha:F2}, HighPoly: {currentHighPolyAlpha:F2}");
        }
    }
    
    void ApplyWireframeAlpha(float alpha)
    {
        // Control GameObject visibility
        bool shouldBeVisible = alpha > 0.01f;
        if (lowPolyWireframe != null && lowPolyWireframe.activeInHierarchy != shouldBeVisible)
        {
            lowPolyWireframe.SetActive(shouldBeVisible);
        }
        
        // If wireframe has material properties for alpha, set them here
        if (lowPolyWireframe != null && shouldBeVisible)
        {
            Renderer renderer = lowPolyWireframe.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Material mat = renderer.material;
                
                // Try to set alpha on wireframe material
                if (mat.HasProperty("_WireframeFrontColour"))
                {
                    Color frontColor = mat.GetColor("_WireframeFrontColour");
                    frontColor.a = alpha;
                    mat.SetColor("_WireframeFrontColour", frontColor);
                }
                
                if (mat.HasProperty("_WireframeBackColour"))
                {
                    Color backColor = mat.GetColor("_WireframeBackColour");
                    backColor.a = alpha;
                    mat.SetColor("_WireframeBackColour", backColor);
                }
            }
        }
    }
    
    void ApplyHighPolyAlpha(float alpha)
    {
        // Fade between original and invisible materials based on alpha
        for (int i = 0; i < highPolyMeshes.Length; i++)
        {
            if (highPolyMeshes[i] != null && originalMaterials[i] != null)
            {
                Renderer renderer = highPolyMeshes[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (alpha > 0.01f)
                    {
                        // Show original materials
                        renderer.materials = originalMaterials[i];
                        
                        // If materials support alpha, fade them
                        Material[] currentMaterials = renderer.materials;
                        for (int m = 0; m < currentMaterials.Length; m++)
                        {
                            if (currentMaterials[m].HasProperty("_Color"))
                            {
                                Color color = originalMaterials[i][m].GetColor("_Color");
                                color.a = alpha;
                                currentMaterials[m].SetColor("_Color", color);
                            }
                        }
                    }
                    else
                    {
                        // Use invisible materials
                        Material[] invisibleMaterials = new Material[renderer.materials.Length];
                        for (int m = 0; m < invisibleMaterials.Length; m++)
                        {
                            invisibleMaterials[m] = invisibleMaterial;
                        }
                        renderer.materials = invisibleMaterials;
                    }
                }
            }
        }
    }
    
    // Public methods for external control
    public void ForceWireframeMode()
    {
        targetWireframeAlpha = 1f;
        targetHighPolyAlpha = 0f;
        Debug.Log("🔧 Forced wireframe mode ON");
    }
    
    public void ForceNormalMode()
    {
        targetWireframeAlpha = 0f;
        targetHighPolyAlpha = 1f;
        Debug.Log("🔧 Forced wireframe mode OFF");
    }
    
    public void SetInsideDistance(float distance)
    {
        insideDistance = distance;
        Debug.Log($"🔧 Inside distance set to: {distance}m");
    }
    
    // Getters
    public bool IsPlayerInside() => isPlayerInside;
    public float GetDistanceToPlayer()
    {
        if (playerCamera == null) return float.MaxValue;
        
        Vector3 sculpturePos = transform.position;
        Vector3 playerPos = playerCamera.transform.position;
        
        if (use2DDistance)
        {
            Vector2 sculpturePos2D = new Vector2(sculpturePos.x, sculpturePos.z);
            Vector2 playerPos2D = new Vector2(playerPos.x, playerPos.z);
            return Vector2.Distance(sculpturePos2D, playerPos2D);
        }
        else
        {
            return Vector3.Distance(sculpturePos, playerPos);
        }
    }
    
    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        // Draw detection area based on shape
        switch (detectionShape)
        {
            case DetectionShape.Sphere:
                Gizmos.color = isPlayerInside ? Color.green : Color.yellow;
                if (use2DDistance)
                {
                    DrawWireCircle(transform.position, insideDistance);
                }
                else
                {
                    Gizmos.DrawWireSphere(transform.position, insideDistance);
                }
                break;
                
            case DetectionShape.Box:
                Gizmos.color = isPlayerInside ? Color.green : Color.yellow;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, boxSize);
                Gizmos.matrix = Matrix4x4.identity;
                break;
                
            case DetectionShape.CustomCollider:
                if (customDetectionZone != null)
                {
                    Gizmos.color = isPlayerInside ? Color.green : Color.yellow;
                    Gizmos.DrawWireCube(customDetectionZone.bounds.center, customDetectionZone.bounds.size);
                }
                break;
        }
        
        // Draw line to player
        if (playerCamera != null && Application.isPlaying)
        {
            Color lineColor = isPlayerInside ? Color.green : Color.red;
            Gizmos.color = lineColor;
            Gizmos.DrawLine(transform.position, playerCamera.transform.position);
        }
        
        // Show fade status with small sphere
        if (Application.isPlaying)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.blue, currentWireframeAlpha);
            Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.1f);
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
    
    // Context menu methods for testing
    [ContextMenu("Test Force Wireframe Mode")]
    public void TestForceWireframe()
    {
        ForceWireframeMode();
    }
    
    [ContextMenu("Test Force Normal Mode")]
    public void TestForceNormal()
    {
        ForceNormalMode();
    }
    
    [ContextMenu("Auto-Find Wireframe Model")]
    public void AutoFindWireframeModel()
    {
        // First try to find by exact name "lowpoly" in the entire scene
        GameObject foundByName = GameObject.Find("lowpoly");
        if (foundByName != null)
        {
            lowPolyWireframe = foundByName;
            Debug.Log($"🔍 Auto-found wireframe model by exact name: {foundByName.name}");
            
            // Check if it has a renderer
            Renderer renderer = lowPolyWireframe.GetComponent<Renderer>();
            if (renderer != null)
            {
                Debug.Log($"   ✅ Has Renderer with material: {renderer.material.name}");
                Debug.Log($"   ✅ Current active state: {lowPolyWireframe.activeInHierarchy}");
            }
            else
            {
                Debug.LogWarning($"   ⚠️ No Renderer found on {foundByName.name}");
            }
            return;
        }
        
        // If not found by exact name, search all GameObjects in scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true); // Include inactive
        
        string[] wireframeNames = { 
            "lowpoly", "low poly", "wireframe", "wireframemodel", 
            "low_poly", "lowpolymodel", "wireframereveal" 
        };
        
        foreach (GameObject obj in allObjects)
        {
            string objName = obj.name.ToLower();
            
            foreach (string name in wireframeNames)
            {
                if (objName.Contains(name))
                {
                    lowPolyWireframe = obj;
                    Debug.Log($"🔍 Auto-found wireframe model in scene: {obj.name}");
                    Debug.Log($"   Location: {(obj.transform.parent ? "Child of " + obj.transform.parent.name : "Root level")}");
                    
                    // Check if it has a renderer
                    Renderer renderer = lowPolyWireframe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Debug.Log($"   ✅ Has Renderer with material: {renderer.material.name}");
                        Debug.Log($"   ✅ Current active state: {lowPolyWireframe.activeInHierarchy}");
                    }
                    else
                    {
                        Debug.LogWarning($"   ⚠️ No Renderer found on {obj.name}");
                    }
                    
                    return;
                }
            }
        }
        
        Debug.LogError("❌ No wireframe model found in entire scene!");
        Debug.Log("Searched for objects named: lowpoly, wireframe, etc.");
        Debug.Log("Make sure your wireframe model exists in the scene and is named 'lowpoly'");
    }
    
    [ContextMenu("Auto-Find High Poly Meshes")]
    public void AutoFindHighPolyMeshes()
    {
        // Look for child objects with renderers
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        System.Collections.Generic.List<GameObject> foundMeshes = new System.Collections.Generic.List<GameObject>();
        
        foreach (Renderer renderer in allRenderers)
        {
            // Skip the wireframe model
            if (lowPolyWireframe != null && renderer.gameObject == lowPolyWireframe)
                continue;
                
            // Look for objects that might be high poly meshes
            string name = renderer.gameObject.name.ToLower();
            if (name.Contains("kip") || name.Contains("mesh") || name.Contains("model"))
            {
                foundMeshes.Add(renderer.gameObject);
                Debug.Log($"🔍 Found potential high poly mesh: {renderer.gameObject.name}");
            }
        }
        
        highPolyMeshes = foundMeshes.ToArray();
        Debug.Log($"✅ Auto-found {highPolyMeshes.Length} high poly meshes");
    }
}