using UnityEngine;
using System.Collections;

public class SimpleSeedPlanting : MonoBehaviour
{
    [Header("References")]
    public GameObject snapSphere;
    public GameObject soilPile;
    public GameObject sculpturePrefab;
    public Material invisibleMaterial; // Drag your invisible material here
    public Transform playerMoveTarget; // Where to move the player
    public GameObject lowPoly;
    public GameObject uiBack;

    public GameObject uiTrigger;
    
    [Header("Animation Timing")]
    public float seedSinkTime = 1f;
    public float soilSinkTime = 1f;
    public float sculptureRiseTime = 2f;
    public float playerMoveTime = 1.5f;
    
    [Header("Positioning")]
    public float sculptureLowerAmount = 0.3f; // How much lower than soil surface
    public float requiredDistance = 3f; // How far player must be to start sculpture growth
    
    private Transform playerTransform;
    private bool waitingForPlayer = false;
    
    void Start()
    {
        // Find the player camera or XR rig
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            playerTransform = mainCam.transform;
            
            // Try to find XR Origin/XR Rig instead
            Transform xrRig = mainCam.transform.parent;
            if (xrRig != null && (xrRig.name.Contains("XR") || xrRig.name.Contains("Rig")))
            {
                playerTransform = xrRig;
                Debug.Log("Found XR Rig: " + xrRig.name);
            }
            else
            {
                Debug.Log("Using camera: " + mainCam.name);
            }
        }
        
        // Start with snap zone inactive
        if (snapSphere != null)
        {
            snapSphere.SetActive(false);
        }
    }
    
    public void OnSeedGrabbed()
    {
        Debug.Log("Seed grabbed! Activating snap zone");
        if (snapSphere != null)
        {
            snapSphere.SetActive(true);
        }
    }
    
    public void OnSocketSnap()
    {
        Debug.Log("OnSocketSnap called!");
        StartCoroutine(PlantingAnimation());
    }

    IEnumerator PlantingAnimation()
    {
        Debug.Log("Starting planting animation");

        // 1. Change snap sphere material to invisible immediately
        if (snapSphere != null && invisibleMaterial != null)
        {
            Renderer renderer = snapSphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = invisibleMaterial;
                Debug.Log("Snap sphere made invisible");
            }
        }

        // Wait a second with invisible material
        yield return new WaitForSeconds(1f);

        // 2. Now disable snap sphere completely
        if (snapSphere != null)
        {
            snapSphere.SetActive(false);
            Debug.Log("Snap sphere disabled");
        }

        // 3. Sink seed into soil
        Debug.Log("Sinking seed...");
        Vector3 startPos = transform.position;
        Vector3 sinkPos = startPos + Vector3.down * 0.5f;

        float timer = 0f;
        while (timer < seedSinkTime)
        {
            timer += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, sinkPos, timer / seedSinkTime);
            yield return null;
        }

        GetComponent<Renderer>().material = invisibleMaterial;


        // 4. Sink soil underground
        Vector3 soilStart = soilPile.transform.position;
        Vector3 soilSink = soilStart + Vector3.down * 2f;

        timer = 0f;
        while (timer < soilSinkTime)
        {
            timer += Time.deltaTime;
            soilPile.transform.position = Vector3.Lerp(soilStart, soilSink, timer / soilSinkTime);
            yield return null;
        }

        // 4. Wait for player to step back
        Debug.Log("Waiting for player to step back...");
        waitingForPlayer = true;

        while (waitingForPlayer)
        {
            uiBack.SetActive(true);
            float distance = Vector3.Distance(playerTransform.position, transform.position);
            if (distance >= requiredDistance)
            {
                Debug.Log("Player stepped back! Starting sculpture growth...");
                waitingForPlayer = false;
            }
            yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
        }

        uiBack.SetActive(false);



        // 6. Spawn and raise sculpture (lower than soil surface)
        Vector3 sculptureStart = soilStart + Vector3.down * 2f;
        Vector3 sculptureEnd = soilStart + Vector3.down * sculptureLowerAmount;
        GameObject sculpture = Instantiate(sculpturePrefab, sculptureStart, Quaternion.identity);

        // Add missing speech components if they don't exist
        SpeechManager speechManager = sculpture.GetComponent<SpeechManager>();
        if (speechManager == null)
        {
            speechManager = sculpture.AddComponent<SpeechManager>();
            Debug.Log("Added SpeechManager to sculpture");
        }

        // Add other required components
        if (sculpture.GetComponent<StreamingSpeechToTextManager>() == null)
        {
            sculpture.AddComponent<StreamingSpeechToTextManager>();
            Debug.Log("Added StreamingSpeechToTextManager to sculpture");
        }

        if (sculpture.GetComponent<LLMManager>() == null)
        {
            sculpture.AddComponent<LLMManager>();
            Debug.Log("Added LLMManager to sculpture");
        }

        if (sculpture.GetComponent<TextToSpeechManager>() == null)
        {
            sculpture.AddComponent<TextToSpeechManager>();
            Debug.Log("Added TextToSpeechManager to sculpture");
        }

        // Assign SpeechManager to the sculpture's SculptureInteraction component
        SculptureInteraction sculptureInteraction = sculpture.GetComponent<SculptureInteraction>();
        if (sculptureInteraction != null && speechManager != null)
        {
            sculptureInteraction.speechManager = speechManager;
            Debug.Log("SpeechManager assigned to sculpture!");
        }

        timer = 0f;
        while (timer < sculptureRiseTime)
        {
            timer += Time.deltaTime;
            sculpture.transform.position = Vector3.Lerp(sculptureStart, sculptureEnd, timer / sculptureRiseTime);
            yield return null;
        }

        uiTrigger.SetActive(true);
        Debug.Log("🎤 Trigger canvas shown after sculpture rise");

        // Hide it after 5 seconds
        yield return new WaitForSeconds(5f);
        
        uiTrigger.SetActive(false);
        Debug.Log("🎤 Trigger canvas hidden after 5 seconds");
        
        lowPoly.SetActive(true);
    }
}