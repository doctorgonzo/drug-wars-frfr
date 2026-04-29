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

    public int GetUsedSlots()
    {
        int stacks =
            inventory.Where(it => it.Type == ItemType.Drug && it.Amount > 0)
                     .Select(it => it.Name)
                     .Distinct()
                     .Count();
        return stacks;
    }

    public int GetFreeSlots()
    {
        return Mathf.Max(0, GetTotalSlots() - GetUsedSlots());
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
