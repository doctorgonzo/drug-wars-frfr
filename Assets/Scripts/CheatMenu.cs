using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Dev/test cheat menu. Press ESC anywhere to toggle.
//
// - Auto-spawns at game start via RuntimeInitializeOnLoadMethod (no Editor wiring required).
// - Persists across scene loads (DontDestroyOnLoad).
// - All UI is built from code — no prefab needed.
//
// Wrap the bootstrap method in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to keep cheats out of release builds.
public class CheatMenu : MonoBehaviour
{
    public static CheatMenu Instance { get; private set; }

    private const KeyCode ToggleKey = KeyCode.Escape;
    private const string DefaultPlayerName = "TEST";
    private const int CashCheatAmount = 10000;
    private const string StartingCityName = "Milwaukee";

    private GameObject _root;        // toggles the entire overlay (dimmer + panel)
    private GameObject _panel;
    private TMP_Text _statusLabel;
    private Button _quickStartButton;
    private Button _addCashButton;
    private Button _dropHeatButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("CheatMenu");
        go.AddComponent<CheatMenu>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        SetPanelVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
            SetPanelVisible(!_root.activeSelf);

        if (_root.activeSelf)
            RefreshButtonStates();
    }

    private void SetPanelVisible(bool visible)
    {
        _root.SetActive(visible);
        if (visible)
        {
            UpdateStatus();
            RefreshButtonStates();
        }
    }

    private void RefreshButtonStates()
    {
        bool hasPlayer = PlayerStats.Instance != null;
        if (_addCashButton != null) _addCashButton.interactable = hasPlayer;
        if (_dropHeatButton != null) _dropHeatButton.interactable = hasPlayer;

        bool canQuickStart = GameSessionManager.Instance != null
                          && GameSessionManager.Instance.AllTrenchcoats != null
                          && GameSessionManager.Instance.AllTrenchcoats.Count > 0;
        if (_quickStartButton != null) _quickStartButton.interactable = canQuickStart;
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null) return;
        var ps = PlayerStats.Instance;
        if (ps == null)
        {
            _statusLabel.text = "<i>No active run.</i>";
            return;
        }
        _statusLabel.text = $"<b>{ps.PlayerName}</b>   ${ps.PlayerWallet:N0}   Heat: {Mathf.RoundToInt(ps.CurrentHeat)}   Day: {(GameTime.Instance != null ? GameTime.Instance.Day : 0)}";
    }

    // ---- Cheat actions ----

    private void OnAddCashClicked()
    {
        if (PlayerStats.Instance == null) return;
        PlayerStats.Instance.PlayerWallet += CashCheatAmount;
        PlayerStats.Instance.NotifyInventoryChanged();
        UpdateStatus();
    }

    private void OnDropHeatClicked()
    {
        if (PlayerStats.Instance == null) return;
        PlayerStats.Instance.CurrentHeat = 0f;
        var heatManager = FindObjectOfType<HeatManager>();
        if (heatManager != null) heatManager.UpdateHeatDisplay();
        UpdateStatus();
    }

    private void OnQuickStartClicked()
    {
        var gsm = GameSessionManager.Instance;
        if (gsm == null) return;

        var trench = gsm.AllTrenchcoats != null && gsm.AllTrenchcoats.Count > 0 ? gsm.AllTrenchcoats[0] : null;
        var weapon = gsm.AllWeapons != null && gsm.AllWeapons.Count > 0 ? gsm.AllWeapons[0] : null;
        var sprite = gsm.PlayerSprites != null && gsm.PlayerSprites.Count > 0 ? gsm.PlayerSprites[0] : null;
        var startCity = gsm.FindCityByName(StartingCityName)
            ?? (gsm.AllCities != null && gsm.AllCities.Count > 0 ? gsm.AllCities[0] : null);

        if (startCity == null || string.IsNullOrEmpty(startCity.SceneName))
        {
            Debug.LogError("[CheatMenu] Quick Start failed — no starting city available.");
            return;
        }

        var ps = PlayerStats.Instance;
        if (ps == null)
        {
            var psGo = new GameObject("PlayerStats");
            ps = psGo.AddComponent<PlayerStats>();
        }

        ps.ResetRunStats();
        ps.PlayerName = DefaultPlayerName;
        ps.PlayerSprite = sprite;
        ps.CurrentTrench = trench;
        ps.CurrentWeapon = weapon;
        ps.PlayerWallet = 10000;
        ps.CurrentCity = startCity;
        ps.RecordCityVisited(startCity.Name);
        ps.InitializeDebt();
        PriceService.RunSeed = Random.Range(int.MinValue, int.MaxValue);

        SetPanelVisible(false);
        SceneManager.LoadScene(startCity.SceneName);
    }

    // ---- UI construction ----

    private void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("CheatCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // on top of everything
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // EventSystem (if scene doesn't have one)
        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Root container (toggled together so the dimmer hides with the panel)
        _root = new GameObject("Root", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        var rootRT = (RectTransform)_root.transform;
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // Backdrop dimmer
        var dimGo = new GameObject("Dimmer");
        dimGo.transform.SetParent(_root.transform, false);
        var dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.65f);
        var dimRT = dimImg.rectTransform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        // Panel
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(_root.transform, false);
        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
        var panelRT = panelImg.rectTransform;
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(560, 460);

        var vlg = _panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(28, 28, 24, 24);
        vlg.spacing = 12;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Title
        AddText(_panel.transform, "CHEAT MENU", 36, FontStyles.Bold, new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center, 50);
        _statusLabel = AddText(_panel.transform, "", 18, FontStyles.Italic, Color.white, TextAlignmentOptions.Center, 56);

        // Buttons
        _addCashButton    = AddButton(_panel.transform, $"+ ${CashCheatAmount:N0} CASH", new Color(0.2f, 0.5f, 0.2f, 1f), OnAddCashClicked);
        _dropHeatButton   = AddButton(_panel.transform, "DROP HEAT TO 0",                new Color(0.2f, 0.4f, 0.55f, 1f), OnDropHeatClicked);
        _quickStartButton = AddButton(_panel.transform, "QUICK START (skip intro)",      new Color(0.55f, 0.35f, 0.15f, 1f), OnQuickStartClicked);
        AddSpacer(_panel.transform, 12);
        AddButton(_panel.transform, "CLOSE (Esc)", new Color(0.3f, 0.3f, 0.3f, 1f), () => SetPanelVisible(false));

        // Help
        AddText(_panel.transform, "ESC anywhere — toggle menu.", 14, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center, 24);
    }

    private static TMP_Text AddText(Transform parent, string content, int size, FontStyles style, Color color, TextAlignmentOptions align, float height)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return t;
    }

    private static Button AddButton(Transform parent, string label, Color bg, System.Action onClick)
    {
        var go = new GameObject("Btn_" + label);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();

        var cb = btn.colors;
        cb.normalColor = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.20f);
        cb.pressedColor    = Color.Lerp(bg, Color.black, 0.20f);
        cb.disabledColor   = new Color(0.25f, 0.25f, 0.25f, 1f);
        btn.colors = cb;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 56;
        le.minHeight = 48;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var t = labelGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 22;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        if (onClick != null) btn.onClick.AddListener(() => onClick());
        return btn;
    }

    private static void AddSpacer(Transform parent, float h)
    {
        var go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;
    }
}
