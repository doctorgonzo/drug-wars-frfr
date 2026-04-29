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
                itemPriceText.text = BuildDealerPriceText(buyPrice, boundItem.Name);
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
                itemPriceText.text = BuildDealerPriceText(buy, boundItem.Name);
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
        if (avgPaid <= 0) return $"Sell: ${sellPrice:N0}";
        int profit = sellPrice - avgPaid;
        string profitStr = profit >= 0
            ? $"<color=#55FF55>+${profit:N0}</color>"
            : $"<color=#FF5555>-${Mathf.Abs(profit):N0}</color>";
        return $"Sell: ${sellPrice:N0}  {profitStr}/unit\n<size=70%>Paid avg: ${avgPaid:N0}</size>";
    }

    private string BuildDealerPriceText(int buyPrice, string drugName)
    {
        if (PlayerStats.Instance != null &&
            PlayerStats.Instance.LastSeenBuyPrice.TryGetValue(drugName, out int lastPrice) &&
            lastPrice != buyPrice)
        {
            string dir = buyPrice < lastPrice ? "<color=#55FF55>▼</color>" : "<color=#FF5555>▲</color>";
            return $"Buy: ${buyPrice:N0} {dir}<size=70%> was ${lastPrice:N0}</size>";
        }
        return $"Buy: ${buyPrice:N0}";
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
            TooltipUI.Instance.ShowTooltip(boundItem.Name, boundItem.Description);
        }
    }

    public void HideItemTooltip()
    {
        TooltipUI.Instance.HideTooltip();
    }
}

