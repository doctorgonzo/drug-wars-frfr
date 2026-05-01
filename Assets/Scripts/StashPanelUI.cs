using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Auto-spawned UI for the per-city stash. Hotkey 'S' toggles. No Editor wiring.
// Two-column layout: player inventory (deposit) on the left, this city's stash
// (withdraw) on the right. Closes on scene load + ignored in non-city scenes.
public class StashPanelUI : MonoBehaviour
{
    public static StashPanelUI Instance { get; private set; }

    private static readonly HashSet<string> NonCityScenes = new HashSet<string>
    {
        "Startup", "Intro", "CharCreation", "GameOver", "YouWin"
    };

    private const int ShiftMultiplier = 10;

    private Canvas _canvas;
    private GameObject _backdrop;
    private GameObject _panel;
    private TMP_Text _headerText;
    private Transform _playerContent;
    private Transform _stashContent;
    private TMP_Text _footerText;

    private bool _built;
    private bool _isOpen;
    private bool _subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("StashPanelUI");
        go.AddComponent<StashPanelUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_isOpen) Hide();
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged += OnDataChanged;
        if (StashService.Instance != null)
            StashService.Instance.OnStashChanged += OnDataChanged;
        if (PlayerStats.Instance != null || StashService.Instance != null)
            _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged -= OnDataChanged;
        if (StashService.Instance != null)
            StashService.Instance.OnStashChanged -= OnDataChanged;
        _subscribed = false;
    }

    private void OnDataChanged()
    {
        if (_isOpen) Refresh();
    }

    private void Update()
    {
        if (!_subscribed) TrySubscribe();

        if (NonCityScenes.Contains(SceneManager.GetActiveScene().name)) return;
        if (!Input.GetKeyDown(KeyCode.S)) return;

        // Don't toggle while the player is typing in an InputField
        var selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
            : null;
        if (selected != null && selected.GetComponent<TMP_InputField>() != null) return;

        Toggle();
    }

    public void Toggle() { if (_isOpen) Hide(); else Show(); }

    public void Show()
    {
        var ps = PlayerStats.Instance;
        if (ps == null || ps.CurrentCity == null) return;
        if (!_built) BuildUI();
        _backdrop.SetActive(true);
        _panel.SetActive(true);
        _isOpen = true;
        Refresh();
    }

    public void Hide()
    {
        if (!_built) return;
        _backdrop.SetActive(false);
        _panel.SetActive(false);
        _isOpen = false;
    }

    private static int GetShiftAmount() =>
        (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            ? ShiftMultiplier : 1;

    // ------------------------------------------------------------------
    //  Procedural UI build
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        // Canvas (overlay, sorted above gameplay UI but below cheat menu)
        var canvasGO = new GameObject("StashCanvas");
        canvasGO.transform.SetParent(null);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 4000;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // Backdrop dim — clicking outside the panel closes it
        _backdrop = new GameObject("Backdrop");
        _backdrop.transform.SetParent(canvasGO.transform, false);
        var backdropRect = _backdrop.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.sizeDelta = Vector2.zero;
        var backdropImg = _backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.55f);
        var backdropBtn = _backdrop.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(Hide);
        _backdrop.SetActive(false);

        // Main panel
        _panel = new GameObject("StashPanel");
        _panel.transform.SetParent(canvasGO.transform, false);
        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.03f, 0.03f);
        panelRect.anchorMax = new Vector2(0.97f, 0.97f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = Vector2.zero;
        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0.10f, 0.11f, 0.16f, 0.98f);

        var panelVlg = _panel.AddComponent<VerticalLayoutGroup>();
        panelVlg.padding = new RectOffset(0, 0, 0, 0);
        panelVlg.spacing = 0;
        panelVlg.childForceExpandWidth = true;
        panelVlg.childForceExpandHeight = false;
        panelVlg.childControlWidth = true;
        panelVlg.childControlHeight = true;

        BuildHeader(_panel.transform);
        BuildBody(_panel.transform);
        BuildFooter(_panel.transform);

        _panel.SetActive(false);
        _built = true;
    }

    private void BuildHeader(Transform parent)
    {
        var header = new GameObject("Header");
        header.transform.SetParent(parent, false);
        header.AddComponent<RectTransform>();
        var le = header.AddComponent<LayoutElement>();
        le.preferredHeight = 100;
        le.minHeight = 100;
        var img = header.AddComponent<Image>();
        img.color = new Color(0.16f, 0.18f, 0.28f, 1f);

        var hlg = header.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(24, 12, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandHeight = true;
        hlg.spacing = 12;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(header.transform, false);
        titleGO.AddComponent<RectTransform>();
        var titleLE = titleGO.AddComponent<LayoutElement>();
        titleLE.flexibleWidth = 1;
        _headerText = titleGO.AddComponent<TextMeshProUGUI>();
        _headerText.text = "STASH";
        _headerText.color = new Color(1f, 0.86f, 0.45f);
        _headerText.fontStyle = FontStyles.Bold;
        _headerText.fontSize = 40;
        _headerText.alignment = TextAlignmentOptions.MidlineLeft;

        // Close button
        var closeGO = new GameObject("Close");
        closeGO.transform.SetParent(header.transform, false);
        var closeRect = closeGO.AddComponent<RectTransform>();
        closeRect.sizeDelta = new Vector2(70, 70);
        var closeLE = closeGO.AddComponent<LayoutElement>();
        closeLE.preferredWidth = 70;
        closeLE.minWidth = 70;
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.45f, 0.18f, 0.18f);
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.onClick.AddListener(Hide);
        var closeLabelGO = new GameObject("X");
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        var closeLabelRect = closeLabelGO.AddComponent<RectTransform>();
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.sizeDelta = Vector2.zero;
        var closeLabel = closeLabelGO.AddComponent<TextMeshProUGUI>();
        closeLabel.text = "✕";
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.color = Color.white;
        closeLabel.fontStyle = FontStyles.Bold;
        closeLabel.fontSize = 36;
    }

    private void BuildBody(Transform parent)
    {
        var body = new GameObject("Body");
        body.transform.SetParent(parent, false);
        body.AddComponent<RectTransform>();
        var bodyLE = body.AddComponent<LayoutElement>();
        bodyLE.preferredHeight = 940;
        bodyLE.flexibleHeight = 1;
        var bodyHLG = body.AddComponent<HorizontalLayoutGroup>();
        bodyHLG.padding = new RectOffset(16, 16, 12, 12);
        bodyHLG.spacing = 12;
        bodyHLG.childForceExpandHeight = true;
        bodyHLG.childForceExpandWidth = true;
        bodyHLG.childControlWidth = true;
        bodyHLG.childControlHeight = true;

        _playerContent = BuildColumn(body.transform, "YOUR INVENTORY", new Color(0.18f, 0.30f, 0.18f, 0.6f));
        _stashContent  = BuildColumn(body.transform, "STASH HERE",     new Color(0.30f, 0.22f, 0.10f, 0.6f));
    }

    private Transform BuildColumn(Transform parent, string title, Color headerTint)
    {
        var col = new GameObject(title);
        col.transform.SetParent(parent, false);
        col.AddComponent<RectTransform>();
        var colImg = col.AddComponent<Image>();
        colImg.color = new Color(0.14f, 0.15f, 0.20f, 1f);
        var colVlg = col.AddComponent<VerticalLayoutGroup>();
        colVlg.padding = new RectOffset(0, 0, 0, 0);
        colVlg.spacing = 0;
        colVlg.childForceExpandWidth = true;
        colVlg.childForceExpandHeight = false;
        colVlg.childControlWidth = true;
        // Same childControlHeight bug as the inner contentVlg — without this, the column's
        // VLG ignores Header's preferredHeight=44 and Scroll's flexibleHeight=1, so they
        // render at default sizeDelta (100,100) and the scroll viewport collapses.
        colVlg.childControlHeight = true;

        // Column header
        var head = new GameObject("Head");
        head.transform.SetParent(col.transform, false);
        head.AddComponent<RectTransform>();
        var headLE = head.AddComponent<LayoutElement>();
        headLE.preferredHeight = 56;
        headLE.minHeight = 56;
        var headImg = head.AddComponent<Image>();
        headImg.color = headerTint;
        var headTextGO = new GameObject("HeadText");
        headTextGO.transform.SetParent(head.transform, false);
        var headTextRect = headTextGO.AddComponent<RectTransform>();
        headTextRect.anchorMin = Vector2.zero;
        headTextRect.anchorMax = Vector2.one;
        headTextRect.sizeDelta = Vector2.zero;
        var headText = headTextGO.AddComponent<TextMeshProUGUI>();
        headText.text = title;
        headText.alignment = TextAlignmentOptions.Center;
        headText.color = Color.white;
        headText.fontStyle = FontStyles.Bold;
        headText.fontSize = 26;

        // Scroll view
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(col.transform, false);
        scrollGO.AddComponent<RectTransform>();
        var scrollLE = scrollGO.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;
        scrollLE.preferredHeight = 360;
        var scrollImg = scrollGO.AddComponent<Image>();
        scrollImg.color = new Color(0.10f, 0.11f, 0.14f, 1f);
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;

        // Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        // RectMask2D clips by rect without needing a graphic. The previous Mask + Image
        // (no sprite, alpha 0.001) was clipping all children to nothing because Image
        // without a sprite renders no geometry — so Mask had no shape to clip to.
        var raycastImg = viewportGO.AddComponent<Image>();
        raycastImg.color = new Color(0, 0, 0, 0.001f);
        viewportGO.AddComponent<RectMask2D>();
        sr.viewport = viewportRect;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);
        var contentVlg = contentGO.AddComponent<VerticalLayoutGroup>();
        contentVlg.padding = new RectOffset(8, 8, 8, 8);
        contentVlg.spacing = 6;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;
        contentVlg.childControlWidth = true;
        // childControlHeight MUST be true — otherwise ContentSizeFitter can't compute the
        // preferred content height from each row's LayoutElement, content collapses to ~0px,
        // rows render but are invisible/unclickable. Same root cause as the RunSummaryUI
        // invisibility bug.
        contentVlg.childControlHeight = true;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentRect;

        return contentGO.transform;
    }

    private void BuildFooter(Transform parent)
    {
        var footer = new GameObject("Footer");
        footer.transform.SetParent(parent, false);
        footer.AddComponent<RectTransform>();
        var le = footer.AddComponent<LayoutElement>();
        le.preferredHeight = 80;
        le.minHeight = 80;
        var img = footer.AddComponent<Image>();
        img.color = new Color(0.16f, 0.18f, 0.28f, 1f);

        var textGO = new GameObject("FooterText");
        textGO.transform.SetParent(footer.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-32, 0);
        textRect.anchoredPosition = Vector2.zero;
        _footerText = textGO.AddComponent<TextMeshProUGUI>();
        _footerText.alignment = TextAlignmentOptions.Center;
        _footerText.color = new Color(0.85f, 0.85f, 0.9f);
        _footerText.fontSize = 22;
        _footerText.text = "";
    }

    // ------------------------------------------------------------------
    //  Refresh — rebuilds rows from current state
    // ------------------------------------------------------------------

    private void Refresh()
    {
        var ps = PlayerStats.Instance;
        if (ps == null) return;
        var city = ps.CurrentCity;
        if (city == null) { Hide(); return; }

        _headerText.text = $"STASH IN <color=#FFD27A>{city.Name.ToUpper()}</color>";

        ClearChildren(_playerContent);
        ClearChildren(_stashContent);

        // Player drugs (deposit side)
        var playerDrugs = ps.inventory.FindAll(i => i != null && i.Type == ItemType.Drug && i.Amount > 0);
        if (playerDrugs.Count == 0)
        {
            BuildEmptyRow(_playerContent, "(no drugs in trenchcoat)");
        }
        else
        {
            foreach (var item in playerDrugs)
                BuildPlayerRow(item, city.Name);
        }

        // Stash contents (withdraw side)
        var stash = StashService.Instance != null
            ? StashService.Instance.GetStash(city.Name)
            : new List<ItemInstance>();
        var stashDrugs = stash.FindAll(i => i != null && i.Type == ItemType.Drug && i.Amount > 0);
        if (stashDrugs.Count == 0)
        {
            BuildEmptyRow(_stashContent, "(stash is empty)");
        }
        else
        {
            foreach (var item in stashDrugs)
                BuildStashRow(item, city.Name);
        }

        int used = ps.GetUsedSlots();
        int total = ps.GetTotalSlots();
        int stashed = StashService.Instance != null
            ? StashService.Instance.GetTotalUnitsStashed(city.Name) : 0;
        _footerText.text =
            $"Trenchcoat: <b>{used}/{total}</b> slots used   •   Stash here: <b>{stashed}</b> units" +
            "   •   <size=85%>shift+click for ×10</size>";

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panel.GetComponent<RectTransform>());
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }

    private void BuildEmptyRow(Transform parent, string text)
    {
        var go = new GameObject("Empty");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 60;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.color = new Color(0.6f, 0.6f, 0.65f);
        t.fontStyle = FontStyles.Italic;
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 22;
    }

    private void BuildPlayerRow(ItemInstance item, string cityName)
    {
        var row = BuildRowShell(_playerContent);
        var label = BuildRowLabel(row.transform, $"{item.Name} × {item.Amount}");
        BuildRowButton(row.transform, "STASH", new Color(0.25f, 0.55f, 0.25f), () =>
        {
            StashService.Instance?.Deposit(cityName, item, GetShiftAmount());
        });
        BuildRowButton(row.transform, "ALL", new Color(0.40f, 0.30f, 0.55f), () =>
        {
            StashService.Instance?.Deposit(cityName, item, item.Amount);
        });
    }

    private void BuildStashRow(ItemInstance item, string cityName)
    {
        var row = BuildRowShell(_stashContent);
        BuildRowLabel(row.transform, $"{item.Name} × {item.Amount}");
        BuildRowButton(row.transform, "TAKE", new Color(0.55f, 0.45f, 0.18f), () =>
        {
            StashService.Instance?.Withdraw(cityName, item, GetShiftAmount());
        });
        BuildRowButton(row.transform, "ALL", new Color(0.40f, 0.30f, 0.55f), () =>
        {
            StashService.Instance?.Withdraw(cityName, item, item.Amount);
        });
    }

    private GameObject BuildRowShell(Transform parent)
    {
        var row = new GameObject("Row");
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 64;
        le.minHeight = 64;
        var img = row.AddComponent<Image>();
        img.color = new Color(0.18f, 0.20f, 0.26f, 1f);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 14, 6, 6);
        hlg.spacing = 14;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        // Explicit flags — defaults vary by Unity version, and getting these wrong
        // produces invisible/clipped rows.
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        return row;
    }

    private TMP_Text BuildRowLabel(Transform parent, string text)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.color = Color.white;
        t.fontSize = 26;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        return t;
    }

    private Button BuildRowButton(Transform parent, string label, Color color, System.Action onClick)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 140;
        le.minWidth = 110;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(color.r * 1.2f, color.g * 1.2f, color.b * 1.2f, 1f);
        colors.pressedColor = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var labelGO = new GameObject("Text");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        var t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
        t.fontSize = 20;

        return btn;
    }
}
