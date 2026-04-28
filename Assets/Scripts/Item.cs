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

    // This will hold the heat value for this specific instance.
    public int HeatValue;

    // Tracks the average price per unit the player paid (for profit/loss feedback).
    public int AvgPurchasePrice;

    public event Action<int> OnAmountChanged;

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

