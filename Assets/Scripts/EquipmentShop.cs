using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EquipmentShop : MonoBehaviour
{
    [Header("Shop Inventory")]
    [SerializeField] private Trenchcoat[] trenchcoatsForSale;
    [SerializeField] private Weapon[] weaponsForSale;

    [Header("UI — Shop Panel")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button openShopButton;
    [SerializeField] private Button closeShopButton;
    [SerializeField] private Transform itemListContent;
    [SerializeField] private GameObject shopItemPrefab;

    [Header("UI — Feedback")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private CityUIHandler cityUIHandler;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private void Start()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (openShopButton != null)
            openShopButton.onClick.AddListener(OpenShop);

        if (closeShopButton != null)
            closeShopButton.onClick.AddListener(CloseShop);
    }

    public void OpenShop()
    {
        PopulateShop();
        shopPanel.transform.SetAsLastSibling(); // render on top of other siblings
        shopPanel.SetActive(true);
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);
        ClearItems();
    }

    private void PopulateShop()
    {
        ClearItems();
        EnsureContentLayout();
        var ps = PlayerStats.Instance;

        // Trenchcoats
        foreach (var trench in trenchcoatsForSale)
        {
            if (trench == null) continue;
            bool isOwned = ps != null && ps.CurrentTrench == trench;
            CreateShopEntry(
                trench.Image,
                trench.Name,
                $"Slots: {trench.StorageSlots}  |  Armor: {trench.ArmorValue}",
                trench.Cost,
                isOwned,
                () => BuyTrenchcoat(trench)
            );
        }

        // Weapons
        foreach (var weapon in weaponsForSale)
        {
            if (weapon == null) continue;
            bool isOwned = ps != null && ps.CurrentWeapon == weapon;
            CreateShopEntry(
                weapon.Image,
                weapon.Name,
                BuildWeaponStatsLine(weapon),
                weapon.Cost,
                isOwned,
                () => BuyWeapon(weapon)
            );
        }
    }

    private static string BuildWeaponStatsLine(Weapon weapon)
    {
        var parts = new System.Collections.Generic.List<string> { $"Damage: {weapon.Damage}" };
        if (weapon.RunSuccessBonus > 0f) parts.Add($"Run +{weapon.RunSuccessBonus * 100f:F0}%");
        if (weapon.BribeLeverage > 0f)   parts.Add($"Bribe -{weapon.BribeLeverage * 100f:F0}%");
        if (weapon.PenaltyReduction > 0f) parts.Add($"Penalty -{weapon.PenaltyReduction * 100f:F0}%");
        return string.Join("  |  ", parts);
    }

    private void EnsureContentLayout()
    {
        if (itemListContent == null) return;

        // --- Wrap ItemListContent in a ScrollRect (once) ---
        EnsureScrollView();

        // Content RT: left 60% of parent, top-anchored so ContentSizeFitter drives height
        var contentRT = itemListContent as RectTransform;
        if (contentRT != null)
        {
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(0.6f, 1);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.offsetMin = new Vector2(0, contentRT.offsetMin.y);
            contentRT.offsetMax = new Vector2(0, 0);
        }

        var vlg = itemListContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = itemListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
        }
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        var csf = itemListContent.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = itemListContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureScrollView()
    {
        // Add ScrollRect + RectMask2D directly to the parent (ShopPanel) — no reparenting needed
        Transform parent = itemListContent.parent;
        if (parent == null) return;
        if (parent.GetComponent<ScrollRect>() != null) return; // already set up

        var scrollRect = parent.gameObject.AddComponent<ScrollRect>();
        scrollRect.content = itemListContent as RectTransform;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.scrollSensitivity = 30f;

        // Clip overflow so items beyond the panel edges aren't visible
        if (parent.GetComponent<RectMask2D>() == null)
            parent.gameObject.AddComponent<RectMask2D>();
    }

    private TMP_FontAsset _cachedFont;
    private TMP_FontAsset GetFont()
    {
        if (_cachedFont != null) return _cachedFont;
        if (shopItemPrefab != null)
        {
            var tmp = shopItemPrefab.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) _cachedFont = tmp.font;
        }
        if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
        return _cachedFont;
    }

    private void CreateShopEntry(Sprite icon, string itemName, string stats, int cost, bool isOwned, System.Action onBuy)
    {
        var font = GetFont();

        // --- Card root ---
        var card = new GameObject(itemName + "_Card", typeof(RectTransform));
        card.transform.SetParent(itemListContent, false);
        spawnedItems.Add(card);

        var cardLE = card.AddComponent<LayoutElement>();
        cardLE.preferredHeight = 120f;
        cardLE.minHeight = 120f;

        var cardBG = card.AddComponent<Image>();
        cardBG.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        var cardHLG = card.AddComponent<HorizontalLayoutGroup>();
        cardHLG.spacing = 10f;
        cardHLG.padding = new RectOffset(10, 10, 10, 10);
        cardHLG.childForceExpandWidth = false;
        cardHLG.childForceExpandHeight = true;
        cardHLG.childControlWidth = true;
        cardHLG.childControlHeight = true;
        cardHLG.childAlignment = TextAnchor.MiddleLeft;

        // --- Icon (left) ---
        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGO.transform.SetParent(card.transform, false);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 80f;
        iconLE.preferredHeight = 80f;
        iconLE.flexibleWidth = 0f;

        // --- Info column (center, stretches) ---
        var infoGO = new GameObject("Info", typeof(RectTransform));
        infoGO.transform.SetParent(card.transform, false);
        var infoVLG = infoGO.AddComponent<VerticalLayoutGroup>();
        infoVLG.spacing = 2f;
        infoVLG.childForceExpandWidth = true;
        infoVLG.childForceExpandHeight = false;
        infoVLG.childControlWidth = true;
        infoVLG.childControlHeight = true;
        infoVLG.childAlignment = TextAnchor.MiddleLeft;
        var infoLE = infoGO.AddComponent<LayoutElement>();
        infoLE.flexibleWidth = 1f;

        AddLabel(infoGO.transform, itemName, font, 20f, Color.white, FontStyles.Bold);
        AddLabel(infoGO.transform, stats, font, 14f, new Color(0.8f, 0.8f, 0.8f), FontStyles.Normal);
        AddLabel(infoGO.transform, $"${cost:N0}", font, 16f, new Color(0.4f, 1f, 0.4f), FontStyles.Bold);

        // --- Buy button (right) ---
        var btnGO = new GameObject("BuyBtn", typeof(RectTransform), typeof(CanvasRenderer));
        btnGO.transform.SetParent(card.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = isOwned ? new Color(0.4f, 0.4f, 0.4f) : Color.white;
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var btnLE = btnGO.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 90f;
        btnLE.flexibleWidth = 0f;

        var btnTextGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        var btnTextRT = btnTextGO.GetComponent<RectTransform>();
        btnTextRT.anchorMin = Vector2.zero;
        btnTextRT.anchorMax = Vector2.one;
        btnTextRT.offsetMin = Vector2.zero;
        btnTextRT.offsetMax = Vector2.zero;
        var btnTMP = btnTextGO.AddComponent<TextMeshProUGUI>();
        btnTMP.font = font;
        btnTMP.text = isOwned ? "EQUIPPED" : "BUY";
        btnTMP.fontSize = 16f;
        btnTMP.enableAutoSizing = true;
        btnTMP.fontSizeMin = 10f;
        btnTMP.fontSizeMax = 18f;
        btnTMP.alignment = TextAlignmentOptions.Center;
        btnTMP.color = new Color(0.15f, 0.15f, 0.15f);

        if (isOwned)
            btn.interactable = false;
        else
            btn.onClick.AddListener(() => { onBuy?.Invoke(); PopulateShop(); });

        LayoutRebuilder.ForceRebuildLayoutImmediate(itemListContent as RectTransform);
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
        tmp.fontSizeMin = 10f;
        tmp.fontSizeMax = size;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = size + 8f;
    }

    private void BuyTrenchcoat(Trenchcoat trench)
    {
        var ps = PlayerStats.Instance;
        if (ps == null) { ShowFeedback("PlayerStats not found!"); return; }

        // Calculate upgrade cost (full price minus trade-in value of current)
        int tradeIn = ps.CurrentTrench != null ? ps.CurrentTrench.Cost / 2 : 0;
        int upgradeCost = trench.Cost - tradeIn;

        if (ps.PlayerWallet < upgradeCost)
        {
            ShowFeedback($"Not enough cash! Need ${upgradeCost:N0} (after ${tradeIn:N0} trade-in)");
            return;
        }

        ps.PlayerWallet -= upgradeCost;
        ps.RecordEquipmentBuy(upgradeCost);
        ps.CurrentTrench = trench;
        ps.NotifyInventoryChanged();

        ShowFeedback($"Equipped {trench.Name}! (Paid ${upgradeCost:N0} after trade-in)");
        RefreshCityUI();
    }

    private void BuyWeapon(Weapon weapon)
    {
        var ps = PlayerStats.Instance;
        if (ps == null) { ShowFeedback("PlayerStats not found!"); return; }

        int tradeIn = ps.CurrentWeapon != null ? ps.CurrentWeapon.Cost / 2 : 0;
        int upgradeCost = weapon.Cost - tradeIn;

        if (ps.PlayerWallet < upgradeCost)
        {
            ShowFeedback($"Not enough cash! Need ${upgradeCost:N0} (after ${tradeIn:N0} trade-in)");
            return;
        }

        ps.PlayerWallet -= upgradeCost;
        ps.RecordEquipmentBuy(upgradeCost);
        ps.CurrentWeapon = weapon;

        ShowFeedback($"Equipped {weapon.Name}! (Paid ${upgradeCost:N0} after trade-in)");
        RefreshCityUI();
    }

    private void ShowFeedback(string msg)
    {
        if (feedbackText == null) return;
        feedbackText.text = msg;
        feedbackText.gameObject.SetActive(true);
        CancelInvoke(nameof(HideFeedback));
        Invoke(nameof(HideFeedback), 3f);
    }

    private void HideFeedback()
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }

    private void RefreshCityUI()
    {
        if (cityUIHandler != null)
            cityUIHandler.UpdateWalletDisplay();
    }

    private void ClearItems()
    {
        foreach (var go in spawnedItems)
        {
            if (go != null) Destroy(go);
        }
        spawnedItems.Clear();
    }
}
