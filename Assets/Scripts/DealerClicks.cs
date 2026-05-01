using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;

public class DealerClicks : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Dealer dealer;
    private CityUIHandler cityUIHandler;
    private Transform playerInventoryContent;
    private GameObject dealerInfoPanel;
    private GameObject inventoryItemPrefab;
    private HeatManager heatManager;

    [Header("Shift-Click")]
    [SerializeField] private int shiftClickAmount = 10;

    private TMP_Text statusText;
    private float feedbackDuration = 1.5f;
    private Coroutine feedbackRoutine;

    private static DealerClicks activeDealer;

    private Dictionary<ItemInstance, InventoryItemUI> dealerItemUIMap = new Dictionary<ItemInstance, InventoryItemUI>();
    private readonly List<GameObject> dealerPool = new List<GameObject>();
    private readonly List<GameObject> playerPool = new List<GameObject>();

    private GameObject _sellAllButton;

    // Contract banner — shown above dealer items when this dealer has an offer/active contract.
    private GameObject _contractBanner;
    private TMP_Text _contractText;
    private Button _contractAcceptBtn;
    private Button _contractDeclineBtn;
    private Button _contractDeliverBtn;
    private bool _subscribedToInventoryChanges;

    private int GetShiftClickAmount() =>
        (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? shiftClickAmount : 1;

    private static void SetupTooltipTrigger(GameObject itemGO, InventoryItemUI itemUI)
    {
        var trigger = itemGO.GetComponentInChildren<ItemTooltipTrigger>();
        if (trigger != null)
        {
            trigger.buttonPlus = itemUI.buttonPlus;
            trigger.buttonMinus = itemUI.buttonMinus;
        }
    }

    public void SetupDealer(Dealer dealerData, CityUIHandler uiHandler, Transform playerInvContent, GameObject dealerInfo, GameObject invItemPrefab, HeatManager heatMgr, TMP_Text feedbackText = null, float feedbackTime = 1.5f)
    {
        this.dealer = dealerData;
        this.cityUIHandler = uiHandler;
        this.playerInventoryContent = playerInvContent;
        this.dealerInfoPanel = dealerInfo;
        this.inventoryItemPrefab = invItemPrefab;
        this.heatManager = heatMgr;
        this.statusText = feedbackText;
        this.feedbackDuration = feedbackTime;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (activeDealer == this)
        {
            dealerInfoPanel.SetActive(false);
            ReturnAllToPool();
            activeDealer = null;
        }
        else
        {
            if (activeDealer != null)
                activeDealer.ReturnAllToPool();

            activeDealer = this;

            // Stable within a day, different per dealer and day.
            int priceSeed = ((dealer.Name ?? "").GetHashCode() & 0x7FFFFFFF)
                            ^ (GameTime.Instance != null ? GameTime.Instance.Day : 0);
            dealer.VisitMultiplier = 0.80f + (float)(new System.Random(priceSeed).NextDouble() * 0.40f);

            if (dealer.RuntimeInventory == null || dealer.RuntimeInventory.Count == 0)
            {
                dealer.InitializeRuntimeInventory();
            }

            PopulateDealerPanel();
            PopulatePlayerPanel();
            dealerInfoPanel.SetActive(true);
        }
    }

    private void ReturnAllToPool()
    {
        // Clear ALL inventory items from the shared dealer panel (not just this dealer's)
        foreach (Transform child in dealerInfoPanel.transform)
        {
            var ui = child.GetComponent<InventoryItemUI>();
            if (ui != null)
            {
                ui.Teardown();
                child.gameObject.SetActive(false);
            }
        }
        dealerItemUIMap.Clear();

        foreach (Transform child in playerInventoryContent)
        {
            var ui = child.GetComponent<InventoryItemUI>();
            if (ui != null) ui.Teardown();
            child.gameObject.SetActive(false);
        }

        // Hide our contract banner — dealerInfoPanel is shared, and another dealer's panel
        // taking over would otherwise see our banner as a sibling.
        if (_contractBanner != null) _contractBanner.SetActive(false);
    }

    private GameObject GetPooledItem(List<GameObject> pool, Transform parent)
    {
        foreach (var obj in pool)
        {
            if (obj != null && !obj.activeSelf)
            {
                obj.transform.SetParent(parent);
                obj.SetActive(true);
                return obj;
            }
        }
        var newObj = Instantiate(inventoryItemPrefab, parent);
        pool.Add(newObj);
        return newObj;
    }

    private void PopulateDealerPanel()
    {
        ReturnDealerItems();
        if (dealer.RuntimeInventory == null || dealer.RuntimeInventory.Count == 0) dealer.InitializeRuntimeInventory();

        foreach (var item in dealer.RuntimeInventory)
        {
            GameObject newInvItemPrefab = GetPooledItem(dealerPool, dealerInfoPanel.transform);
            InventoryItemUI itemUI = newInvItemPrefab.GetComponent<InventoryItemUI>();
            itemUI.Setup(item, InventoryContext.Dealer, dealer);
            SetupTooltipTrigger(newInvItemPrefab, itemUI);

            dealerItemUIMap[item] = itemUI;
            bool playerHasItem = PlayerStats.Instance.inventory.Any(playerItem => playerItem.Name == item.Name && playerItem.Amount > 0);
            itemUI.SetMinusButtonInteractable(playerHasItem);
            var capturedItem = item;
            itemUI.OnPlusClicked.AddListener((_) => OnPlusClicked(capturedItem, itemUI));
            itemUI.OnMinusClicked.AddListener((_) => OnMinusClicked(capturedItem, itemUI));

            if (item.Type == ItemType.Drug)
                PlayerStats.Instance.LastSeenBuyPrice[item.Name] = dealer.GetModifiedBuyPrice(item);
        }

        UpdateContractBanner();
        TrySubscribeInventoryChanges();
    }

    private void ReturnDealerItems()
    {
        // Clear ALL inventory items from the shared dealer panel (not just this dealer's)
        foreach (Transform child in dealerInfoPanel.transform)
        {
            var ui = child.GetComponent<InventoryItemUI>();
            if (ui != null)
            {
                ui.Teardown();
                child.gameObject.SetActive(false);
            }
        }
        dealerItemUIMap.Clear();
    }

    private void ReturnPlayerItems()
    {
        foreach (Transform child in playerInventoryContent)
        {
            var ui = child.GetComponent<InventoryItemUI>();
            if (ui != null) ui.Teardown();
            child.gameObject.SetActive(false);
        }
    }

    private void PopulatePlayerPanel()
    {
        ReturnPlayerItems();

        bool hasDrugs = PlayerStats.Instance.inventory.Any(i => i.Type == ItemType.Drug && i.Amount > 0);
        if (hasDrugs)
        {
            EnsureSellAllButton();
            _sellAllButton.SetActive(true);
            _sellAllButton.transform.SetAsFirstSibling();
        }

        foreach (var playerItem in PlayerStats.Instance.inventory)
        {
            GameObject newPlayerItemPrefab = GetPooledItem(playerPool, playerInventoryContent);
            InventoryItemUI playerItemUI = newPlayerItemPrefab.GetComponent<InventoryItemUI>();
            playerItemUI.Setup(playerItem, InventoryContext.Player, dealer);
            SetupTooltipTrigger(newPlayerItemPrefab, playerItemUI);

            var capturedItem = playerItem;
            playerItemUI.OnMinusClicked.AddListener((_) => OnPlayerSellClicked(capturedItem, playerItemUI));
        }

        var scrollRect = playerInventoryContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    private void EnsureSellAllButton()
    {
        if (_sellAllButton != null) return;

        _sellAllButton = new GameObject("SellAllButton");
        _sellAllButton.transform.SetParent(playerInventoryContent, false);

        var rect = _sellAllButton.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 18);

        var img = _sellAllButton.AddComponent<Image>();
        img.color = new Color(0.75f, 0.15f, 0.15f, 1f);

        var btn = _sellAllButton.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.9f, 0.25f, 0.25f, 1f);
        colors.pressedColor = new Color(0.5f, 0.1f, 0.1f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(SellAll);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(_sellAllButton.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var label = textGO.AddComponent<TextMeshProUGUI>();
        label.text = "SELL ALL DRUGS";
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 10;
    }

    // ------------------------------------------------------------------
    //  Contract banner (procedural UI; lives at the top of the dealer panel)
    // ------------------------------------------------------------------

    private void EnsureContractBanner()
    {
        if (_contractBanner != null) return;
        if (dealerInfoPanel == null) return;

        _contractBanner = new GameObject("ContractBanner");
        _contractBanner.transform.SetParent(dealerInfoPanel.transform, false);

        var rect = _contractBanner.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 60);

        var bg = _contractBanner.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.18f, 0.30f, 0.95f);

        var le = _contractBanner.AddComponent<LayoutElement>();
        le.preferredHeight = 60;
        le.minHeight = 60;

        var vlg = _contractBanner.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 4, 4);
        vlg.spacing = 3;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // Description text
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(_contractBanner.transform, false);
        textGO.AddComponent<RectTransform>();
        var textLE = textGO.AddComponent<LayoutElement>();
        textLE.preferredHeight = 28;
        _contractText = textGO.AddComponent<TextMeshProUGUI>();
        _contractText.alignment = TextAlignmentOptions.Center;
        _contractText.color = Color.white;
        _contractText.enableAutoSizing = true;
        _contractText.fontSizeMin = 8;
        _contractText.fontSizeMax = 14;
        _contractText.fontStyle = FontStyles.Bold;
        _contractText.richText = true;

        // Buttons row
        var btnRow = new GameObject("Buttons");
        btnRow.transform.SetParent(_contractBanner.transform, false);
        btnRow.AddComponent<RectTransform>();
        var rowLE = btnRow.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 22;
        rowLE.minHeight = 22;
        var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.padding = new RectOffset(2, 2, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        _contractAcceptBtn  = BuildBannerButton(btnRow.transform, "ACCEPT",  new Color(0.20f, 0.55f, 0.20f), OnAcceptContract);
        _contractDeclineBtn = BuildBannerButton(btnRow.transform, "DECLINE", new Color(0.45f, 0.45f, 0.45f), OnDeclineContract);
        _contractDeliverBtn = BuildBannerButton(btnRow.transform, "DELIVER", new Color(0.85f, 0.65f, 0.15f), OnDeliverContract);
    }

    private Button BuildBannerButton(Transform parent, string label, Color color, System.Action onClick)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(color.r * 1.2f, color.g * 1.2f, color.b * 1.2f, 1f);
        colors.pressedColor = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var t = textGO.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
        t.fontSize = 9;
        return btn;
    }

    private void UpdateContractBanner()
    {
        if (dealer == null) return;
        var cm = ContractManager.Instance;
        if (cm == null)
        {
            if (_contractBanner != null) _contractBanner.SetActive(false);
            return;
        }

        var offer = cm.GetOfferForDealer(dealer);
        var active = cm.GetActiveContractForDealer(dealer);

        if (offer == null && active == null)
        {
            if (_contractBanner != null) _contractBanner.SetActive(false);
            return;
        }

        EnsureContractBanner();
        _contractBanner.SetActive(true);
        _contractBanner.transform.SetAsFirstSibling();

        int currentDay = GameTime.Instance != null ? GameTime.Instance.Day : 1;

        if (offer != null)
        {
            int daysLeft = Mathf.Max(0, offer.deadlineDay - currentDay);
            int advance = Mathf.RoundToInt(offer.totalPayment * 0.5f);
            _contractText.text =
                $"<color=#FFD700>JOB OFFER</color>  {offer.drugName} × {offer.quantityRequired}  in {daysLeft}d\n" +
                $"<size=85%>${offer.totalPayment:N0}  (${advance:N0} advance)</size>";
            _contractAcceptBtn.gameObject.SetActive(true);
            _contractDeclineBtn.gameObject.SetActive(true);
            _contractDeliverBtn.gameObject.SetActive(false);
        }
        else
        {
            int daysLeft = active.deadlineDay - currentDay;
            var item = PlayerStats.Instance?.inventory
                .FirstOrDefault(i => i.Type == ItemType.Drug && i.Name == active.drugName);
            int playerHas = item != null ? item.Amount : 0;
            bool canDeliver = playerHas >= active.quantityRequired;

            string deadlineStr = daysLeft <= 0
                ? "<color=#FF4444>OVERDUE</color>"
                : $"{daysLeft}d left";
            string ownStr = canDeliver
                ? $"<color=#44FF44>{playerHas}/{active.quantityRequired} ready</color>"
                : $"{playerHas}/{active.quantityRequired}";

            _contractText.text =
                $"<color=#FFD700>DELIVER</color>  {active.drugName} × {active.quantityRequired}  {deadlineStr}\n" +
                $"<size=85%>{ownStr} — final ${active.RemainingPayment:N0}</size>";
            _contractAcceptBtn.gameObject.SetActive(false);
            _contractDeclineBtn.gameObject.SetActive(false);
            _contractDeliverBtn.gameObject.SetActive(true);
            _contractDeliverBtn.interactable = canDeliver;
        }
    }

    private void OnAcceptContract()
    {
        if (dealer == null || ContractManager.Instance == null) return;
        if (ContractManager.Instance.AcceptOffer(dealer))
        {
            cityUIHandler?.UpdateWalletDisplay();
            UpdateContractBanner();
            ShowFeedback("CONTRACT ACCEPTED");
            if (JuiceFX.Instance != null && cityUIHandler != null)
                JuiceFX.Instance.CoinBurstAtUI(cityUIHandler.WalletRect, 12, new Color(1f, 0.85f, 0.2f));
        }
    }

    private void OnDeclineContract()
    {
        if (dealer == null || ContractManager.Instance == null) return;
        ContractManager.Instance.DeclineOffer(dealer);
        UpdateContractBanner();
    }

    private void OnDeliverContract()
    {
        if (dealer == null || ContractManager.Instance == null) return;
        if (ContractManager.Instance.TryDeliver(dealer))
        {
            cityUIHandler?.UpdateWalletDisplay();
            PopulatePlayerPanel();
            UpdateContractBanner();
            ShowFeedback("CONTRACT COMPLETE");
            if (JuiceFX.Instance != null && cityUIHandler != null)
                JuiceFX.Instance.CoinBurstAtUI(cityUIHandler.WalletRect, 30, new Color(1f, 0.85f, 0.2f));
        }
    }

    private void TrySubscribeInventoryChanges()
    {
        if (_subscribedToInventoryChanges) return;
        if (PlayerStats.Instance == null) return;
        PlayerStats.Instance.OnInventoryChanged += OnInventoryChangedRefreshBanner;
        _subscribedToInventoryChanges = true;
    }

    private void OnInventoryChangedRefreshBanner()
    {
        // Refresh button-enabled state when player inventory changes mid-session (e.g., they
        // just bought enough of the contract drug from this same dealer).
        if (_contractBanner != null && _contractBanner.activeInHierarchy)
            UpdateContractBanner();
    }

    private void OnDestroy()
    {
        if (_subscribedToInventoryChanges && PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged -= OnInventoryChangedRefreshBanner;
    }

    private void SellAll()
    {
        var drugsToSell = PlayerStats.Instance.inventory
            .Where(i => i.Type == ItemType.Drug && i.Amount > 0)
            .ToList();
        if (drugsToSell.Count == 0) return;

        int totalProfit = 0;
        int totalValue = 0;

        foreach (var playerItem in drugsToSell)
        {
            int qty = playerItem.Amount;
            int lineValue = dealer.GetSellRevenueForBatch(playerItem, qty);
            int avgUnitPrice = qty > 0 ? lineValue / qty : 0;
            totalValue += lineValue;
            totalProfit += (avgUnitPrice - playerItem.AvgPurchasePrice) * qty;
            PlayerStats.Instance.RecordDrugSell(playerItem.Name, qty, lineValue);

            // Bump market saturation so subsequent sales of this drug at this city see lower prices.
            string sellCityName = PlayerStats.Instance?.CurrentCity?.Name ?? "";
            PlayerStats.Instance?.BumpMarketSaturation(sellCityName, playerItem.Name, qty, playerItem.RiskTier);

            ItemInstance dealerItem = dealer.RuntimeInventory.FirstOrDefault(i => i.Name == playerItem.Name);
            if (dealerItem == null)
            {
                dealerItem = new ItemInstance(playerItem, amountOverride: 0);
                dealer.RuntimeInventory.Add(dealerItem);
            }

            if (heatManager != null)
            {
                float _hm = CityEventManager.GetHeatMult(PlayerStats.Instance?.CurrentCity?.Name ?? "");
                int sellHeat = Mathf.RoundToInt(playerItem.HeatValue * qty * _hm);
                heatManager.AddHeat(sellHeat);
                if (PlayerStats.Instance?.CurrentCity != null)
                    PlayerStats.Instance.BumpCityHeat(PlayerStats.Instance.CurrentCity.Name, sellHeat);
            }

            dealerItem.ChangeAmount(qty);
            playerItem.ChangeAmount(-qty);
        }

        PlayerStats.Instance.inventory.RemoveAll(i => i.Amount <= 0);
        PlayerStats.Instance.PlayerWallet += totalValue;
        PlayerStats.Instance.NotifyInventoryChanged();
        cityUIHandler.UpdateWalletDisplay();

        if (ProfitLossPopup.Instance != null)
            ProfitLossPopup.Instance.Show(totalProfit);

        // Juice: coin shower scaled to the take
        if (JuiceFX.Instance != null && cityUIHandler != null)
        {
            int coinCount = Mathf.Clamp(8 + totalValue / 200, 8, 40);
            JuiceFX.Instance.CoinBurstAtUI(cityUIHandler.WalletRect, coinCount,
                totalProfit >= 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.95f, 0.4f, 0.4f));
        }

        PopulateDealerPanel();
        PopulatePlayerPanel();
    }

    private void OnPlayerSellClicked(ItemInstance playerItem, InventoryItemUI playerItemUI)
    {
        if (playerItem == null || playerItem.Amount <= 0) return;
        int amountToSell = GetShiftClickAmount();
        amountToSell = Mathf.Min(amountToSell, playerItem.Amount);
        if (amountToSell <= 0) return;

        ItemInstance dealerItem = dealer.RuntimeInventory.FirstOrDefault(i => i.Name == playerItem.Name);
        if (dealerItem == null)
        {
            dealerItem = new ItemInstance(playerItem, amountOverride: 0);
            dealer.RuntimeInventory.Add(dealerItem);
            GameObject newDealerItemPrefab = GetPooledItem(dealerPool, dealerInfoPanel.transform);
            InventoryItemUI newDealerItemUI = newDealerItemPrefab.GetComponent<InventoryItemUI>();
            newDealerItemUI.Setup(dealerItem, InventoryContext.Dealer, dealer);
            newDealerItemUI.OnPlusClicked.AddListener((_) => OnPlusClicked(dealerItem, newDealerItemUI));
            newDealerItemUI.OnMinusClicked.AddListener((_) => OnMinusClicked(dealerItem, newDealerItemUI));
            dealerItemUIMap[dealerItem] = newDealerItemUI;
        }

        int totalValue = dealer.GetSellRevenueForBatch(playerItem, amountToSell);
        int avgSellPrice = amountToSell > 0 ? totalValue / amountToSell : 0;
        int totalProfit = (avgSellPrice - playerItem.AvgPurchasePrice) * amountToSell;
        PlayerStats.Instance.PlayerWallet += totalValue;
        if (playerItem.Type == ItemType.Drug)
        {
            PlayerStats.Instance.RecordDrugSell(playerItem.Name, amountToSell, totalValue);
            // Bump saturation so the next sale of this drug here is cheaper.
            string sellCityName = PlayerStats.Instance.CurrentCity?.Name ?? "";
            PlayerStats.Instance.BumpMarketSaturation(sellCityName, playerItem.Name, amountToSell, playerItem.RiskTier);
        }
        cityUIHandler.UpdateWalletDisplay();
        playerItem.ChangeAmount(-amountToSell);

        // Juice: coin burst at wallet on every sale
        if (JuiceFX.Instance != null && cityUIHandler != null)
        {
            int coinCount = Mathf.Clamp(6 + totalValue / 250, 6, 30);
            JuiceFX.Instance.CoinBurstAtUI(cityUIHandler.WalletRect, coinCount,
                totalProfit >= 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.95f, 0.4f, 0.4f));
        }

        if (ProfitLossPopup.Instance != null)
            ProfitLossPopup.Instance.Show(totalProfit);
        dealerItem.ChangeAmount(amountToSell);
        playerItemUI.RefreshButtonState();

        if (playerItem.Type == ItemType.Drug && heatManager != null)
        {
            float _hm = CityEventManager.GetHeatMult(PlayerStats.Instance?.CurrentCity?.Name ?? "");
            int sellHeat = Mathf.RoundToInt(playerItem.HeatValue * amountToSell * _hm);
            heatManager.AddHeat(sellHeat);
            if (PlayerStats.Instance?.CurrentCity != null)
                PlayerStats.Instance.BumpCityHeat(PlayerStats.Instance.CurrentCity.Name, sellHeat);
        }

        if (dealerItemUIMap.ContainsKey(dealerItem))
        {
            dealerItemUIMap[dealerItem].SetMinusButtonInteractable(true);
        }

        if (playerItem.Amount <= 0)
        {
            PlayerStats.Instance.inventory.Remove(playerItem);
            playerItemUI.Teardown();
            playerItemUI.gameObject.SetActive(false);
        }
        PlayerStats.Instance.NotifyInventoryChanged();
        RefreshAllPrices();
    }

    private void OnPlusClicked(ItemInstance item, InventoryItemUI itemUI)
    {
        int amountToBuy = GetShiftClickAmount();
        amountToBuy = Mathf.Min(amountToBuy, item.Amount);

        if (amountToBuy <= 0) return;
        if (item.Type == ItemType.Drug)
        {
            int unitsPerSlot = Mathf.Max(1, item.UnitsPerSlot);
            int maxBuyable = PlayerStats.Instance.GetMaxBuyableAmount(item.Name, unitsPerSlot, item.RiskTier);
            if (maxBuyable <= 0)
            {
                ShowFeedback($"Out of room! Trenchcoat full.");
                return;
            }
            if (amountToBuy > maxBuyable)
            {
                ShowFeedback($"Trenchcoat only has room for {maxBuyable} more.");
                amountToBuy = maxBuyable;
            }
        }
        int buyPrice = dealer.GetModifiedBuyPrice(item);
        int totalCost = buyPrice * amountToBuy;

        if (PlayerStats.Instance.PlayerWallet < totalCost)
        {
            ShowFeedback($"Not enough cash! Need ${totalCost:N0}");
            return;
        }

        PlayerStats.Instance.PlayerWallet -= totalCost;
        if (item.Type == ItemType.Drug)
            PlayerStats.Instance.RecordDrugBuy(amountToBuy, totalCost);
        cityUIHandler.UpdateWalletDisplay();
        item.ChangeAmount(-amountToBuy);

        if (item.Type == ItemType.Drug && heatManager != null)
        {
            float _hm = CityEventManager.GetHeatMult(PlayerStats.Instance?.CurrentCity?.Name ?? "");
            int buyHeat = Mathf.Max(1, Mathf.RoundToInt(item.HeatValue * item.BuyHeatMultiplier * amountToBuy * _hm));
            heatManager.AddHeat(buyHeat);
        }

        ItemInstance existing = PlayerStats.Instance.inventory.FirstOrDefault(i => i.Name == item.Name);
        if (existing != null)
        {
            int oldTotal = existing.Amount * existing.AvgPurchasePrice;
            existing.ChangeAmount(amountToBuy);
            existing.AvgPurchasePrice = (oldTotal + totalCost) / existing.Amount;
        }
        else
        {
            ItemInstance newItem = new ItemInstance(item, amountOverride: amountToBuy);
            newItem.AvgPurchasePrice = buyPrice;
            PlayerStats.Instance.inventory.Add(newItem);
            PopulatePlayerPanel();
        }
        PlayerStats.Instance.NotifyInventoryChanged();
        itemUI.SetMinusButtonInteractable(true);
        RefreshAllPrices();
    }

    private void OnMinusClicked(ItemInstance item, InventoryItemUI itemUI)
    {
        ItemInstance playerItem = PlayerStats.Instance.inventory.FirstOrDefault(i => i.Name == item.Name);
        if (playerItem != null)
        {
            foreach (Transform child in playerInventoryContent)
            {
                var ui = child.GetComponent<InventoryItemUI>();
                if (ui != null && ui.BoundItem == playerItem)
                {
                    OnPlayerSellClicked(playerItem, ui);
                    break;
                }
            }
        }
    }

    private void RefreshAllPrices()
    {
        foreach (var kvp in dealerItemUIMap)
            kvp.Value.RefreshPrice();
        foreach (Transform child in playerInventoryContent)
        {
            var ui = child.GetComponent<InventoryItemUI>();
            if (ui != null) ui.RefreshPrice();
        }
    }

    private void ShowFeedback(string message)
    {
        if (statusText == null) return;
        if (feedbackRoutine != null) StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(FeedbackRoutine(message));
    }

    private IEnumerator FeedbackRoutine(string message)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(true);
        yield return new WaitForSeconds(feedbackDuration);
        statusText.gameObject.SetActive(false);
        feedbackRoutine = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dealer != null)
        {
            TooltipUI.Instance.ShowTooltip(dealer.Name, dealer.Description);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipUI.Instance.HideTooltip();
    }
}

