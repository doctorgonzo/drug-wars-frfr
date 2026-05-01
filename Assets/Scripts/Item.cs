using System;
using UnityEngine;

public enum ItemType
{
    Drug,
    Weapon,
    Trenchcoat
}

// This class is a TEMPLATE for an item. It should only hold the base, unchanging data.
// Your asset files in the project are of this type.
public class Item : ScriptableObject
{
    public string Name;
    public string Description;
    public int Cost;
    public int Amount; // The starting amount for a dealer
    public ItemType Type;
    public Sprite Image;
}

// --- ItemInstance is BACK ---
// This class is a simple data container that holds the RUNTIME data for an item.
// It is a copy of the data from the Item asset and is what your inventories will use.
[System.Serializable]
public class ItemInstance
{

    public string Name;
    public string Description;
    public int Cost;
    public int Amount; // The current amount in an inventory
    public ItemType Type;
    public Sprite Image;

    public int HeatValue;
    public float BuyHeatMultiplier = 1f;
    public int RiskTier; // DrugRiskTier cast to int; 0 for non-drugs

    // How many units fit in one trenchcoat slot. Bulky drugs use small values.
    public int UnitsPerSlot = 30;

    // Three-tier purity: Cut / Standard / Pure. Drives a multiplier stack on cost, sell, slot
    // density, and heat per unit. A drug stack is keyed by (Name, Quality) — different qualities
    // never merge into the same inventory entry.
    public DrugQuality Quality = DrugQuality.Standard;

    // Tracks the average price per unit the player paid (for profit/loss feedback).
    public int AvgPurchasePrice;

    public event Action<int> OnAmountChanged;

    // Display name with quality prefix ("Pure Crack" / "Crack" / "Cut Crack"). Non-drugs ignore
    // quality and just return Name.
    public string DisplayName => Type == ItemType.Drug
        ? DrugQualityX.Prefix(Quality) + Name
        : Name;

    // Stack-identity check: two ItemInstances merge into the same player-inventory entry iff
    // they share Name AND Quality. Quality always matches for non-drugs.
    public bool MatchesStack(ItemInstance other)
    {
        if (other == null) return false;
        if (Name != other.Name) return false;
        if (Type != other.Type) return false;
        if (Type == ItemType.Drug && Quality != other.Quality) return false;
        return true;
    }

    public bool MatchesStack(string name, DrugQuality quality, ItemType type)
    {
        if (Name != name) return false;
        if (Type != type) return false;
        if (type == ItemType.Drug && Quality != quality) return false;
        return true;
    }

    // The constructor now correctly copies all data, including HeatValue.
    public ItemInstance(Item item)
    {
        Name = item.Name;
        Description = item.Description;
        Cost = item.Cost;
        Amount = item.Amount;
        Type = item.Type;
        Image = item.Image;

        // This is the critical step that was missing before.
        // We check if the template item is a Drug, and if so, copy its HeatValue.
        if (item is Drug drugItem)
        {
            this.HeatValue = drugItem.HeatValue;
            this.BuyHeatMultiplier = drugItem.BuyHeatMultiplier;
            this.RiskTier = (int)drugItem.RiskTier;
            this.UnitsPerSlot = Mathf.Max(1, drugItem.UnitsPerSlot);
        }
    }

    // Copy constructor: creates a clone of an existing instance with an optional amount override.
    public ItemInstance(ItemInstance other, int? amountOverride = null)
    {
        Name = other.Name;
        Description = other.Description;
        Cost = other.Cost;
        Amount = amountOverride ?? other.Amount;
        Type = other.Type;
        Image = other.Image;
        HeatValue = other.HeatValue;
        BuyHeatMultiplier = other.BuyHeatMultiplier;
        RiskTier = other.RiskTier;
        UnitsPerSlot = other.UnitsPerSlot;
        Quality = other.Quality;
        AvgPurchasePrice = other.AvgPurchasePrice;
    }

    // A default constructor is needed for creating empty instances.
    public ItemInstance() { }

    public void ChangeAmount(int delta)
    {
        Amount += delta;
        OnAmountChanged?.Invoke(Amount);
    }
}

