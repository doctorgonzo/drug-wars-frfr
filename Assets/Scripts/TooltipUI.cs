using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;
    [SerializeField] private RectTransform canvasRectTransform; // The main canvas RectTransform
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f); // Offset from the mouse cursor

    [Header("Sizing")]
    [SerializeField] private float maxWidth = 260f;
    [SerializeField] private float padding = 10f;
    [SerializeField] private float spacing = 2f;

    [Header("Font Sizes")]
    [SerializeField] private float nameFontSize = 16f;
    [SerializeField] private float descFontSize = 13f;

    [Header("Background")]
    [SerializeField] private Color bgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);

    private RectTransform tooltipRectTransform;
    private LayoutElement layoutElement;
    private Canvas canvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        tooltipRectTransform = tooltipPanel.GetComponent<RectTransform>();
        tooltipRectTransform.pivot = new Vector2(0f, 1f); // top-left anchors at cursor+offset
        canvas = canvasRectTransform.GetComponent<Canvas>();
        tooltipPanel.SetActive(false);

        ConfigureLayout();
        ConfigureText();
    }

    private void ConfigureLayout()
    {
        // --- ContentSizeFitter: auto-shrink to content ---
        var fitter = tooltipPanel.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = tooltipPanel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // --- VerticalLayoutGroup: padding + spacing ---
        var vlg = tooltipPanel.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = tooltipPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(
            Mathf.RoundToInt(padding),
            Mathf.RoundToInt(padding),
            Mathf.RoundToInt(padding),
            Mathf.RoundToInt(padding));
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // --- LayoutElement: cap max width ---
        layoutElement = tooltipPanel.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = tooltipPanel.AddComponent<LayoutElement>();

        // --- Background color ---
        var bg = tooltipPanel.GetComponent<Image>();
        if (bg != null) bg.color = bgColor;
    }

    private void ConfigureText()
    {
        if (itemNameText != null)
        {
            itemNameText.fontSize = nameFontSize;
            itemNameText.fontStyle = FontStyles.Bold;
            itemNameText.enableWordWrapping = true;
            itemNameText.overflowMode = TextOverflowModes.Ellipsis;
            itemNameText.margin = Vector4.zero;
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.fontSize = descFontSize;
            itemDescriptionText.fontStyle = FontStyles.Italic;
            itemDescriptionText.enableWordWrapping = true;
            itemDescriptionText.overflowMode = TextOverflowModes.Ellipsis;
            itemDescriptionText.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            itemDescriptionText.margin = Vector4.zero;
        }
    }

    private void Update()
    {
        if (!tooltipPanel.activeSelf) return;

        Vector2 localPoint;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, Input.mousePosition, cam, out localPoint);
        Vector2 desired = localPoint + offset;

        // --- Clamp to screen edges ---
        Vector2 canvasSize = canvasRectTransform.rect.size;
        Vector2 tooltipSize = tooltipRectTransform.rect.size;
        Vector2 pivot = tooltipRectTransform.pivot;

        float minX = -canvasSize.x * 0.5f + tooltipSize.x * pivot.x;
        float maxX = canvasSize.x * 0.5f - tooltipSize.x * (1f - pivot.x);
        float minY = -canvasSize.y * 0.5f + tooltipSize.y * pivot.y;
        float maxY = canvasSize.y * 0.5f - tooltipSize.y * (1f - pivot.y);

        desired.x = Mathf.Clamp(desired.x, minX, maxX);
        desired.y = Mathf.Clamp(desired.y, minY, maxY);

        tooltipRectTransform.localPosition = desired;
    }

    public void ShowTooltip(string itemName, string itemDescription)
    {
        if (!this) return;
        if (!tooltipPanel) return;
        itemNameText.text = itemName;
        itemDescriptionText.text = itemDescription;

        // Constrain width: use max width only if text is wide enough to need it
        float nameWidth = itemNameText.GetPreferredValues(itemName).x + padding * 2f;
        float descWidth = itemDescriptionText.GetPreferredValues(itemDescription).x + padding * 2f;
        float contentWidth = Mathf.Max(nameWidth, descWidth);
        if (contentWidth > maxWidth)
            layoutElement.preferredWidth = maxWidth;
        else
            layoutElement.preferredWidth = -1; // let it auto-size small

        tooltipPanel.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);
    }

    public void HideTooltip()
    {
        if (!this) return;
        if (!tooltipPanel) return;
        try { tooltipPanel.SetActive(false); }
        catch (MissingReferenceException) { /* swallowed because it's being torn down */ }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        tooltipPanel = null; // break stale refs
    }
}