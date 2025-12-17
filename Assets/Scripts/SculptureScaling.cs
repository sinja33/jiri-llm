using UnityEngine;

public class SculptureScaling : MonoBehaviour
{
    [Header("Scaling Settings")]
    [Tooltip("Radius where scaling begins (should be larger than interaction distance)")]
    public float scalingDistance = 50f; // Increased from 20f to 50f
    
    [Tooltip("Minimum scale when player is far away or outside scaling zone")]
    public float minScale = 0.8f; // Increased from 0.5f to 0.8f so it's not too tiny
    
    [Tooltip("Maximum scale when player is very close")]
    public float maxScale = 1.5f; // Reduced from 2.0f to 1.5f for more reasonable scaling
    
    [Tooltip("How smoothly the scaling transitions (higher = smoother)")]
    public float scalingSpeed = 2f;
    
    [Tooltip("Distance at which maximum scale is reached")]
    public float maxScaleDistance = 8f; // Increased from 3f to 8f
    
    [Header("Debug")]
    public bool debugScaling = false;
    
    private Transform playerTransform;
    private Vector3 originalScale;
    private float targetScale;
    private float currentScale;
    
    void Start()
    {
        // Store the original scale of the sculpture
        originalScale = transform.localScale;
        currentScale = minScale;
        targetScale = minScale;
        
        // Set initial scale to minimum
        transform.localScale = originalScale * minScale;
        
        FindPlayer();
        
        if (debugScaling)
            Debug.Log($"Sculpture scaling initialized - Original scale: {originalScale}, Min: {minScale}, Max: {maxScale}");
    }
    
    void FindPlayer()
    {
        // Find the main camera (player)
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0)
                mainCam = cameras[0];
        }
        
        if (mainCam != null)
        {
            playerTransform = mainCam.transform;
            if (debugScaling)
                Debug.Log("Found player camera for scaling: " + mainCam.name);
        }
        else
        {
            Debug.LogError("No camera found for sculpture scaling!");
        }
    }
    
    void Update()
    {
        if (playerTransform != null)
        {
            UpdateScaling();
        }
        else
        {
            // Try to find player again if we lost reference
            FindPlayer();
        }
    }
    
    void UpdateScaling()
    {
        // Calculate 2D distance (ignore Y axis for consistent scaling)
        Vector3 sculpturePos = transform.position;
        Vector3 playerPos = playerTransform.position;
        
        Vector2 sculpturePos2D = new Vector2(sculpturePos.x, sculpturePos.z);
        Vector2 playerPos2D = new Vector2(playerPos.x, playerPos.z);
        float distance2D = Vector2.Distance(sculpturePos2D, playerPos2D);
        
        // Calculate target scale based on distance
        float newTargetScale;
        
        if (distance2D >= scalingDistance)
        {
            // Outside scaling zone - use minimum scale
            newTargetScale = minScale;
        }
        else if (distance2D <= maxScaleDistance)
        {
            // Very close - use maximum scale
            newTargetScale = maxScale;
        }
        else
        {
            // Inside scaling zone - interpolate between min and max
            // Invert the distance so closer = bigger scale
            float normalizedDistance = (distance2D - maxScaleDistance) / (scalingDistance - maxScaleDistance);
            newTargetScale = Mathf.Lerp(maxScale, minScale, normalizedDistance);
        }
        
        // Update target scale
        targetScale = newTargetScale;
        
        // Smoothly interpolate current scale towards target
        currentScale = Mathf.Lerp(currentScale, targetScale, scalingSpeed * Time.deltaTime);
        
        // Apply the scaling
        transform.localScale = originalScale * currentScale;
        
        // Debug information
        if (debugScaling && Time.frameCount % 30 == 0) // Log every 30 frames to avoid spam
        {
            Debug.Log($"Distance: {distance2D:F2}m | Target Scale: {targetScale:F2} | Current Scale: {currentScale:F2}");
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Visual zones removed - no gizmos drawn
    }
    
    // Public methods for external control
    public void SetScalingParameters(float newScalingDistance, float newMinScale, float newMaxScale, float newMaxScaleDistance)
    {
        scalingDistance = newScalingDistance;
        minScale = Mathf.Max(0.1f, newMinScale); // Ensure minimum scale isn't too small
        maxScale = Mathf.Max(minScale, newMaxScale); // Ensure max scale is larger than min
        maxScaleDistance = Mathf.Min(newMaxScaleDistance, scalingDistance); // Ensure max scale distance is within scaling distance
        
        if (debugScaling)
            Debug.Log($"Scaling parameters updated: Distance={scalingDistance}, Min={minScale}, Max={maxScale}, MaxDist={maxScaleDistance}");
    }
    
    public float GetCurrentScale()
    {
        return currentScale;
    }
    
    public float GetTargetScale()
    {
        return targetScale;
    }
    
    public bool IsPlayerInScalingZone()
    {
        if (playerTransform == null) return false;
        
        Vector2 sculpturePos2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 playerPos2D = new Vector2(playerTransform.position.x, playerTransform.position.z);
        float distance2D = Vector2.Distance(sculpturePos2D, playerPos2D);
        
        return distance2D < scalingDistance;
    }
    
    // Method to reset sculpture to original scale
    public void ResetToOriginalScale()
    {
        transform.localScale = originalScale;
        currentScale = 1f;
        targetScale = 1f;
    }
    
    // Method to immediately set scale without interpolation
    public void SetScaleImmediate(float scale)
    {
        scale = Mathf.Clamp(scale, minScale, maxScale);
        currentScale = scale;
        targetScale = scale;
        transform.localScale = originalScale * scale;
    }
}