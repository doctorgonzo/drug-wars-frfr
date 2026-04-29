using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroSequence : MonoBehaviour
{
    private static readonly (string headline, string body)[] Panels =
    {
        (
            "THREE WEEKS AGO",
            "You borrowed $50,000 from a loan shark named Big Tony.\n\nSeemed like a good idea at the time."
        ),
        (
            "THE DEAL",
            "30 days to pay it back.\n\nWith interest."
        ),
        (
            "YOUR OPTIONS",
            "You know a few dealers across the city.\n\nBuy low. Sell high. Don't get caught."
        ),
        (
            "GET TO WORK.",
            ""
        ),
    };

    [SerializeField] private string nextSceneName = "CharCreation";
    [SerializeField] private float crossfadeDuration = 0.35f;
    [SerializeField] private float sceneExitDuration = 0.8f;

    // Colors — same palette as TutorialManager
    private static readonly Color BgColor      = new Color(0.05f, 0.05f, 0.05f, 1.00f);
    private static readonly Color AccentColor  = new Color(0.22f, 0.78f, 0.35f, 1.00f);
    private static readonly Color TextPrimary  = new Color(0.95f, 0.95f, 0.95f, 1.00f);
    private static readonly Color TextBody     = new Color(0.72f, 0.72f, 0.72f, 1.00f);
    private static readonly Color TextMuted    = new Color(0.40f, 0.40f, 0.40f, 1.00f);
    private static readonly Color FooterLine   = new Color(0.18f, 0.18f, 0.18f, 1.00f);
    private static readonly Color BtnBg        = new Color(0.22f, 0.78f, 0.35f, 1.00f);

    private int currentPanel;
    private bool transitioning;

    private CanvasGroup contentGroup;
    private TMP_Text headlineText;
    private TMP_Text bodyText;
    private TMP_Text panelCounter;
    private TMP_Text nextBtnLabel;

    private TMP_FontAsset bebasFont;
    private TMP_FontAsset interFont;

    private void Start()
    {
        bebasFont = Resources.Load<TMP_FontAsset>("Fonts/BebasNeue-Regular SDF");
        interFont = Resources.Load<TMP_FontAsset>("Fonts/Inter-VariableFont_opsz,wght SDF");

        BuildUI();
        ShowPanel(0, instant: true);
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Full-screen canvas
        var canvasGo = new GameObject("IntroCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Black background — fills everything
        var bg = MakeImage(canvasGo, "Background", BgColor);
        Stretch(bg.GetComponent<RectTransform>());
        bg.GetComponent<Image>().raycastTarget = true;

        // Content block — 800px wide, 500px tall, centered, slightly above center
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(bg.transform, false);
        contentGroup = content.AddComponent<CanvasGroup>();
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = contentRT.anchorMax = new Vector2(0.5f, 0.5f);
        contentRT.pivot = new Vector2(0.5f, 0.5f);
        contentRT.anchoredPosition = new Vector2(0f, 30f);
        contentRT.sizeDelta = new Vector2(800f, 520f);

        // Accent bar — thin horizontal line, left edge, above headline
        var accentBar = MakeImage(content, "AccentBar", AccentColor);
        var accentRT = accentBar.GetComponent<RectTransform>();
        accentRT.anchorMin = new Vector2(0f, 1f);
        accentRT.anchorMax = new Vector2(0f, 1f);
        accentRT.pivot = new Vector2(0f, 1f);
        accentRT.anchoredPosition = new Vector2(0f, 0f);
        accentRT.sizeDelta = new Vector2(72f, 4f);

        // Headline — Bebas Neue, large, left-aligned, below accent
        headlineText = MakeTMP(content, "Headline", bebasFont, 78f, TextPrimary);
        var headlineRT = headlineText.GetComponent<RectTransform>();
        headlineRT.anchorMin = new Vector2(0f, 1f);
        headlineRT.anchorMax = new Vector2(1f, 1f);
        headlineRT.pivot = new Vector2(0f, 1f);
        headlineRT.anchoredPosition = new Vector2(0f, -16f);
        headlineRT.sizeDelta = new Vector2(0f, 110f);
        headlineText.alignment = TextAlignmentOptions.BottomLeft;
        headlineText.enableAutoSizing = true;
        headlineText.fontSizeMin = 40f;
        headlineText.fontSizeMax = 78f;
        headlineText.enableWordWrapping = false;

        // Divider — 1px line below headline
        var divider = MakeImage(content, "Divider", FooterLine);
        var divRT = divider.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 1f);
        divRT.anchorMax = new Vector2(1f, 1f);
        divRT.pivot = new Vector2(0f, 1f);
        divRT.anchoredPosition = new Vector2(0f, -128f);
        divRT.sizeDelta = new Vector2(0f, 1f);

        // Body text — Inter, auto-size, below divider
        bodyText = MakeTMP(content, "Body", interFont, 22f, TextBody);
        var bodyRT = bodyText.GetComponent<RectTransform>();
        bodyRT.anchorMin = new Vector2(0f, 0f);
        bodyRT.anchorMax = new Vector2(1f, 1f);
        bodyRT.offsetMin = new Vector2(0f, 72f);    // above footer
        bodyRT.offsetMax = new Vector2(0f, -148f);  // below divider
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 14f;
        bodyText.fontSizeMax = 22f;
        bodyText.enableWordWrapping = true;
        bodyText.lineSpacing = 10f;

        // Footer — pinned to bottom of content block
        var footer = new GameObject("Footer", typeof(RectTransform));
        footer.transform.SetParent(content.transform, false);
        var footerRT = footer.GetComponent<RectTransform>();
        footerRT.anchorMin = Vector2.zero;
        footerRT.anchorMax = new Vector2(1f, 0f);
        footerRT.pivot = new Vector2(0.5f, 0f);
        footerRT.offsetMin = Vector2.zero;
        footerRT.offsetMax = new Vector2(0f, 64f);

        // Footer top line
        var footerDiv = MakeImage(footer, "FooterDivider", FooterLine);
        var fdRT = footerDiv.GetComponent<RectTransform>();
        fdRT.anchorMin = new Vector2(0f, 1f);
        fdRT.anchorMax = Vector2.one;
        fdRT.pivot = new Vector2(0.5f, 1f);
        fdRT.offsetMin = new Vector2(0f, -1f);
        fdRT.offsetMax = Vector2.zero;

        // Panel counter — footer left
        panelCounter = MakeTMP(footer, "Counter", interFont, 13f, TextMuted);
        var counterRT = panelCounter.GetComponent<RectTransform>();
        counterRT.anchorMin = Vector2.zero;
        counterRT.anchorMax = new Vector2(0.5f, 1f);
        counterRT.offsetMin = Vector2.zero;
        counterRT.offsetMax = Vector2.zero;
        panelCounter.alignment = TextAlignmentOptions.MidlineLeft;
        panelCounter.enableAutoSizing = false;

        // Next button — footer right, green, Bebas Neue
        var nextBtn = MakeButton(footer, "NextBtn", "NEXT", bebasFont, 22f, Color.white, BtnBg);
        var nextBtnRT = nextBtn.GetComponent<RectTransform>();
        nextBtnRT.anchorMin = new Vector2(1f, 0f);
        nextBtnRT.anchorMax = Vector2.one;
        nextBtnRT.pivot = new Vector2(1f, 0.5f);
        nextBtnRT.offsetMin = new Vector2(-160f, 10f);
        nextBtnRT.offsetMax = new Vector2(0f, -10f);
        nextBtnLabel = nextBtn.GetComponentInChildren<TMP_Text>();
        nextBtn.GetComponent<Button>().onClick.AddListener(OnNextClicked);
    }

    // ── Panel Logic ───────────────────────────────────────────────────────────

    private void ShowPanel(int index, bool instant = false)
    {
        var (headline, body) = Panels[index];
        headlineText.text = headline;
        bodyText.text = body;
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));
        panelCounter.text = $"{index + 1}  /  {Panels.Length}";
        nextBtnLabel.text = index == Panels.Length - 1 ? "LET'S GO" : "NEXT";

        if (instant) contentGroup.alpha = 1f;
    }

    private void OnNextClicked()
    {
        if (transitioning) return;
        currentPanel++;
        if (currentPanel >= Panels.Length)
            StartCoroutine(ExitToNextScene());
        else
            StartCoroutine(CrossfadeToPanel(currentPanel));
    }

    private IEnumerator CrossfadeToPanel(int index)
    {
        transitioning = true;
        yield return StartCoroutine(FadeGroup(contentGroup, 1f, 0f, crossfadeDuration));
        ShowPanel(index);
        yield return StartCoroutine(FadeGroup(contentGroup, 0f, 1f, crossfadeDuration));
        transitioning = false;
    }

    private IEnumerator ExitToNextScene()
    {
        transitioning = true;
        if (FadeController.Instance != null)
            FadeController.Instance.FadeOut(sceneExitDuration);
        yield return new WaitForSeconds(sceneExitDuration + 0.1f);
        SceneManager.LoadScene(nextSceneName);
    }

    private static IEnumerator FadeGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        cg.alpha = to;
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
        go.GetComponent<Image>().color = bgColor;
        go.GetComponent<Image>().raycastTarget = true;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        Stretch(labelGo.GetComponent<RectTransform>());
        var t = labelGo.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = label;
        t.fontSize = fontSize;
        t.color = textColor;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bgColor * 1.2f;
        colors.pressedColor = bgColor * 0.75f;
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
