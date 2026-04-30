using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;

public static class CityUIPrefabTool
{
    private const string PrefabFolder = "Assets/Prefabs/CityUI";

    private static readonly string[] PrefabTargets =
    {
        "InfoCanvas",
        "FadeCanvas",
        "ToastManager",
        "DealerManager",
        "HeatManager",
        "DebtManager",
        "TravelManager",
        "MarketNewsTicker",
        "EquipmentShop",
    };

    private static readonly string[] CitySceneNames =
        { "Milwaukee", "Baghdad", "Belgrade", "Miami", "Toronto", "Tokyo" };

    private static string PrefabPath(string name) => $"{PrefabFolder}/{name}.prefab";

    // ───────────────────────────────────────────────
    // Step 1 — Save prefabs from the current scene
    // ───────────────────────────────────────────────
    [MenuItem("Drug Wars/Prefabs/1. Save All CityUI Prefabs From Current Scene")]
    private static void SavePrefabs()
    {
        if (!Directory.Exists(PrefabFolder))
            Directory.CreateDirectory(PrefabFolder);

        int saved = 0;
        foreach (string target in PrefabTargets)
            saved += TrySavePrefab(target);

        if (saved > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("CityUI Prefabs",
                $"Saved {saved} prefab(s) to {PrefabFolder}/.\n\n" +
                "Next: open each other city scene and run\n" +
                "Drug Wars → Prefabs → 2. Replace With Prefab Instances.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("CityUI Prefabs",
                "No matching root GameObjects found in the current scene.\n" +
                $"Expected roots: {string.Join(", ", PrefabTargets)}",
                "OK");
        }
    }

    private static int TrySavePrefab(string rootName)
    {
        var go = FindRootByName(rootName);
        if (go == null)
        {
            Debug.LogWarning($"[CityUIPrefabTool] '{rootName}' not found in scene — skipping.");
            return 0;
        }

        string path = PrefabPath(rootName);
        bool isNew = !File.Exists(path);
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
        Debug.Log($"[CityUIPrefabTool] {(isNew ? "Created" : "Updated")} prefab: {path}");
        return 1;
    }

    // ──────────────────────────────────────────────────────
    // Step 2 — Replace scene roots with prefab instances
    // ──────────────────────────────────────────────────────
    [MenuItem("Drug Wars/Prefabs/2. Replace With Prefab Instances (Current Scene)")]
    private static void ReplaceCurrentScene()
    {
        int replaced = 0;
        foreach (string target in PrefabTargets)
            replaced += TryReplace(target);

        if (replaced > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("CityUI Prefabs",
                $"Replaced {replaced} object(s) with prefab instances.\n\n" +
                "Check Inspector for any per-city overrides\n" +
                "(EquipmentShop inventory, etc.).\n\n" +
                "Remember to save the scene (Cmd+S).",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("CityUI Prefabs",
                "Nothing to replace — either prefabs don't exist yet\n" +
                "or objects are already prefab instances.",
                "OK");
        }
    }

    private static int TryReplace(string rootName)
    {
        string prefabPath = PrefabPath(rootName);
        if (!File.Exists(prefabPath))
        {
            Debug.LogWarning($"[CityUIPrefabTool] Prefab not found: {prefabPath} — run Step 1 first.");
            return 0;
        }

        var existing = FindRootByName(rootName);
        if (existing == null)
        {
            Debug.LogWarning($"[CityUIPrefabTool] '{rootName}' not found in scene — skipping.");
            return 0;
        }

        if (PrefabUtility.IsPartOfPrefabInstance(existing))
        {
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(existing);
            string assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (assetPath == prefabPath)
            {
                Debug.Log($"[CityUIPrefabTool] '{rootName}' is already an instance of {prefabPath} — skipping.");
                return 0;
            }
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[CityUIPrefabTool] Failed to load prefab at {prefabPath}.");
            return 0;
        }

        int siblingIndex = existing.transform.GetSiblingIndex();
        Undo.RegisterCompleteObjectUndo(existing, $"Replace {rootName} with prefab");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = rootName;
        instance.transform.SetSiblingIndex(siblingIndex);

        var oldRT = existing.GetComponent<RectTransform>();
        var newRT = instance.GetComponent<RectTransform>();
        if (oldRT != null && newRT != null)
        {
            newRT.anchorMin = oldRT.anchorMin;
            newRT.anchorMax = oldRT.anchorMax;
            newRT.anchoredPosition = oldRT.anchoredPosition;
            newRT.sizeDelta = oldRT.sizeDelta;
            newRT.pivot = oldRT.pivot;
        }

        Undo.DestroyObjectImmediate(existing);
        Undo.RegisterCreatedObjectUndo(instance, $"Replace {rootName} with prefab");

        Debug.Log($"[CityUIPrefabTool] Replaced '{rootName}' with prefab instance from {prefabPath}.");
        return 1;
    }

    // ─────────────────────────────────
    // Step 3 — Validate all city scenes
    // ─────────────────────────────────
    [MenuItem("Drug Wars/Prefabs/3. Validate All City Scenes")]
    private static void ValidateAllScenes()
    {
        var report = new System.Text.StringBuilder();
        int issues = 0;

        string currentScenePath = SceneManager.GetActiveScene().path;

        foreach (string sceneName in CitySceneNames)
        {
            string scenePath = $"Assets/Scenes/{sceneName}.unity";
            if (!File.Exists(scenePath))
            {
                report.AppendLine($"  \u26a0 {sceneName}: scene file not found");
                issues++;
                continue;
            }

            report.AppendLine($"\n--- {sceneName} ---");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var roots = scene.GetRootGameObjects();

            foreach (string target in PrefabTargets)
                issues += CheckPrefabLink(roots, target, sceneName, report);

            if (scene.path != currentScenePath)
                EditorSceneManager.CloseScene(scene, true);
        }

        string header = issues == 0
            ? "All city scenes are using shared prefabs.\n"
            : $"Found {issues} issue(s):\n";

        report.Insert(0, header);
        EditorUtility.DisplayDialog("CityUI Validation", report.ToString(), "OK");
    }

    private static int CheckPrefabLink(GameObject[] roots, string name,
        string sceneName, System.Text.StringBuilder report)
    {
        string expectedPath = PrefabPath(name);
        var go = roots.FirstOrDefault(r => r.name == name);

        if (go == null)
        {
            report.AppendLine($"  \u26a0 '{name}' not found");
            return 1;
        }

        if (!PrefabUtility.IsPartOfPrefabInstance(go))
        {
            report.AppendLine($"  \u2717 '{name}' is NOT a prefab instance — run Step 2");
            return 1;
        }

        var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
        string actualPath = AssetDatabase.GetAssetPath(source);
        if (actualPath != expectedPath)
        {
            report.AppendLine($"  \u2717 '{name}' linked to wrong prefab: {actualPath}");
            return 1;
        }

        var overrides = PrefabUtility.GetObjectOverrides(go, true);
        int count = overrides.Count;
        string suffix = count > 0 ? $" ({count} override(s))" : "";
        report.AppendLine($"  \u2713 '{name}'{suffix}");

        return 0;
    }

    // ───────────────────────
    // Helpers
    // ───────────────────────
    private static GameObject FindRootByName(string name)
    {
        return SceneManager.GetActiveScene()
            .GetRootGameObjects()
            .FirstOrDefault(go => go.name == name);
    }
}
