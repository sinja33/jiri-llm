using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class EnvironmentLoader
{
    private static Dictionary<string, string> envVars = new Dictionary<string, string>();
    private static bool isLoaded = false;
    
    public static void LoadEnvironmentFile()
    {
        if (isLoaded) return;
        
        string envFilePath = Path.Combine(Application.dataPath, "../.env");
        
        if (!File.Exists(envFilePath))
        {
            Debug.LogWarning("⚠️ .env file not found at: " + envFilePath);
            Debug.LogWarning("Create a .env file in your project root with: OPENAI_API_KEY=your_key");
            isLoaded = true;
            return;
        }
        
        try
        {
            string[] lines = File.ReadAllLines(envFilePath);
            
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                    
                int equalsIndex = line.IndexOf('=');
                if (equalsIndex > 0)
                {
                    string key = line.Substring(0, equalsIndex).Trim();
                    string value = line.Substring(equalsIndex + 1).Trim();
                    
                    // Remove quotes if present
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                        value = value.Substring(1, value.Length - 2);
                    
                    envVars[key] = value;
                    Debug.Log($"✅ Loaded environment variable: {key}");
                }
            }
            
            isLoaded = true;
            Debug.Log($"🔐 Environment file loaded with {envVars.Count} variables");
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Error loading .env file: " + e.Message);
            isLoaded = true;
        }
    }
    
    public static string GetEnvironmentVariable(string key, string fallback = "")
    {
        LoadEnvironmentFile();
        
        if (envVars.ContainsKey(key))
        {
            return envVars[key];
        }
        
        // Fallback to system environment variables
        string systemEnvValue = System.Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(systemEnvValue))
        {
            return systemEnvValue;
        }
        
        if (!string.IsNullOrEmpty(fallback))
        {
            Debug.LogWarning($"⚠️ Environment variable '{key}' not found, using fallback");
            return fallback;
        }
        
        Debug.LogError($"❌ Environment variable '{key}' not found and no fallback provided!");
        return "";
    }
}