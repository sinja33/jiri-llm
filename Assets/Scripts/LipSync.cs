using UnityEngine;

public class ULipSyncConnector : MonoBehaviour
{
    [Header("Components")]
    public uLipSync.uLipSync uLipSyncComponent;
    public TextToSpeechManager ttsManager;
    public SpeechManager speechManager;
    
    private AudioSource lipSyncAudioSource;
    
    void Start()
    {
        // Auto-find components
        if (uLipSyncComponent == null)
            uLipSyncComponent = GetComponentInChildren<uLipSync.uLipSync>();
            
        if (ttsManager == null)
            ttsManager = FindObjectOfType<TextToSpeechManager>();
            
        if (speechManager == null)
            speechManager = FindObjectOfType<SpeechManager>();
            
        // Get the audio source that's on the same GameObject as uLipSync
        if (uLipSyncComponent != null)
        {
            lipSyncAudioSource = uLipSyncComponent.GetComponent<AudioSource>();
        }
    }
    
    void Update()
    {
        if (lipSyncAudioSource == null) return;
        
        // Check what's playing and copy it to the lip sync audio source
        if (ttsManager != null && ttsManager.audioSource.isPlaying)
        {
            CopyAudioToLipSync(ttsManager.audioSource);
        }
        else if (speechManager != null && speechManager.bufferAudioSource != null && speechManager.bufferAudioSource.isPlaying)
        {
            CopyAudioToLipSync(speechManager.bufferAudioSource);
        }
        else if (speechManager != null && speechManager.mainAudioSource != null && speechManager.mainAudioSource.isPlaying)
        {
            CopyAudioToLipSync(speechManager.mainAudioSource);
        }
        else if (lipSyncAudioSource.isPlaying)
        {
            // Stop lip sync audio if nothing else is playing
            lipSyncAudioSource.Stop();
        }
    }
    
    void CopyAudioToLipSync(AudioSource sourceAudio)
    {
        if (!lipSyncAudioSource.isPlaying || lipSyncAudioSource.clip != sourceAudio.clip)
        {
            lipSyncAudioSource.clip = sourceAudio.clip;
            lipSyncAudioSource.time = sourceAudio.time;
            lipSyncAudioSource.Play();
        }
    }
}