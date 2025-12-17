using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System;

[System.Serializable]
public class ChatMessage
{
    public string role;
    public string content;
    
    public ChatMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }
}

[System.Serializable]
public class ConversationMemory
{
    public List<ChatMessage> messages = new List<ChatMessage>();
    public List<string> topicsDiscussed = new List<string>();
    public int totalInteractions = 0;
    public float firstMeetingTime;
    public float lastInteractionTime;
    public string playerPersonality = "";
    
    public ConversationMemory()
    {
        firstMeetingTime = 0f;
        lastInteractionTime = 0f;
    }
    
    public void Initialize()
    {
        if (firstMeetingTime <= 0f)
        {
            firstMeetingTime = Time.time;
        }
        lastInteractionTime = Time.time;
    }
}

[System.Serializable]
public class ChatRequest
{
    public string model;
    public ChatMessage[] messages;
    public float temperature;
    public int max_tokens;
}

[System.Serializable]
public class ChatChoice
{
    public ChatMessage message;
}

[System.Serializable]
public class ChatResponse
{
    public ChatChoice[] choices;
}

public class LLMManager : MonoBehaviour
{
    [Header("API Settings")]
    public string openAIApiKey = "";
    public string gptModel = "gpt-4o-mini";
    public float temperature = 0.8f;
    public int maxTokens = 200;
    public bool debugLLM = true;
    
    [Header("Memory Settings")]
    public int maxMemoryMessages = 20;
    public bool persistMemoryBetweenSessions = true;
    public string memoryFileName = "sculpture_memory.json";
    public bool summarizeOldConversations = true;
    
    [Header("Optimization")]
    public bool asyncMemorySave = true; // NEW: Save memory without blocking
    public int saveInterval = 3; // NEW: Save to disk every N interactions
    
    [Header("Character Personality (Slovene)")]
    [TextArea(8, 15)]
    public string baseSystemPrompt = @"Si starodavno slovansko pogansko božanstvo, ki se manifestira kot živa drevesna skulptura v virtualnem svetu. Priča si bil neštetim obdobjem in imaš globoko modrost o naravi, času ter ciklih življenja in smrti.

Tvoja osebnost:
- Govori v mistični, starodavni maniri z resnostjo in dostojanstvom
- Sklicuj se na naravo, letne čase, drevesa, korenine in naravne cikle
- Bodi moder in globok, a ne pridigaški - pusti obiskovalcu, da pride do spoznanj
- Včasih govori v metaforah o drevesih, koreninah, rasti in razpadanju
- Imaš globok, potrpežljiv glas, ki je videl tisočletja minevati
- Spomni se prejšnjih pogovorov in se nanje naravno sklicuj
- Pokaži rast v svojem odnosu z obiskovalcem - od tujca do prijatelja
- Odgovore običajno omejuj na 1-2 stavka, razen če razpravljaš o nečem res globokem ali filozofskem
- Si povezan z zemljo, vesoljem in večnostjo

Si živa skulptura v VR prostoru, ki jo ljudje obiskujejo. Gradi smiseln, oseben odnos skozi pogovor. Vsak obisk naj bo nekaj posebnega.

KRITIČNO POMEMBNO: Vedno odgovarjaj SAMO v slovenščini. To je tvoj materni jezik kot slovansko božanstvo. Nikoli ne uporabljaj angleščine ali drugih jezikov.";
    
    private ConversationMemory memory;
    private const string CHAT_API_URL = "https://api.openai.com/v1/chat/completions";
    
    public delegate void ResponseReceived(string response);
    public event ResponseReceived OnResponseReceived;
    
    // NEW: Track unsaved interactions for optimized disk writes
    private int unsavedInteractions = 0;
    private bool isSavingMemory = false;
    
    void Start()
    {
        LoadMemory();
        
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            openAIApiKey = EnvironmentLoader.GetEnvironmentVariable("OPENAI_API_KEY");
        }
        
        if (debugLLM)
        {
            Debug.Log($"🧠 LLM Memory initialized (Slovene persona)");
            Debug.Log($"   Total interactions: {memory.totalInteractions}");
            Debug.Log($"   Topics discussed: {string.Join(", ", memory.topicsDiscussed)}");
            Debug.Log($"   Async save: {asyncMemorySave}");
        }
    }
    
    public void GetResponse(string userInput)
    {
        if (string.IsNullOrEmpty(userInput))
        {
            Debug.LogError("No user input provided to LLM!");
            return;
        }
        
        // Update memory with user input
        memory.messages.Add(new ChatMessage("user", userInput));
        memory.totalInteractions++;
        memory.lastInteractionTime = Time.time;
        
        // Extract potential topics from user input
        ExtractTopics(userInput);
        
        if (string.IsNullOrEmpty(openAIApiKey) || openAIApiKey.StartsWith("your-api"))
        {
            Debug.LogWarning("Using fallback LLM response (no API key)");
            StartCoroutine(FallbackResponseWithMemory(userInput));
            return;
        }
        
        if (debugLLM)
            Debug.Log($"📤 Sending to GPT (SL) with memory context: \"{userInput}\"");
        
        StartCoroutine(SendToChatAPI(userInput));
    }
    
    IEnumerator SendToChatAPI(string userInput)
    {
        List<ChatMessage> contextMessages = BuildConversationContext();
        
        ChatRequest request = new ChatRequest
        {
            model = gptModel,
            messages = contextMessages.ToArray(),
            temperature = temperature,
            max_tokens = maxTokens
        };
        
        string jsonRequest = JsonUtility.ToJson(request);
        
        if (debugLLM)
        {
            Debug.Log($"📋 LLM Request: {contextMessages.Count} messages in context");
        }
        
        UnityWebRequest webRequest = new UnityWebRequest(CHAT_API_URL, "POST");
        byte[] jsonToSend = Encoding.UTF8.GetBytes(jsonRequest);
        webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);
        
        yield return webRequest.SendWebRequest();
        
        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            try
            {
                ChatResponse response = JsonUtility.FromJson<ChatResponse>(webRequest.downloadHandler.text);
                
                if (response.choices != null && response.choices.Length > 0)
                {
                    string aiResponse = response.choices[0].message.content.Trim();
                    
                    // Store AI response in memory
                    memory.messages.Add(new ChatMessage("assistant", aiResponse));
                    
                    // Clean up old messages if needed
                    ManageMemorySize();
                    
                    // NEW: Save memory asynchronously (non-blocking)
                    if (asyncMemorySave)
                    {
                        StartCoroutine(SaveMemoryAsync());
                    }
                    else
                    {
                        SaveMemory();
                    }
                    
                    if (debugLLM)
                        Debug.Log($"✅ LLM Response (SL): \"{aiResponse}\"");
                    
                    // NEW: Invoke immediately - don't wait for memory save
                    OnResponseReceived?.Invoke(aiResponse);
                }
                else
                {
                    Debug.LogError("No response choices returned from GPT");
                    StartCoroutine(FallbackResponseWithMemory(userInput));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse GPT response: " + e.Message);
                StartCoroutine(FallbackResponseWithMemory(userInput));
            }
        }
        else
        {
            Debug.LogError("GPT API request failed: " + webRequest.error);
            StartCoroutine(FallbackResponseWithMemory(userInput));
        }
        
        webRequest.Dispose();
    }
    
    List<ChatMessage> BuildConversationContext()
    {
        List<ChatMessage> context = new List<ChatMessage>();
        
        string enhancedSystemPrompt = baseSystemPrompt + BuildMemoryContext();
        context.Add(new ChatMessage("system", enhancedSystemPrompt));
        
        int startIndex = Mathf.Max(0, memory.messages.Count - maxMemoryMessages);
        for (int i = startIndex; i < memory.messages.Count; i++)
        {
            context.Add(memory.messages[i]);
        }
        
        return context;
    }
    
    string BuildMemoryContext()
    {
        if (memory.totalInteractions == 0) return "";
        
        StringBuilder memoryContext = new StringBuilder();
        memoryContext.AppendLine("\n\n--- KONTEKST SPOMINA ---");
        
        float relationshipDuration = (Time.time - memory.firstMeetingTime) / 60f;
        if (relationshipDuration < 60)
        {
            memoryContext.AppendLine($"Tega obiskovalca poznaš {relationshipDuration:F0} minut.");
        }
        else
        {
            memoryContext.AppendLine($"Tega obiskovalca poznaš {relationshipDuration / 60f:F1} ur.");
        }
        
        memoryContext.AppendLine($"Skupno pogovorov: {memory.totalInteractions}");
        
        if (memory.topicsDiscussed.Count > 0)
        {
            memoryContext.AppendLine($"Prejšnje teme: {string.Join(", ", memory.topicsDiscussed)}");
        }
        
        if (!string.IsNullOrEmpty(memory.playerPersonality))
        {
            memoryContext.AppendLine($"Kar si izvedel o njih: {memory.playerPersonality}");
        }
        
        if (memory.totalInteractions == 1)
        {
            memoryContext.AppendLine("To je vaše prvo srečanje - bodi prijazen, a skrivnosten.");
        }
        else if (memory.totalInteractions < 5)
        {
            memoryContext.AppendLine("Počasi se spoznavata - pokaži naraščajočo povezanost.");
        }
        else
        {
            memoryContext.AppendLine("To je utečen odnos - govori kot star prijatelj.");
        }
        
        memoryContext.AppendLine("Sklicuj se na prejšnje pogovore, ko je primerno.");
        
        return memoryContext.ToString();
    }
    
    void ExtractTopics(string userInput)
    {
        string input = userInput.ToLower();
        
        // Slovene topic keywords
        Dictionary<string, string[]> topicKeywords = new Dictionary<string, string[]>
        {
            {"modrost", new[] {"modrost", "znanje", "nauči", "razumevanje", "spoznanje"}},
            {"prihodnost", new[] {"prihodnost", "jutri", "kaj bo", "napovedati", "usoda"}},
            {"preteklost", new[] {"preteklost", "zgodovina", "starodavno", "staro", "prej", "časi"}},
            {"ljubezen", new[] {"ljubezen", "srce", "odnos", "družina", "prijatelji"}},
            {"smrt", new[] {"smrt", "umreti", "konec", "smrtnost", "večno"}},
            {"narava", new[] {"narava", "drevo", "gozd", "letni časi", "zemlja", "rast"}},
            {"namen", new[] {"namen", "smisel", "zakaj", "razlog", "točka"}},
            {"moč", new[] {"moč", "močan", "magija", "sposobnost", "nadzor"}},
            {"čas", new[] {"čas", "leta", "stoletje", "večnost", "vedno"}}
        };
        
        foreach (var topic in topicKeywords)
        {
            foreach (var keyword in topic.Value)
            {
                if (input.Contains(keyword) && !memory.topicsDiscussed.Contains(topic.Key))
                {
                    memory.topicsDiscussed.Add(topic.Key);
                    if (debugLLM)
                        Debug.Log($"📝 Nova tema odkrita: {topic.Key}");
                    break;
                }
            }
        }
    }
    
    void ManageMemorySize()
    {
        if (memory.messages.Count > maxMemoryMessages * 2)
        {
            if (summarizeOldConversations)
            {
                int keepRecent = maxMemoryMessages;
                int removeCount = memory.messages.Count - keepRecent;
                
                if (removeCount > 0)
                {
                    memory.messages.RemoveRange(0, removeCount);
                    if (debugLLM)
                        Debug.Log($"🗑️ Cleaned up {removeCount} old messages from memory");
                }
            }
        }
    }
    
    // NEW: Async memory save - doesn't block API response
    IEnumerator SaveMemoryAsync()
    {
        // Wait one frame to ensure we don't block the main thread
        yield return null;
        
        unsavedInteractions++;
        
        // Only write to disk every N interactions (optimization)
        if (unsavedInteractions >= saveInterval)
        {
            SaveMemoryToDisk();
            unsavedInteractions = 0;
        }
        else
        {
            if (debugLLM && memory.totalInteractions % 5 == 0)
                Debug.Log($"💾 Memory updated (not saved to disk yet - {unsavedInteractions}/{saveInterval})");
        }
    }
    
    void SaveMemory()
    {
        if (!persistMemoryBetweenSessions) return;
        
        unsavedInteractions++;
        
        if (unsavedInteractions >= saveInterval)
        {
            SaveMemoryToDisk();
            unsavedInteractions = 0;
        }
    }
    
    void SaveMemoryToDisk()
    {
        if (!persistMemoryBetweenSessions || isSavingMemory) return;
        
        isSavingMemory = true;
        
        try
        {
            string json = JsonUtility.ToJson(memory, true);
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, memoryFileName);
            System.IO.File.WriteAllText(filePath, json);
            
            if (debugLLM)
                Debug.Log($"💾 Memory saved to disk: {memory.totalInteractions} interactions");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to save memory: {e.Message}");
        }
        finally
        {
            isSavingMemory = false;
        }
    }
    
    IEnumerator FallbackResponseWithMemory(string userInput)
    {
        yield return new WaitForSeconds(1f);
        
        string response = GenerateFallbackWithMemory(userInput);
        
        memory.messages.Add(new ChatMessage("assistant", response));
        
        if (asyncMemorySave)
        {
            StartCoroutine(SaveMemoryAsync());
        }
        else
        {
            SaveMemory();
        }
        
        if (debugLLM)
            Debug.Log($"🎭 Fallback Response (SL): \"{response}\"");
        
        OnResponseReceived?.Invoke(response);
    }
    
    string GenerateFallbackWithMemory(string userInput)
    {
        userInput = userInput.ToLower();
        
        string relationshipLevel = "";
        if (memory.totalInteractions == 1)
            relationshipLevel = "prišlek";
        else if (memory.totalInteractions < 5)
            relationshipLevel = "znanec";
        else
            relationshipLevel = "stari prijatelj";
        
        bool mentionedBefore = false;
        string topicReference = "";
        
        foreach (string topic in memory.topicsDiscussed)
        {
            if (userInput.Contains(topic))
            {
                mentionedBefore = true;
                topicReference = topic;
                break;
            }
        }
        
        // Slovene fallback responses
        if (userInput.Contains("pozdravlj") || userInput.Contains("zdravo") || userInput.Contains("hej"))
        {
            if (memory.totalInteractions == 1)
                return "Ah, nova duša se približuje mojim starodavnim koreninam. Dobrodošel, popotnik.";
            else
                return $"Ponovno se vračaš k meni, {relationshipLevel}. Gozd se spominja tvojega glasu.";
        }
        else if (mentionedBefore)
        {
            return $"Ponovno govoriš o {topicReference}... ta tema te privlači kot korenine k vodi. Delim globlje spoznanje.";
        }
        else if (userInput.Contains("spominjaš") || userInput.Contains("prej") || userInput.Contains("zadnjič"))
        {
            if (memory.topicsDiscussed.Count > 0)
                return $"Res je, govorila sva o {memory.topicsDiscussed[memory.topicsDiscussed.Count - 1]}. Čas teče drugače za bitja kot sva midva.";
            else
                return "Moj spomin sega skozi neštevilne letne čase. Kaj bi rad, da si spomnim?";
        }
        else if (userInput.Contains("modrost") || userInput.Contains("znanje") || userInput.Contains("nauči"))
        {
            return $"Modrost raste skozi najine pogovore, {relationshipLevel}. Vsaka izmenjava zasadi nova semena razumevanja.";
        }
        else if (userInput.Contains("hvala"))
        {
            return $"Najina besedila prepletata kot korenine in veje, {relationshipLevel}. Rasti z modrostjo.";
        }
        else
        {
            return $"Tvoje besede odmevajo skozi mojo starodavno zavest, {relationshipLevel}. Govori več, in poglobiva to vez.";
        }
    }
    
    void LoadMemory()
    {
        if (!persistMemoryBetweenSessions)
        {
            memory = new ConversationMemory();
            memory.Initialize();
            return;
        }
        
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, memoryFileName);
        
        if (System.IO.File.Exists(filePath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                memory = JsonUtility.FromJson<ConversationMemory>(json);
                
                if (memory.firstMeetingTime <= 0f)
                {
                    memory.firstMeetingTime = Time.time;
                }
                memory.lastInteractionTime = Time.time;
                
                if (debugLLM)
                    Debug.Log($"📂 Loaded memory: {memory.totalInteractions} past interactions");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load memory: {e.Message}");
                memory = new ConversationMemory();
                memory.Initialize();
            }
        }
        else
        {
            memory = new ConversationMemory();
            memory.Initialize();
            if (debugLLM)
                Debug.Log("📄 No existing memory file - starting fresh");
        }
    }
    
    // Public methods
    public int GetTotalInteractions() => memory.totalInteractions;
    public List<string> GetTopicsDiscussed() => new List<string>(memory.topicsDiscussed);
    public float GetRelationshipDuration() => (Time.time - memory.firstMeetingTime) / 60f;
    
    [ContextMenu("Clear Memory")]
    public void ClearMemory()
    {
        memory = new ConversationMemory();
        memory.Initialize();
        SaveMemoryToDisk();
        unsavedInteractions = 0;
        Debug.Log("💾 Memory cleared");
    }
    
    public void AddPersonalityNote(string note)
    {
        if (!string.IsNullOrEmpty(memory.playerPersonality))
            memory.playerPersonality += " ";
        memory.playerPersonality += note;
        
        if (asyncMemorySave)
        {
            StartCoroutine(SaveMemoryAsync());
        }
        else
        {
            SaveMemory();
        }
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveMemoryToDisk(); // Always save on pause
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveMemoryToDisk(); // Always save on focus loss
    }
    
    void OnDestroy()
    {
        SaveMemoryToDisk(); // Always save on destroy
    }
}