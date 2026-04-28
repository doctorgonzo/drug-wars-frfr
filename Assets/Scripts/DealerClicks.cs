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

            if (dealer.RuntimeInventory == null || dealer.RuntimeInventory.Count == 0)
                dealer.InitializeRuntimeInventory();

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
        }
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
        foreach (var playerItem in PlayerStats.Instance.inventory)
        {
            GameObject newPlayerItemPrefab = GetPooledItem(playerPool, playerInventoryContent);
            InventoryItemUI playerItemUI = newPlayerItemPrefab.GetComponent<InventoryItemUI>();
            playerItemUI.Setup(playerItem, InventoryContext.Player, dealer);
            SetupTooltipTrigger(newPlayerItemPrefab, playerItemUI);

            var capturedItem = playerItem;
            playerItemUI.OnMinusClicked.AddListener((_) => OnPlayerSellClicked(capturedItem, playerItemUI));
        }
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

        int sellPrice = dealer.GetModifiedSellPrice(playerItem);
        int totalValue = sellPrice * amountToSell;
        int totalProfit = (sellPrice - playerItem.AvgPurchasePrice) * amountToSell;
        PlayerStats.Instance.PlayerWallet += totalValue;
        cityUIHandler.UpdateWalletDisplay();
        playerItem.ChangeAmount(-amountToSell);

        if (ProfitLossPopup.Instance != null)
            ProfitLossPopup.Instance.Show(totalProfit);
        dealerItem.ChangeAmount(amountToSell);
        playerItemUI.RefreshButtonState();

        if (playerItem.Type == ItemType.Drug && heatManager != null)
        {
            heatManager.AddHeat(playerItem.HeatValue * amountToSell);
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
            // Do we already have this drug? (same stack)
            var existing2 = PlayerStats.Instance.inventory.FirstOrDefault(i => i.Name == item.Name);

            if (existing2 == null)
            {
                // This purchase would create a NEW stack; need one free slot
                if (PlayerStats.Instance.GetFreeSlots() <= 0)
                {
                    ShowFeedback("No free slots!");
                    return;
                }
                // Fine: making one new stack. No need to clamp amountToBuy: stacks have no unit limit.
            }
            // else: adding to existing stack uses no slots → proceed
        }
        int buyPrice = dealer.GetModifiedBuyPrice(item);
        int totalCost = buyPrice * amountToBuy;

        if (PlayerStats.Instance.PlayerWallet < totalCost)
        {
            ShowFeedback($"Not enough cash! Need ${totalCost:N0}");
            return;
        }

        PlayerStats.Instance.PlayerWallet -= totalCost;
        cityUIHandler.UpdateWalletDisplay();
        item.ChangeAmount(-amountToBuy);

        if (item.Type == ItemType.Drug && heatManager != null)
        {
            heatManager.AddHeat(item.HeatValue * amountToBuy);
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

