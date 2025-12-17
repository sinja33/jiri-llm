using UnityEngine;

public class ApplyWireframeShader : MonoBehaviour
{
    [Header("Wireframe Colors")]
    public Color frontColor = Color.white;
    public Color backColor = Color.gray;
    
    [Header("Wireframe Settings")]
    [Range(0.01f, 0.2f)]
    public float wireframeWidth = 0.05f;
    
    [Header("Options")]
    public bool applyOnStart = true;
    
    private Material wireframeMaterial;
    private Renderer objectRenderer;
    
    void Start()
    {
        if (applyOnStart)
        {
            ApplyWireframe();
        }
    }
    
    public void ApplyWireframe()
    {
        objectRenderer = GetComponent<Renderer>();
        
        if (objectRenderer == null)
        {
            Debug.LogError("No Renderer found on " + gameObject.name);
            return;
        }
        
        // Find the wireframe shader
        Shader wireframeShader = Shader.Find("Unlit/WireframeSimple");
        
        if (wireframeShader == null)
        {
            Debug.LogError("Wireframe shader 'Unlit/WireframeSimple' not found! Make sure you've created the shader file.");
            return;
        }
        
        // Create material with the wireframe shader
        wireframeMaterial = new Material(wireframeShader);
        
        // Set the properties
        wireframeMaterial.SetColor("_WireframeFrontColour", frontColor);
        wireframeMaterial.SetColor("_WireframeBackColour", backColor);
        wireframeMaterial.SetFloat("_WireframeWidth", wireframeWidth);
        
        wireframeMaterial.name = "Wireframe Material";
        
        // Apply to the object
        objectRenderer.material = wireframeMaterial;
        
        Debug.Log("✓ Wireframe shader applied to " + gameObject.name);
    }
    
    void Update()
    {
        // Update material properties in real-time
        if (wireframeMaterial != null)
        {
            wireframeMaterial.SetColor("_WireframeFrontColour", frontColor);
            wireframeMaterial.SetColor("_WireframeBackColour", backColor);
            wireframeMaterial.SetFloat("_WireframeWidth", wireframeWidth);
        }
    }
    
    // Public methods to change settings at runtime
    public void SetFrontColor(Color color)
    {
        frontColor = color;
        Debug.Log("Front wireframe color changed to: " + color);
    }
    
    public void SetBackColor(Color color)
    {
        backColor = color;
        Debug.Log("Back wireframe color changed to: " + color);
    }
    
    public void SetWireframeWidth(float width)
    {
        wireframeWidth = Mathf.Clamp(width, 0.01f, 0.2f);
        Debug.Log("Wireframe width changed to: " + wireframeWidth);
    }
    
    // Context menu for easy testing
    [ContextMenu("Apply Wireframe")]
    void ApplyWireframeFromMenu()
    {
        ApplyWireframe();
    }
    
    void OnDestroy()
    {
        if (wireframeMaterial != null)
        {
            DestroyImmediate(wireframeMaterial);
        }
    }
}