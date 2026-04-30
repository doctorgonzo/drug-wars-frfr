using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Populates the Inventory tab with equipped gear and carried drugs.
/// Attach to the Inventory content panel and assign the content Transform in the Inspector.
/// </summary>
public class InventoryTabUI : MonoBehaviour
{
    [Tooltip("Parent transform where inventory rows are spawned (can be this object).")]
    [SerializeField] private Transform contentParent;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private TMP_FontAsset _cachedFont;

    private void OnEnable()
    {
        Refresh();
    }

    private void Start()
    {
        EnsureLayout();

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged -= Refresh;
    }

    public void Refresh()
    {
        ClearRows();
        if (PlayerStats.Instance == null) return;

        var ps = PlayerStats.Instance;
        var font = GetFont();

        // --- Equipped Trenchcoat ---
        if (ps.CurrentTrench != null)
        {
            AddEquipmentRow(
                font,
                ps.CurrentTrench.Image,
                ps.CurrentTrench.Name,
                $"Slots: {ps.CurrentTrench.StorageSlots}  |  Armor: {ps.CurrentTrench.ArmorValue}",
                "EQUIPPED"
            );
        }

        // --- Equipped Weapon ---
        if (ps.CurrentWeapon != null)
        {
            AddEquipmentRow(
                font,
                ps.CurrentWeapon.Image,
                ps.CurrentWeapon.Name,
                $"Damage: {ps.CurrentWeapon.Damage}",
                "EQUIPPED"
            );
        }

        // --- Separator ---
        if ((ps.CurrentTrench != null || ps.CurrentWeapon != null) && ps.inventory.Count > 0)
            AddSeparator(font);

        // --- Carried drugs / items ---
        foreach (var item in ps.inventory)
        {
            if (item == null || item.Amount <= 0) continue;
            AddItemRow(font, item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent as RectTransform);

        var scrollRect = contentParent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    // ─── Row builders ────────────────────────────────────────

    private void AddEquipmentRow(TMP_FontAsset font, Sprite icon, string itemName, string stats, string badge)
    {
        var tile = CreateTile(75f);

        AddIcon(tile.transform, icon, 32f);
        AddLabel(tile.transform, itemName, font, 10f, Color.white, FontStyles.Bold);
        AddLabel(tile.transform, stats, font, 8f, new Color(0.75f, 0.75f, 0.75f), FontStyles.Normal);
        AddBadgeSmall(tile.transform, badge, font);

        spawnedRows.Add(tile);
    }

    private void AddItemRow(TMP_FontAsset font, ItemInstance item)
    {
        var tile = CreateTile(70f);

        AddIcon(tile.transform, item.Image, 30f);
        AddLabel(tile.transform, item.Name, font, 10f, Color.white, FontStyles.Bold);

        string detail = $"x{item.Amount}";
        if (item.AvgPurchasePrice > 0)
            detail += $" (${item.AvgPurchasePrice:N0})";
        AddLabel(tile.transform, detail, font, 8f, new Color(0.7f, 0.7f, 0.7f), FontStyles.Normal);

        spawnedRows.Add(tile);
    }

    private void AddSeparator(TMP_FontAsset font)
    {
        // no-op for grid layout
    }

    // ─── Shared helpers ──────────────────────────────────────

    private GameObject CreateTile(float size)
    {
        var tile = new GameObject("Tile", typeof(RectTransform));
        tile.transform.SetParent(contentParent, false);

        var bg = tile.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.25f, 0.85f);

        var vlg = tile.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 1f;
        vlg.padding = new RectOffset(3, 3, 3, 3);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childAlignment = TextAnchor.UpperCenter;

        return tile;
    }

    private void AddIcon(Transform parent, Sprite sprite, float size)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        le.flexibleWidth = 0f;
    }

    private void AddBadgeSmall(Transform parent, string text, TMP_FontAsset font)
    {
        var go = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = 8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.4f, 1f, 0.4f);
        tmp.alignment = TextAlignmentOptions.Center;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 12f;
    }

    private void AddLabel(Transform parent, string text, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = size;
        tmp.alignment = TextAlignmentOptions.Center;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = size + 6f;
    }

    // ─── Layout & font ───────────────────────────────────────

    private void EnsureLayout()
    {
        if (contentParent == null) contentParent = transform;

        var rt = contentParent as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0, rt.offsetMax.y);
        }

        // Remove any VerticalLayoutGroup that might exist from a previous version
        var oldVlg = contentParent.GetComponent<VerticalLayoutGroup>();
        if (oldVlg != null) Destroy(oldVlg);

        var grid = contentParent.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = contentParent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(80f, 95f);
        grid.spacing = new Vector2(6f, 6f);
        grid.padding = new RectOffset(4, 4, 34, 4);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.Flexible;

        // Let pre-existing children (e.g. Open Shop button) ignore the grid
        foreach (Transform child in contentParent)
        {
            var le = child.GetComponent<LayoutElement>();
            if (le == null) le = child.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        var csf = contentParent.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = contentParent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Add scroll support
        Transform parent = contentParent.parent;
        if (parent != null && parent.GetComponent<ScrollRect>() == null)
        {
            var sr = parent.gameObject.AddComponent<ScrollRect>();
            sr.content = contentParent as RectTransform;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 20f;

            if (parent.GetComponent<RectMask2D>() == null)
                parent.gameObject.AddComponent<RectMask2D>();
        }
    }

    private TMP_FontAsset GetFont()
    {
        if (_cachedFont != null) return _cachedFont;

        // Try to grab from any TMP in the scene
        var existing = FindObjectOfType<TMP_Text>();
        if (existing != null) _cachedFont = existing.font;

        if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
        return _cachedFont;
    }

    private void ClearRows()
    {
        foreach (var go in spawnedRows)
        {
            if (go != null) Destroy(go);
        }
        spawnedRows.Clear();
    }
}
