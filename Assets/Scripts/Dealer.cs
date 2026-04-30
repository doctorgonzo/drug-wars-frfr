using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ItemPriceModifier
{
    public ItemType itemType;
    [Range(0.5f, 2.0f)]
    public float buyPriceMultiplier = 1.0f;
    [Range(0.1f, 1.0f)]
    public float sellPriceRatio = 0.5f;
}

[CreateAssetMenu(fileName = "Dealer", menuName = "Scriptable Objects/Dealer")]
public class Dealer : ScriptableObject
{
    public string Name;
    public string Description;
    public Sprite Image;
    public int Wallet;
    public Item[] Inventory;
    public List<ItemPriceModifier> priceModifiers;

    // Re-rolled each visit; applies a ±20% swing on top of all other price factors.
    [System.NonSerialized] public float VisitMultiplier = 1f;

    // Runtime inventory now lives in GameSessionManager to avoid mutating this ScriptableObject.
    // This property provides backward-compatible access.
    public List<ItemInstance> RuntimeInventory =>
        GameSessionManager.Instance != null ? GameSessionManager.Instance.GetRuntimeInventory(this) : new List<ItemInstance>();

    private static CityPriceModifier FindCityMod(City city, ItemType type)
    {
        if (city == null || city.priceModifiers == null) return null;
        return city.priceModifiers.FirstOrDefault(m => m.itemType == type);
    }

    public void InitializeRuntimeInventory()
    {
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.InitializeDealerInventory(this);
    }

    public int GetModifiedBuyPrice(ItemInstance item)
    {
        // 1) Base from dealer per-type settings (your existing logic)
        ItemPriceModifier dealerMod = priceModifiers.FirstOrDefault(m => m.itemType == item.Type);
        float dealerBuyMult = dealerMod != null ? dealerMod.buyPriceMultiplier : 1f;
        float basePrice = item.Cost * dealerBuyMult;

        // 2) City layer
        City city = PlayerStats.Instance.CurrentCity;  // your project already holds this
        float cityCOL = city != null ? city.costOfLiving : 1f;

        CityPriceModifier cityMod = FindCityMod(city, item.Type);
        float cityBuyMult = cityMod != null ? cityMod.buyPriceMultiplier : 1f;

        // 3) Favorite drug demand multiplier
        float faveMult = 1f;
        if (city != null && city.FavoriteDrug != null && item.Type == ItemType.Drug)
        {
            if (string.Equals(item.Name, city.FavoriteDrug.Name, System.StringComparison.Ordinal))
            {
                faveMult = city.favoriteDrugDemandMultiplier;
            }
        }

        // 4) Daily volatility
        float vol = cityMod != null ? cityMod.dailyVolatility : 0.2f; // default 20% if not set
        float dv = PriceService.DailyVolatility(city?.Name ?? "Unknown", item.Name, vol);
        float dailyMult = 1f + dv;

        // 5) Market events (buy side tends to be affected similarly)
        var evt = PriceService.DailyEvent(city?.Name ?? "Unknown", item.Type,
                    cityMod != null ? cityMod.boomChance : 0.05f,
                    cityMod != null ? cityMod.bustChance : 0.05f);

        float eventMult = 1f;
        if (evt == PriceService.MarketEvent.Boom)
            eventMult = cityMod != null ? cityMod.boomMultiplier : 1.6f;
        else if (evt == PriceService.MarketEvent.Bust)
            eventMult = cityMod != null ? cityMod.bustMultiplier : 0.6f;

        // 6) Combine
        float final = basePrice * cityCOL * cityBuyMult * faveMult * dailyMult * eventMult * VisitMultiplier;

        // 7) City event modifier (drugs only)
        if (item.Type == ItemType.Drug)
        {
            var cityEvt = CityEventManager.GetEventForCity(city?.Name ?? "");
            if (cityEvt == CityEventManager.CityEvent.Lockdown)
                final *= CityEventManager.LockdownPriceMult;
            else if (cityEvt == CityEventManager.CityEvent.Shortage)
                final *= CityEventManager.ShortagePriceMult;
        }

        // 8) Clamp & round to int dollars
        final = Mathf.Clamp(final, 1f, 999_999f);
        return Mathf.RoundToInt(final);
    }

    public int GetModifiedSellPrice(ItemInstance item)
    {
        // Start from your computed buy price for this dealer+city+day
        int modifiedBuy = GetModifiedBuyPrice(item);

        // Dealer per-type sell ratio only (city factors already baked into modifiedBuy)
        ItemPriceModifier dealerMod = priceModifiers.FirstOrDefault(m => m.itemType == item.Type);
        float dealerSellRatio = dealerMod != null ? dealerMod.sellPriceRatio : 0.5f;

        int sellPrice = Mathf.RoundToInt(modifiedBuy * dealerSellRatio);

        // City drug bonus: premium for selling specific drugs in their home market
        City sellCity = PlayerStats.Instance?.CurrentCity;
        if (item.Type == ItemType.Drug && sellCity != null)
        {
            float drugBonus = sellCity.GetDrugSellBonus(item.Name);
            if (drugBonus > 1f)
                sellPrice = Mathf.RoundToInt(sellPrice * drugBonus);
        }

        // Festival: 2× sell on city's favorite drug
        if (item.Type == ItemType.Drug
            && CityEventManager.GetEventForCity(sellCity?.Name ?? "") == CityEventManager.CityEvent.Festival
            && sellCity?.FavoriteDrug != null
            && string.Equals(item.Name, sellCity.FavoriteDrug.Name, System.StringComparison.Ordinal))
        {
            sellPrice = Mathf.RoundToInt(sellPrice * CityEventManager.FestivalSellMult);
        }

        // Guard rails
        sellPrice = Mathf.Clamp(sellPrice, 1, 999_999);
        return sellPrice;
    }

    [ContextMenu("Clear Runtime Inventory")]
    public void ClearRuntimeInventory()
    {
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.ClearDealerInventory(this);
    }
}

