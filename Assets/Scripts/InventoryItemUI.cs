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

    // --- MODIFIED: Setup now correctly accepts an ItemInstance ---
    public void Setup(ItemInstance item, InventoryContext context, Dealer dealer)
    {
        boundItem = item;
        this.context = context;
        this.dealerReference = dealer;
        iconImage.sprite = item.Image;
        itemAmountText.text = "Available: " + item.Amount;
        boundItem.OnAmountChanged += HandleAmountChanged;

        if (dealer == null)
        {
            itemPriceText.text = "Price: N/A";
            return;
        }

        switch (context)
        {
            case InventoryContext.Dealer:
                int buyPrice = dealer.GetModifiedBuyPrice(boundItem);
                itemPriceText.text = $"Buy: ${buyPrice:N0}";
                buttonPlus.gameObject.SetActive(true);
                buttonMinus.gameObject.SetActive(true);
                buttonPlus.onClick.RemoveAllListeners();
                buttonPlus.onClick.AddListener(() => OnPlusClicked.Invoke(boundItem));
                buttonMinus.onClick.RemoveAllListeners();
                buttonMinus.onClick.AddListener(() => OnMinusClicked.Invoke(boundItem));
                break;
            case InventoryContext.Player:
                int sellPrice = dealer.GetModifiedSellPrice(boundItem);
                itemPriceText.text = $"Sell: ${sellPrice:N0}";
                buttonPlus.gameObject.SetActive(false);
                RefreshButtonState(); // Call this to set the minus button's visibility
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

    // Recalculate the displayed price (call when reopening the panel or after day changes)
    public void RefreshPrice()
    {
        if (boundItem == null || dealerReference == null) return;
        switch (context)
        {
            case InventoryContext.Dealer:
                itemPriceText.text = $"Buy: ${dealerReference.GetModifiedBuyPrice(boundItem):N0}";
                break;
            case InventoryContext.Player:
                itemPriceText.text = $"Sell: ${dealerReference.GetModifiedSellPrice(boundItem):N0}";
                break;
        }
    }

    private void HandleAmountChanged(int newAmount)
    {
        itemAmountText.text = "Available: " + newAmount;
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

