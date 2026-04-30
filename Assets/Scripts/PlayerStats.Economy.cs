using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


// Partial class: economy-related state (wallet, inventory, slots)
public partial class PlayerStats
{
    [SerializeField] private int playerWallet = 10000;

    // Session-only: last buy price seen for each drug (cleared on new game, not saved)
    [NonSerialized] public Dictionary<string, int> LastSeenBuyPrice = new Dictionary<string, int>();

    public event Action<int> OnWalletChanged;
    public event Action OnInventoryChanged;

    public int PlayerWallet
    {
        get => playerWallet;
        set
        {
            if (playerWallet == value) return;
            playerWallet = value;
            OnWalletChanged?.Invoke(playerWallet);
        }
    }

    public List<ItemInstance> inventory = new List<ItemInstance>();

    public int GetTotalSlots()
    {
        return CurrentTrench != null ? CurrentTrench.StorageSlots : 0;
    }

    // Slots used = sum across drug stacks of ceil(amount / UnitsPerSlot).
    // Bulky drugs (low UnitsPerSlot) consume more slots per stack, so volume actually matters.
    public int GetUsedSlots()
    {
        int slots = 0;
        foreach (var it in inventory)
        {
            if (it == null || it.Type != ItemType.Drug || it.Amount <= 0) continue;
            int per = Mathf.Max(1, it.UnitsPerSlot);
            slots += Mathf.CeilToInt((float)it.Amount / per);
        }
        return slots;
    }

    public int GetFreeSlots()
    {
        return Mathf.Max(0, GetTotalSlots() - GetUsedSlots());
    }

    // How many slots a hypothetical buy would add. Accounts for the existing stack of the same
    // drug (so adding 5 weed onto an existing 25-weed stack might cost 0 or 1 additional slots
    // depending on the per-slot capacity).
    public int GetSlotCostForBuy(string drugName, int amountToAdd, int unitsPerSlot)
    {
        if (amountToAdd <= 0) return 0;
        int per = Mathf.Max(1, unitsPerSlot);
        int existingAmt = 0;
        var existing = inventory.FirstOrDefault(i => i != null && i.Name == drugName && i.Type == ItemType.Drug);
        if (existing != null) existingAmt = existing.Amount;
        int slotsBefore = existingAmt > 0 ? Mathf.CeilToInt((float)existingAmt / per) : 0;
        int slotsAfter = Mathf.CeilToInt((float)(existingAmt + amountToAdd) / per);
        return Mathf.Max(0, slotsAfter - slotsBefore);
    }

    // Largest amount of this drug the player can still buy without exceeding total slot capacity.
    public int GetMaxBuyableAmount(string drugName, int unitsPerSlot)
    {
        int per = Mathf.Max(1, unitsPerSlot);
        int existingAmt = 0;
        var existing = inventory.FirstOrDefault(i => i != null && i.Name == drugName && i.Type == ItemType.Drug);
        if (existing != null) existingAmt = existing.Amount;
        int existingStackSlots = existingAmt > 0 ? Mathf.CeilToInt((float)existingAmt / per) : 0;
        int otherStacksSlots = GetUsedSlots() - existingStackSlots;
        int slotsAvailableToThisStack = Mathf.Max(0, GetTotalSlots() - otherStacksSlots);
        int maxStackUnits = slotsAvailableToThisStack * per;
        return Mathf.Max(0, maxStackUnits - existingAmt);
    }

    public int NetWorth =>
        PlayerWallet + inventory
            .Where(it => it != null && it.Type == ItemType.Drug && it.Amount > 0)
            .Sum(it => Mathf.RoundToInt(it.AvgPurchasePrice * it.Amount));

    // Call this after you mutate inventory (buy/sell/add/remove)
    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}
