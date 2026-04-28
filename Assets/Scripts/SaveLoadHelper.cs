using System.IO;
using UnityEngine;

// Handles reading/writing SaveData to a JSON file in Application.persistentDataPath.
public static class SaveLoadHelper
{
    private const string FileName = "drugwars_save.json";

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static void WriteToDisk(SaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[SaveLoad] Saved to {SavePath}");
    }

    public static SaveData ReadFromDisk()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[SaveLoad] No save file found.");
            return null;
        }

        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        Debug.Log("[SaveLoad] Loaded save file.");
        return data;
    }

    public static bool SaveExists() => File.Exists(SavePath);

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("[SaveLoad] Save file deleted.");
        }
    }
}
