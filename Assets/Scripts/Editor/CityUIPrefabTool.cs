using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
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
        "TravelUIParent",
        "CityUIHandler",
        "DealerContainer",
        "CityPreviewCard",
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

    // ──────────────────────────────────────────────────────────
    // Step 4 — Auto-wire cross-prefab references in the scene
    // ──────────────────────────────────────────────────────────
    [MenuItem("Drug Wars/Prefabs/4. Auto-Wire Cross References (Current Scene)")]
    private static void AutoWire()
    {
        int wired = 0;
        var log = new System.Text.StringBuilder();

        var infoCanvas = FindRootByName("InfoCanvas");
        var travelMgrGO = FindRootByName("TravelManager");
        var travelUI = FindRootByName("TravelUIParent");
        var heatMgrGO = FindRootByName("HeatManager");
        var debtMgrGO = FindRootByName("DebtManager");
        var dealerMgrGO = FindRootByName("DealerManager");
        var equipShopGO = FindRootByName("EquipmentShop");
        var cityUIGO = FindRootByName("CityUIHandler");
        var dealerContainer = FindRootByName("DealerContainer");
        var mapCanvas = FindRootByName("MapCanvas");
        var previewCard = FindRootByName("CityPreviewCard");

        // ── CityUIHandler ──
        if (cityUIGO != null && infoCanvas != null)
        {
            var comp = cityUIGO.GetComponent<CityUIHandler>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                wired += Wire(so, "playerName", FindDeep<TMP_Text>(infoCanvas, "PlayerName"), log);
                wired += Wire(so, "playerWallet", FindDeep<TMP_Text>(infoCanvas, "WalletText"), log);
                wired += Wire(so, "playerImage", FindDeep<Image>(infoCanvas, "PlayerImage"), log);
                wired += Wire(so, "trenchSlotsText", FindDeep<TMP_Text>(infoCanvas, "TrenchSlots"), log);
                wired += Wire(so, "trenchArmorText", FindDeep<TMP_Text>(infoCanvas, "TrenchArmor"), log);
                wired += Wire(so, "trenchImage", FindDeep<Image>(infoCanvas, "TrenchImage"), log);
                wired += Wire(so, "weaponDamageText", FindDeep<TMP_Text>(infoCanvas, "WeaponDamage"), log);
                wired += Wire(so, "weaponImage", FindDeep<Image>(infoCanvas, "WeaponImage"), log);
                wired += Wire(so, "cityNameText", FindDeep<TMP_Text>(infoCanvas, "CityName"), log);
                wired += Wire(so, "cityPopulationText", FindDeep<TMP_Text>(infoCanvas, "Population"), log);
                wired += Wire(so, "cityFavoriteDrugText", FindDeep<TMP_Text>(infoCanvas, "FavoriteDrug"), log);
                wired += Wire(so, "debtText", FindDeep<TMP_Text>(infoCanvas, "DebtText"), log);
                wired += Wire(so, "dayText", FindDeep<TMP_Text>(infoCanvas, "DayCounterText"), log);
                wired += Wire(so, "netWorthText", FindDeep<TMP_Text>(infoCanvas, "NetWorthText"), log);
                if (travelMgrGO != null)
                    wired += Wire(so, "travelManager", travelMgrGO.GetComponent<TravelManager>(), log);
                so.ApplyModifiedProperties();
                log.AppendLine("  CityUIHandler: done");
            }

            // TooltipUI also lives on the CityUIHandler GameObject
            var tooltipUI = cityUIGO.GetComponent<TooltipUI>();
            if (tooltipUI != null)
            {
                var tso = new SerializedObject(tooltipUI);
                // TooltipPanel prefab instance — find by name in scene
                var tooltipPanelRoot = FindRootByName("TooltipPanel");
                if (tooltipPanelRoot != null)
                {
                    var innerPanel = FindDeepGO(tooltipPanelRoot, "TooltipPanel");
                    if (innerPanel != null && innerPanel != tooltipPanelRoot)
                        wired += Wire(tso, "tooltipPanel", innerPanel, log);
                    else
                        wired += Wire(tso, "tooltipPanel", tooltipPanelRoot, log);
                    wired += Wire(tso, "itemNameText", FindDeep<TMP_Text>(tooltipPanelRoot, "DealerName"), log);
                    wired += Wire(tso, "itemDescriptionText", FindDeep<TMP_Text>(tooltipPanelRoot, "DealerDescription"), log);
                }
                // canvasRectTransform must be the TooltipCanvas (the canvas the tooltip renders on)
                if (tooltipPanelRoot != null)
                {
                    var tooltipCanvas = FindDeepTransform(tooltipPanelRoot, "TooltipCanvas");
                    if (tooltipCanvas != null)
                        wired += Wire(tso, "canvasRectTransform", tooltipCanvas.GetComponent<RectTransform>(), log);
                }
                tso.ApplyModifiedProperties();
                log.AppendLine("  TooltipUI: done");
            }
        }

        // ── HeatManager ──
        if (heatMgrGO != null && infoCanvas != null)
        {
            var comp = heatMgrGO.GetComponent<HeatManager>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                var slider = FindDeep<Slider>(infoCanvas, "HeatSlider");
                wired += Wire(so, "heatSlider", slider, log);
                wired += Wire(so, "heatText", FindDeep<TMP_Text>(infoCanvas, "HeatText"), log);
                wired += Wire(so, "heatStatusText", FindDeep<TMP_Text>(infoCanvas, "HeatStatusText"), log);
                wired += Wire(so, "riskLevelText", FindDeep<TMP_Text>(infoCanvas, "RiskLevelText"), log);
                if (slider != null)
                {
                    var fillArea = slider.transform.Find("Fill Area/Fill");
                    if (fillArea != null)
                        wired += Wire(so, "heatFillImage", fillArea.GetComponent<Image>(), log);
                }
                so.ApplyModifiedProperties();
                log.AppendLine("  HeatManager: done");
            }
        }

        // ── DebtManager ──
        if (debtMgrGO != null && infoCanvas != null)
        {
            var comp = debtMgrGO.GetComponent<DebtManager>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                wired += Wire(so, "debtText", FindDeep<TMP_Text>(infoCanvas, "DebtText"), log);
                wired += Wire(so, "dayText", FindDeep<TMP_Text>(infoCanvas, "DayCounterText"), log);
                wired += Wire(so, "interestWarningText", FindDeep<TMP_Text>(infoCanvas, "InterestWarningText"), log);
                wired += Wire(so, "payDebtButton", FindDeep<Button>(infoCanvas, "PayDebtButton"), log);
                wired += Wire(so, "payAmountInput", FindDeep<TMP_InputField>(infoCanvas, "PayAmountInput"), log);
                wired += Wire(so, "borrowButton", FindDeep<Button>(infoCanvas, "BorrowButton"), log);
                wired += Wire(so, "borrowAmountInput", FindDeep<TMP_InputField>(infoCanvas, "BorrowInput"), log);
                wired += Wire(so, "borrowInfoText", FindDeep<TMP_Text>(infoCanvas, "BorrowInfoText"), log);
                so.ApplyModifiedProperties();
                log.AppendLine("  DebtManager: done");
            }
        }

        // ── DealerManager ──
        if (dealerMgrGO != null && infoCanvas != null)
        {
            var comp = dealerMgrGO.GetComponent<DealerManager>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);

                if (cityUIGO != null)
                    wired += Wire(so, "cityUIHandler", cityUIGO.GetComponent<CityUIHandler>(), log);
                wired += Wire(so, "heatManager", heatMgrGO != null ? heatMgrGO.GetComponent<HeatManager>() : null, log);

                var viewportContent = infoCanvas.transform.Find("PlayerInventory/Viewport/Content");
                if (viewportContent != null)
                    wired += Wire(so, "playerInventoryContent", viewportContent, log);

                var dealerInfoPanel = FindDeepGO(infoCanvas, "DealerInfoPanel");
                wired += Wire(so, "dealerInfoPanel", dealerInfoPanel, log);

                wired += Wire(so, "statusText", FindDeep<TMP_Text>(infoCanvas, "StatusMessageText"), log);

                var dealerItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DealerItem.prefab");
                wired += Wire(so, "inventoryItemPrefab", dealerItemPrefab, log);

                var dealerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DealerPrefab.prefab");
                wired += Wire(so, "dealerPrefab", dealerPrefab, log);

                // Dealer spawn points live inside MapCanvas (scene-specific)
                var spawnParent = mapCanvas ?? dealerContainer;
                if (spawnParent != null)
                {
                    var spawnProp = so.FindProperty("dealerSpawnPoints");
                    if (spawnProp != null)
                    {
                        var spawns = new System.Collections.Generic.List<Transform>();
                        for (int i = 1; i <= 6; i++)
                        {
                            var t = FindDeepTransform(spawnParent, $"DealerSpawn{i}");
                            if (t != null) spawns.Add(t);
                        }
                        spawnProp.arraySize = spawns.Count;
                        for (int i = 0; i < spawns.Count; i++)
                            spawnProp.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
                        wired += spawns.Count;
                        log.AppendLine($"    Found {spawns.Count} dealer spawn point(s)");
                    }
                }

                so.ApplyModifiedProperties();
                log.AppendLine("  DealerManager: done");
            }
        }

        // ── EquipmentShop ──
        if (equipShopGO != null && infoCanvas != null)
        {
            var comp = equipShopGO.GetComponent<EquipmentShop>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                wired += Wire(so, "shopPanel", FindDeepGO(infoCanvas, "ShopPanel"), log);
                wired += Wire(so, "openShopButton", FindDeep<Button>(infoCanvas, "OpenShopBUtton"), log);
                // Try alternate spelling in case it was fixed
                if (FindDeep<Button>(infoCanvas, "OpenShopBUtton") == null)
                    wired += Wire(so, "openShopButton", FindDeep<Button>(infoCanvas, "OpenShopButton"), log);
                wired += Wire(so, "closeShopButton", FindDeep<Button>(infoCanvas, "CloseButton"), log);
                wired += Wire(so, "itemListContent", FindDeepTransform(infoCanvas, "ItemListContent"), log);
                wired += Wire(so, "feedbackText", FindDeep<TMP_Text>(infoCanvas, "FeedbackText"), log);
                if (cityUIGO != null)
                    wired += Wire(so, "cityUIHandler", cityUIGO.GetComponent<CityUIHandler>(), log);

                var shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ShopItemPrefab.prefab");
                wired += Wire(so, "shopItemPrefab", shopPrefab, log);

                so.ApplyModifiedProperties();
                log.AppendLine("  EquipmentShop: done");
            }
        }

        // ── TravelManager ──
        if (travelMgrGO != null)
        {
            var comp = travelMgrGO.GetComponent<TravelManager>();
            if (comp != null)
            {
                var so = new SerializedObject(comp);
                if (travelUI != null)
                {
                    wired += Wire(so, "travelUIParent", travelUI, log);
                    wired += Wire(so, "cityDropdown", FindDeep<TMP_Dropdown>(travelUI, "Dropdown"), log);
                    wired += Wire(so, "travelButton", FindDeep<Button>(travelUI, "Button"), log);
                    wired += Wire(so, "travelCostText", FindDeep<TMP_Text>(travelUI, "FareText"), log);
                }
                if (previewCard != null)
                {
                    wired += Wire(so, "cityPreviewPanel", previewCard, log);
                    wired += Wire(so, "previewCityName", FindDeep<TMP_Text>(previewCard, "PreviewCityName"), log);
                    wired += Wire(so, "previewPopulation", FindDeep<TMP_Text>(previewCard, "PreviewPopulation"), log);
                    wired += Wire(so, "previewCOL", FindDeep<TMP_Text>(previewCard, "PreviewCOL"), log);
                    wired += Wire(so, "previewFavDrug", FindDeep<TMP_Text>(previewCard, "PreviewFavDrug"), log);
                }
                so.ApplyModifiedProperties();
                log.AppendLine("  TravelManager: done");
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        string msg = wired > 0
            ? $"Wired {wired} reference(s).\n\nRemember to save (Cmd+S).\n\n{log}"
            : $"No references wired. Check console for warnings.\n\n{log}";
        EditorUtility.DisplayDialog("Auto-Wire", msg, "OK");
    }

    // ─────────────────────────────────
    // Step 5 — Validate all city scenes
    // ─────────────────────────────────
    [MenuItem("Drug Wars/Prefabs/5. Validate All City Scenes")]
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

    private static T FindDeep<T>(GameObject root, string childName) where T : Component
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<T>(true);
        return all.FirstOrDefault(c => c.gameObject.name == childName);
    }

    private static GameObject FindDeepGO(GameObject root, string childName)
    {
        if (root == null) return null;
        return FindInChildren(root.transform, childName)?.gameObject;
    }

    private static Transform FindDeepTransform(GameObject root, string childName)
    {
        if (root == null) return null;
        return FindInChildren(root.transform, childName);
    }

    private static Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindInChildren(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static int Wire(SerializedObject so, string field, Object value, System.Text.StringBuilder log)
    {
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            log.AppendLine($"    WARN: field '{field}' not found on {so.targetObject.GetType().Name}");
            return 0;
        }
        if (value == null)
        {
            log.AppendLine($"    WARN: no target found for '{field}'");
            return 0;
        }
        prop.objectReferenceValue = value;
        return 1;
    }
}
