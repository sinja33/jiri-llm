using UnityEngine;
using UnityEngine.XR;
using System.Collections;

public class UICanvasManager : MonoBehaviour
{
    [Header("UI Canvases")]
    [Tooltip("Welcome screen with instructions")]
    public Canvas startCanvas;
    
    [Tooltip("Legend/info canvas (toggle with B button)")]
    public Canvas legendCanvas;
    
    [Header("VR Input")]
    [Tooltip("Which controller to monitor for B button")]
    public XRNode controllerHand = XRNode.RightHand;
    
    [Header("Timing Settings")]
    [Tooltip("How long to show start canvas")]
    public float startCanvasDuration = 5f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    // Private variables
    private UnityEngine.XR.InputDevice targetController;
    private bool hasValidController = false;
    private bool legendCanvasVisible = false;
    private bool wasBButtonPressed = false;
    private bool startCanvasShown = false;
    
    void Start()
    {
        InitializeSystem();
        InitializeVRInput();
        
        // Show start canvas immediately
        ShowStartCanvas();
        
        if (showDebugInfo)
        {
            Debug.Log("📱 UI Canvas Manager initialized");
            Debug.Log($"   Start Canvas: {(startCanvas != null)}");
            Debug.Log($"   Legend Canvas: {(legendCanvas != null)}");
        }
    }
    
    void InitializeSystem()
    {
        // Initially hide both canvases
        SetCanvasActive(startCanvas, false);
        SetCanvasActive(legendCanvas, false);
    }
    
    void InitializeVRInput()
    {
        // Try to get the specified controller
        var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesAtXRNode(controllerHand, devices);
        
        if (devices.Count > 0)
        {
            targetController = devices[0];
            hasValidController = true;
            if (showDebugInfo)
                Debug.Log($"🎮 Found VR controller: {targetController.name} on {controllerHand}");
        }
        else
        {
            hasValidController = false;
            if (showDebugInfo)
                Debug.LogWarning($"⚠️ No VR controller found on {controllerHand}");
        }
    }
    
    void Update()
    {
        HandleVRInput();
    }
    
    void HandleVRInput()
    {
        if (!hasValidController || !targetController.isValid)
        {
            // Try to reconnect controller every 2 seconds
            if (Time.frameCount % 120 == 0)
            {
                InitializeVRInput();
            }
            return;
        }
        
        // Check B button (secondary button)
        bool bButtonPressed = false;
        targetController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bButtonPressed);
        
        // Handle B button press (toggle legend canvas)
        if (bButtonPressed && !wasBButtonPressed)
        {
            ToggleLegendCanvas();
        }
        
        wasBButtonPressed = bButtonPressed;
    }
    
    // === START CANVAS METHODS ===
    
    public void ShowStartCanvas()
    {
        if (startCanvasShown) return;
        
        SetCanvasActive(startCanvas, true);
        startCanvasShown = true;
        
        if (showDebugInfo)
            Debug.Log("📱 Showing start canvas");
        
        // Auto-hide after duration
        StartCoroutine(HideStartCanvasAfterDelay());
    }
    
    IEnumerator HideStartCanvasAfterDelay()
    {
        yield return new WaitForSeconds(startCanvasDuration);
        SetCanvasActive(startCanvas, false);
        
        if (showDebugInfo)
            Debug.Log("📱 Auto-hiding start canvas");
    }
    
    // === LEGEND CANVAS METHODS ===
    
    public void ToggleLegendCanvas()
    {
        legendCanvasVisible = !legendCanvasVisible;
        SetCanvasActive(legendCanvas, legendCanvasVisible);
        
        if (showDebugInfo)
            Debug.Log($"📱 Legend canvas {(legendCanvasVisible ? "shown" : "hidden")} (B button pressed)");
    }
    
    // === UTILITY METHODS ===
    
    void SetCanvasActive(Canvas canvas, bool active)
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(active);
        }
    }
    
    // === PUBLIC METHODS ===
    
    public void ShowLegendCanvas()
    {
        legendCanvasVisible = true;
        SetCanvasActive(legendCanvas, true);
    }
    
    public void HideLegendCanvas()
    {
        legendCanvasVisible = false;
        SetCanvasActive(legendCanvas, false);
    }
    
    // === MANUAL CONTROLS (for testing) ===
    
    [ContextMenu("Test Start Canvas")]
    public void TestStartCanvas()
    {
        ShowStartCanvas();
    }
    
    [ContextMenu("Test Toggle Legend")]
    public void TestToggleLegend()
    {
        ToggleLegendCanvas();
    }
    
    [ContextMenu("Hide All Canvases")]
    public void HideAllCanvases()
    {
        SetCanvasActive(startCanvas, false);
        SetCanvasActive(legendCanvas, false);
        legendCanvasVisible = false;
        
        Debug.Log("📱 All canvases hidden");
    }
    
    // === GETTERS ===
    
    public bool IsLegendVisible() => legendCanvasVisible;
    public bool HasValidController() => hasValidController;
    public bool IsStartCanvasShown() => startCanvasShown;
}