using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    private static readonly (string title, string body)[] Steps =
    {
        (
            "WELCOME TO THE GAME",
            "You owe Big Tony $50,000.\n\nYou have 30 days to pay it back before he sends someone to collect — permanently.\n\nHere's what you need to know."
        ),
        (
            "THE DEALERS",
            "Those portraits on the map are dealers.\n\nClick one to open their inventory. Buy drugs cheap here, sell them to another dealer for profit.\n\nBuy low. Sell high."
        ),
        (
            "HEAT",
            "Every buy and sell adds Heat to the bar at the top of your screen.\n\nHit 100% and the cops show up. Let it decay between deals — or keep pushing your luck."
        ),
        (
            "TRAVEL",
            "Prices vary wildly between cities. Use the travel panel to move between them.\n\nEach trip costs $500 and takes 6 in-game hours. Time is debt."
        ),
        (
            "PAY THE MAN",
            "Open the Debt tab in your info panel to make payments toward what you owe.\n\nInterest compounds daily. Pay Big Tony off before Day 30.\n\nGood luck."
        ),
    };

    private const string SeenKey = "TutorialSeen_v1";

    // Colors
    private static readonly Color PanelBg     = new Color(0.09f, 0.09f, 0.09f, 0.97f);
    private static readonly Color TitleBg     = new Color(0.13f, 0.13f, 0.13f, 1.00f);
    private static readonly Color FooterBg    = new Color(0.07f, 0.07f, 0.07f, 1.00f);
    private static readonly Color AccentColor = new Color(0.22f, 0.78f, 0.35f, 1.00f);
    private static readonly Color TextPrimary = new Color(0.95f, 0.95f, 0.95f, 1.00f);
    private static readonly Color TextMuted   = new Color(0.50f, 0.50f, 0.50f, 1.00f);
    private static readonly Color OverlayBg   = new Color(0.00f, 0.00f, 0.00f, 0.80f);
    private static readonly Color DividerColor= new Color(0.20f, 0.20f, 0.20f, 1.00f);

    private int currentStep;
    private CanvasGroup panelGroup;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text stepCounter;
    private TMP_Text nextLabel;
    private GameObject root;

    private TMP_FontAsset bebasFont;
    private TMP_FontAsset interFont;

    private void Start()
    {
        if (PlayerPrefs.GetInt(SeenKey, 0) == 1) { Destroy(gameObject); return; }

        bebasFont = Resources.Load<TMP_FontAsset>("Fonts/BebasNeue-Regular SDF");
        interFont = Resources.Load<TMP_FontAsset>("Fonts/Inter-VariableFont_opsz,wght SDF");

        BuildUI();
        ShowStep(0);
        StartCoroutine(FadeIn());
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Root canvas — always on top
        var canvasGo = new GameObject("TutorialCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        ((CanvasScaler)canvasGo.GetComponent<CanvasScaler>()).referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Full-screen overlay
        var overlay = MakeImage(canvasGo, "Overlay", OverlayBg);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.GetComponent<Image>().raycastTarget = true;

        // Panel (700 × 460, centered)
        root = MakeImage(overlay, "Panel", PanelBg);
        var panelRT = root.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(700, 460);
        panelGroup = root.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;

        // Accent bar (left edge, 5px)
        var accent = MakeImage(root, "Accent", AccentColor);
        var accentRT = accent.GetComponent<RectTransform>();
        accentRT.anchorMin = Vector2.zero;
        accentRT.anchorMax = new Vector2(0f, 1f);
        accentRT.pivot = new Vector2(0f, 0.5f);
        accentRT.offsetMin = Vector2.zero;
        accentRT.offsetMax = new Vector2(5f, 0f);

        // Title bar (top, 72px)
        var titleBar = MakeImage(root, "TitleBar", TitleBg);
        var titleBarRT = titleBar.GetComponent<RectTransform>();
        titleBarRT.anchorMin = new Vector2(0f, 1f);
        titleBarRT.anchorMax = Vector2.one;
        titleBarRT.pivot = new Vector2(0.5f, 1f);
        titleBarRT.offsetMin = new Vector2(0f, -72f);
        titleBarRT.offsetMax = Vector2.zero;

        titleText = MakeTMP(titleBar, "TitleText", bebasFont, 30f, TextPrimary);
        var titleRT = titleText.GetComponent<RectTransform>();
        Stretch(titleRT);
        titleRT.offsetMin = new Vector2(24f, 0f);
        titleRT.offsetMax = new Vector2(-24f, 0f);
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.enableAutoSizing = false;

        // Divider below title bar
        var divider = MakeImage(root, "Divider", DividerColor);
        var divRT = divider.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 1f);
        divRT.anchorMax = Vector2.one;
        divRT.pivot = new Vector2(0.5f, 1f);
        divRT.offsetMin = new Vector2(0f, -73f);
        divRT.offsetMax = new Vector2(0f, -72f);

        // Footer (bottom, 58px)
        var footer = MakeImage(root, "Footer", FooterBg);
        var footerRT = footer.GetComponent<RectTransform>();
        footerRT.anchorMin = Vector2.zero;
        footerRT.anchorMax = new Vector2(1f, 0f);
        footerRT.pivot = new Vector2(0.5f, 0f);
        footerRT.offsetMin = Vector2.zero;
        footerRT.offsetMax = new Vector2(0f, 58f);

        // Footer top divider
        var footerDiv = MakeImage(footer, "FooterDivider", DividerColor);
        var fdRT = footerDiv.GetComponent<RectTransform>();
        fdRT.anchorMin = new Vector2(0f, 1f);
        fdRT.anchorMax = Vector2.one;
        fdRT.pivot = new Vector2(0.5f, 1f);
        fdRT.offsetMin = new Vector2(0f, -1f);
        fdRT.offsetMax = Vector2.zero;

        // Step counter (footer left)
        stepCounter = MakeTMP(footer, "StepCounter", interFont, 13f, TextMuted);
        var scRT = stepCounter.GetComponent<RectTransform>();
        scRT.anchorMin = Vector2.zero;
        scRT.anchorMax = new Vector2(0.4f, 1f);
        scRT.offsetMin = new Vector2(20f, 0f);
        scRT.offsetMax = Vector2.zero;
        stepCounter.alignment = TextAlignmentOptions.MidlineLeft;
        stepCounter.enableAutoSizing = false;

        // Skip button (footer right, subtle)
        var skipBtn = MakeButton(footer, "SkipBtn", "SKIP", interFont, 13f, TextMuted, Color.clear);
        var skipRT = skipBtn.GetComponent<RectTransform>();
        skipRT.anchorMin = new Vector2(1f, 0f);
        skipRT.anchorMax = Vector2.one;
        skipRT.pivot = new Vector2(1f, 0.5f);
        skipRT.offsetMin = new Vector2(-190f, 8f);
        skipRT.offsetMax = new Vector2(-120f, -8f);
        skipBtn.GetComponent<Button>().onClick.AddListener(Dismiss);

        // Next button (footer right, accent)
        var nextBtn = MakeButton(footer, "NextBtn", "NEXT", bebasFont, 20f, Color.white, AccentColor);
        var nextRT = nextBtn.GetComponent<RectTransform>();
        nextRT.anchorMin = new Vector2(1f, 0f);
        nextRT.anchorMax = Vector2.one;
        nextRT.pivot = new Vector2(1f, 0.5f);
        nextRT.offsetMin = new Vector2(-115f, 8f);
        nextRT.offsetMax = new Vector2(-16f, -8f);
        nextLabel = nextBtn.GetComponentInChildren<TMP_Text>();
        nextBtn.GetComponent<Button>().onClick.AddListener(OnNext);

        // Body text area (between title bar and footer)
        bodyText = MakeTMP(root, "BodyText", interFont, 17f, TextPrimary);
        var bodyRT = bodyText.GetComponent<RectTransform>();
        bodyRT.anchorMin = Vector2.zero;
        bodyRT.anchorMax = Vector2.one;
        bodyRT.offsetMin = new Vector2(28f, 66f);   // above footer
        bodyRT.offsetMax = new Vector2(-28f, -82f);  // below title bar
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 13f;
        bodyText.fontSizeMax = 17f;
        bodyText.textWrappingMode = TextWrappingModes.PreserveWhitespace;
        bodyText.lineSpacing = 6f;
        bodyText.color = new Color(0.82f, 0.82f, 0.82f, 1f);
    }

    // ── Steps ─────────────────────────────────────────────────────────────────

    private void ShowStep(int index)
    {
        titleText.text = Steps[index].title;
        bodyText.text  = Steps[index].body;
        stepCounter.text = $"{index + 1}  /  {Steps.Length}";
        nextLabel.text = index == Steps.Length - 1 ? "GOT IT" : "NEXT";
    }

    private void OnNext()
    {
        currentStep++;
        if (currentStep >= Steps.Length) Dismiss();
        else ShowStep(currentStep);
    }

    private void Dismiss()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        StartCoroutine(FadeOutAndDestroy());
    }

    // ── Fade ──────────────────────────────────────────────────────────────────

    private IEnumerator FadeIn()
    {
        float t = 0f; const float dur = 0.3f;
        while (t < dur) { t += Time.deltaTime; panelGroup.alpha = Mathf.Clamp01(t / dur); yield return null; }
        panelGroup.alpha = 1f;
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float t = 0f; const float dur = 0.25f;
        while (t < dur) { t += Time.deltaTime; panelGroup.alpha = 1f - Mathf.Clamp01(t / dur); yield return null; }
        Destroy(gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject MakeImage(GameObject parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent.transform, false);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        return go;
    }

    private static TMP_Text MakeTMP(GameObject parent, string name, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent.transform, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        return t;
    }

    private static GameObject MakeButton(GameObject parent, string name, string label,
        TMP_FontAsset font, float fontSize, Color textColor, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent.transform, false);
        var img = go.GetComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = true;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = textGo.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = label;
        t.fontSize = fontSize;
        t.color = textColor;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bgColor == Color.clear
            ? new Color(1f, 1f, 1f, 0.1f)
            : bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.8f;
        btn.colors = colors;

        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
