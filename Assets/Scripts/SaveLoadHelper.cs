using UnityEngine;

// WebGL-safe save/load using PlayerPrefs (browser localStorage).
public static class SaveLoadHelper
{
    private const string SaveKey = "DrugWarsSave";

    public static void WriteToDisk(SaveData data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: false);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log("[SaveLoad] Saved.");
    }

    public static SaveData ReadFromDisk()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            Debug.Log("[SaveLoad] No save found.");
            return null;
        }
        string json = PlayerPrefs.GetString(SaveKey);
        Debug.Log("[SaveLoad] Loaded.");
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static bool SaveExists() => PlayerPrefs.HasKey(SaveKey);

    public static void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Debug.Log("[SaveLoad] Save deleted.");
    }
}
