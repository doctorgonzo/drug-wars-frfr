using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Linq;

public enum InventoryContext
{
    Dealer,
    Player
}

public class InventoryItemUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text itemPriceText;
    public TMP_Text itemAmountText;
    public Button buttonPlus;
    public Button buttonMinus;

    // --- MODIFIED: Events now correctly use ItemInstance ---
    public UnityEvent<ItemInstance> OnPlusClicked = new UnityEvent<ItemInstance>();
    public UnityEvent<ItemInstance> OnMinusClicked = new UnityEvent<ItemInstance>();

    private ItemInstance boundItem;
    private InventoryContext context;
    public ItemInstance BoundItem => boundItem;
    private Dealer dealerReference;
    private int cachedAvgPurchasePrice;

    // --- MODIFIED: Setup now correctly accepts an ItemInstance ---
    public void Setup(ItemInstance item, InventoryContext context, Dealer dealer)
    {
        boundItem = item;
        this.context = context;
        this.dealerReference = dealer;
        this.cachedAvgPurchasePrice = item.AvgPurchasePrice;
        iconImage.sprite = item.Image;
        boundItem.OnAmountChanged += HandleAmountChanged;

        itemPriceText.enableAutoSizing = true;
        itemPriceText.fontSizeMin = 5f;
        itemPriceText.fontSizeMax = 14f;
        itemAmountText.enableAutoSizing = true;
        itemAmountText.fontSizeMin = 5f;
        itemAmountText.fontSizeMax = 14f;

        if (dealer == null)
        {
            itemPriceText.text = "Price: N/A";
            itemAmountText.text = "Available: " + item.Amount;
            return;
        }

        switch (context)
        {
            case InventoryContext.Dealer:
                int buyPrice = dealer.GetModifiedBuyPrice(boundItem);
                itemPriceText.text = BuildDealerPriceText(buyPrice, boundItem);
                itemAmountText.text = "Stock: " + item.Amount;
                buttonPlus.gameObject.SetActive(true);
                buttonMinus.gameObject.SetActive(true);
                buttonPlus.onClick.RemoveAllListeners();
                buttonPlus.onClick.AddListener(() => OnPlusClicked.Invoke(boundItem));
                buttonMinus.onClick.RemoveAllListeners();
                buttonMinus.onClick.AddListener(() => OnMinusClicked.Invoke(boundItem));
                break;
            case InventoryContext.Player:
                int sellPrice = dealer.GetModifiedSellPrice(boundItem);
                itemPriceText.text = BuildPlayerPriceText(sellPrice, cachedAvgPurchasePrice);
                itemAmountText.text = "Qty: " + item.Amount;
                buttonPlus.gameObject.SetActive(false);
                RefreshButtonState();
                buttonMinus.onClick.RemoveAllListeners();
                buttonMinus.onClick.AddListener(() => OnMinusClicked.Invoke(boundItem));
                break;
        }
    }

    public void RefreshButtonState()
    {
        if (context != InventoryContext.Player || dealerReference == null) return;
        buttonMinus.gameObject.SetActive(boundItem != null && boundItem.Amount > 0);
    }

    public void RefreshPrice()
    {
        if (boundItem == null || dealerReference == null) return;
        switch (context)
        {
            case InventoryContext.Dealer:
                int buy = dealerReference.GetModifiedBuyPrice(boundItem);
                itemPriceText.text = BuildDealerPriceText(buy, boundItem);
                break;
            case InventoryContext.Player:
                int sell = dealerReference.GetModifiedSellPrice(boundItem);
                itemPriceText.text = BuildPlayerPriceText(sell, cachedAvgPurchasePrice);
                break;
        }
    }

    private void HandleAmountChanged(int newAmount)
    {
        itemAmountText.text = context == InventoryContext.Player
            ? $"Qty: {newAmount}"
            : $"Stock: {newAmount}";
    }

    private string BuildPlayerPriceText(int sellPrice, int avgPaid)
    {
        // Same quality badge convention as the dealer side — only Cut/Pure get a prefix.
        string qualityPrefix = "";
        if (boundItem != null && boundItem.Type == ItemType.Drug && boundItem.Quality != DrugQuality.Standard)
        {
            string label = boundItem.Quality == DrugQuality.Pure ? "PURE" : "CUT";
            qualityPrefix = $"<color={DrugQualityX.BadgeHex(boundItem.Quality)}>[{label}]</color> ";
        }

        if (avgPaid <= 0) return $"{qualityPrefix}Sell: ${sellPrice:N0}";
        int profit = sellPrice - avgPaid;
        string profitStr = profit >= 0
            ? $"<color=#55FF55>+${profit:N0}</color>"
            : $"<color=#FF5555>-${Mathf.Abs(profit):N0}</color>";
        return $"{qualityPrefix}Sell: ${sellPrice:N0}  {profitStr}/unit\n<size=70%>Paid avg: ${avgPaid:N0}</size>";
    }

    private string BuildDealerPriceText(int buyPrice, ItemInstance item)
    {
        // Quality badge for drug stacks: "[PURE]" / "[CUT]" prefix in the quality color. Standard
        // is implicit (no badge) to keep the line short for the common case.
        string qualityPrefix = "";
        if (item.Type == ItemType.Drug && item.Quality != DrugQuality.Standard)
        {
            string label = item.Quality == DrugQuality.Pure ? "PURE" : "CUT";
            qualityPrefix = $"<color={DrugQualityX.BadgeHex(item.Quality)}>[{label}]</color> ";
        }

        if (PlayerStats.Instance != null &&
            PlayerStats.Instance.LastSeenBuyPrice.TryGetValue(DealerClicks.BuildPriceKey(item), out int lastPrice) &&
            lastPrice != buyPrice)
        {
            string dir = buyPrice < lastPrice ? "<color=#55FF55>▼</color>" : "<color=#FF5555>▲</color>";
            return $"{qualityPrefix}Buy: ${buyPrice:N0} {dir}<size=70%> was ${lastPrice:N0}</size>";
        }
        return $"{qualityPrefix}Buy: ${buyPrice:N0}";
    }

    public void Teardown()
    {
        if (boundItem != null)
            boundItem.OnAmountChanged -= HandleAmountChanged;
        boundItem = null;
        dealerReference = null;
        OnPlusClicked.RemoveAllListeners();
        OnMinusClicked.RemoveAllListeners();
        buttonPlus.onClick.RemoveAllListeners();
        buttonMinus.onClick.RemoveAllListeners();
    }

    private void OnDestroy()
    {
        if (boundItem != null)
            boundItem.OnAmountChanged -= HandleAmountChanged;
    }

    public void SetMinusButtonInteractable(bool interactable)
    {
        buttonMinus.interactable = interactable;
    }

    public void ShowItemTooltip()
    {
        if (boundItem != null)
        {
            string desc = boundItem.Description ?? "";
            if (boundItem.Type == ItemType.Drug)
            {
                // Quality line: "PURE — premium product (1.6x cost / 1.55x sell / 0.7x heat)"
                if (boundItem.Quality != DrugQuality.Standard)
                {
                    string qLabel = boundItem.Quality == DrugQuality.Pure ? "PURE" : "CUT";
                    string qSummary = boundItem.Quality == DrugQuality.Pure
                        ? "premium — pricier, denser, quieter"
                        : "cut with filler — cheap, bulky, hot";
                    if (!string.IsNullOrEmpty(desc)) desc += "\n";
                    desc += $"<color={DrugQualityX.BadgeHex(boundItem.Quality)}>[{qLabel}]</color> <size=85%>{qSummary}</size>";
                }
                if (boundItem.UnitsPerSlot > 0)
                {
                    int effectivePerSlot = Mathf.RoundToInt(boundItem.UnitsPerSlot * DrugQualityX.UnitsPerSlotMult(boundItem.Quality));
                    if (!string.IsNullOrEmpty(desc)) desc += "\n";
                    desc += $"<size=85%><color=#AAAAAA>Bulk: {effectivePerSlot} units per slot</color></size>";
                }
            }
            TooltipUI.Instance.ShowTooltip(boundItem.DisplayName, desc);
        }
    }

    public void HideItemTooltip()
    {
        TooltipUI.Instance.HideTooltip();
    }
}

