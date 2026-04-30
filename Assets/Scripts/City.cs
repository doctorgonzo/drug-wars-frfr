using UnityEngine;
using System.Collections.Generic;

// Per-drug sell bonus for a specific city.
// Stacks multiplicatively on top of the dealer's base sell ratio.
// Lets each city pay a premium for its "home" drugs, creating meaningful trade routes.
[System.Serializable]
public class CityDrugBonus
{
    [Tooltip("Must match the Item SO's Name field exactly (e.g. 'LSD', 'Crack', 'Weed').")]
    public string drugName;
    [Range(1f, 3f)]
    [Tooltip("Sell price multiplier applied when the player sells this drug here.")]
    public float sellMultiplier = 1.5f;
}

[System.Serializable]
public class CityPriceModifier
{
    public ItemType itemType;
    [Range(0.25f, 3f)] public float buyPriceMultiplier = 1f; // affects Buy price
    [Range(0.1f, 1.0f)] public float sellPriceRatio = 0.5f;  // affects Sell price
    [Header("Daily Volatility (0 = none, 0.3 = �30%)")]
    [Range(0f, 0.6f)] public float dailyVolatility = 0.2f;

    [Header("Market Events")]
    [Range(0f, 1f)] public float boomChance = 0.05f;
    [Range(1f, 4f)] public float boomMultiplier = 1.6f;
    [Range(0f, 1f)] public float bustChance = 0.05f;
    [Range(0.1f, 1f)] public float bustMultiplier = 0.6f;
}

[CreateAssetMenu(fileName = "City", menuName = "Scriptable Objects/City")]
public class City : ScriptableObject
{
    public string Name;
    public string Description;
    public Sprite Image;
    public int Population;
    public Drug FavoriteDrug;
    public string SceneName;
    public Dealer[] Dealers;

    [Header("Economy")]
    [Tooltip("General cost-of-living factor for ALL items in this city.")]
    [Range(0.5f, 2.0f)] public float costOfLiving = 1f;

    [Tooltip("Extra multiplier applied to the city's FavoriteDrug (on top of itemType modifier).")]
    [Range(1f, 3f)] public float favoriteDrugDemandMultiplier = 1.25f;

    [Tooltip("Per ItemType price behavior in this city.")]
    public List<CityPriceModifier> priceModifiers = new List<CityPriceModifier>();

    [Tooltip("Drug-specific sell bonuses. Players earn a premium when selling these drugs here.")]
    public List<CityDrugBonus> drugBonuses = new List<CityDrugBonus>();

    /// <summary>Returns the sell bonus multiplier for the named drug in this city (1.0 if none).</summary>
    public float GetDrugSellBonus(string itemName)
    {
        if (drugBonuses == null) return 1f;
        foreach (var bonus in drugBonuses)
            if (!string.IsNullOrEmpty(bonus.drugName) &&
                string.Equals(bonus.drugName, itemName, System.StringComparison.OrdinalIgnoreCase))
                return bonus.sellMultiplier;
        return 1f;
    }
}
