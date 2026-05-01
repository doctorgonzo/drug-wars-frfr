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

    [Tooltip("How often (in days) this dealer restocks their inventory. Set 0 to disable.")]
    public int restockIntervalDays = 2;

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

        // 3) Daily volatility
        float vol = cityMod != null ? cityMod.dailyVolatility : 0.2f; // default 20% if not set
        float dv = PriceService.DailyVolatility(city?.Name ?? "Unknown", item.Name, vol);
        float dailyMult = 1f + dv;

        // 4) Market events (buy side tends to be affected similarly)
        var evt = PriceService.DailyEvent(city?.Name ?? "Unknown", item.Type,
                    cityMod != null ? cityMod.boomChance : 0.05f,
                    cityMod != null ? cityMod.bustChance : 0.05f);

        float eventMult = 1f;
        if (evt == PriceService.MarketEvent.Boom)
            eventMult = cityMod != null ? cityMod.boomMultiplier : 1.6f;
        else if (evt == PriceService.MarketEvent.Bust)
            eventMult = cityMod != null ? cityMod.bustMultiplier : 0.6f;

        // 5) Combine — note: favoriteDrugDemandMultiplier is NOT applied here. It's a "consumer
        //    demand" boost for sellers only, applied in GetModifiedSellPrice.
        float final = basePrice * cityCOL * cityBuyMult * dailyMult * eventMult * VisitMultiplier;

        // Daily tip: if today's tip is a "DealBuy" pointing at this city + drug, knock the price down.
        if (item.Type == ItemType.Drug)
        {
            var tip = DailyTipService.GetTodaysTip();
            if (tip.Type == DailyTipType.DealBuy && tip.Matches(city?.Name ?? "", item.Name))
                final *= tip.Multiplier;
        }

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

    // Hard ceiling on sell-side multiplier stacking. Even with boom + festival + favorite +
    // hot tip + max volatility all aligned, the player can't sell crack at 10× base. Caps
    // the post-multiplier sell price at this fraction of the drug's BASE cost, so buy-side
    // inflation is also bounded.
    private const float MaxSellPriceBaseMult = 3.0f;

    // Pre-saturation per-unit sell price. All city/dealer/event multipliers, then the cap.
    private float ComputeBaseSellPriceF(ItemInstance item)
    {
        // Start from your computed buy price for this dealer+city+day
        int modifiedBuy = GetModifiedBuyPrice(item);

        // Dealer per-type sell ratio only (city factors already baked into modifiedBuy)
        ItemPriceModifier dealerMod = priceModifiers.FirstOrDefault(m => m.itemType == item.Type);
        float dealerSellRatio = dealerMod != null ? dealerMod.sellPriceRatio : 0.5f;

        float sellPriceF = modifiedBuy * dealerSellRatio;

        City sellCity = PlayerStats.Instance?.CurrentCity;

        // Favorite drug demand: a city's favorite drug commands a premium when sold here.
        if (item.Type == ItemType.Drug
            && sellCity != null
            && sellCity.FavoriteDrug != null
            && string.Equals(item.Name, sellCity.FavoriteDrug.Name, System.StringComparison.Ordinal))
        {
            sellPriceF *= sellCity.favoriteDrugDemandMultiplier;
        }

        // City drug bonus: premium for selling specific drugs (per-drug list, separate from FavoriteDrug)
        if (item.Type == ItemType.Drug && sellCity != null)
        {
            float drugBonus = sellCity.GetDrugSellBonus(item.Name);
            if (drugBonus > 1f)
                sellPriceF *= drugBonus;
        }

        // Festival: extra multiplier on top when the city is celebrating its favorite drug
        if (item.Type == ItemType.Drug
            && CityEventManager.GetEventForCity(sellCity?.Name ?? "") == CityEventManager.CityEvent.Festival
            && sellCity?.FavoriteDrug != null
            && string.Equals(item.Name, sellCity.FavoriteDrug.Name, System.StringComparison.Ordinal))
        {
            sellPriceF *= CityEventManager.FestivalSellMult;
        }

        // Daily tip: HotSell pumps up the sell price for today only at the target city + drug.
        if (item.Type == ItemType.Drug && sellCity != null)
        {
            var tip = DailyTipService.GetTodaysTip();
            if (tip.Type == DailyTipType.HotSell && tip.Matches(sellCity.Name, item.Name))
                sellPriceF *= tip.Multiplier;
        }

        // Hard ceiling: drug sell price can't exceed 3× base cost. Prevents perfect-storm
        // multiplier stacks (favorite × festival × tip × boom × volatility) from breaking
        // the economy. Applied to drugs only — equipment isn't subject to event multipliers.
        if (item.Type == ItemType.Drug)
        {
            float ceiling = item.Cost * MaxSellPriceBaseMult;
            if (sellPriceF > ceiling) sellPriceF = ceiling;
        }

        return sellPriceF;
    }

    // Per-unit sell price at CURRENT market saturation. Used for UI display and per-unit
    // profit calc. Multi-unit sales should use GetSellRevenueForBatch — saturation grows
    // during a sale, so the average per-unit price for a batch differs from the start price.
    public int GetModifiedSellPrice(ItemInstance item)
    {
        float perUnit = ComputeBaseSellPriceF(item);

        if (item.Type == ItemType.Drug && PlayerStats.Instance != null)
        {
            string cityName = PlayerStats.Instance.CurrentCity?.Name ?? "";
            float sat = PlayerStats.Instance.GetMarketSaturation(cityName, item.Name);
            perUnit *= PlayerStats.SaturationToMult(sat);
        }

        return Mathf.Clamp(Mathf.RoundToInt(perUnit), 1, 999_999);
    }

    // Total revenue for selling `amount` units in one transaction. Saturation grows during
    // the sale; the per-unit price slides linearly from SaturationToMult(s0) toward
    // SaturationToMult(s1). Caller is responsible for calling PlayerStats.BumpMarketSaturation
    // afterwards to commit the saturation increase.
    public int GetSellRevenueForBatch(ItemInstance item, int amount)
    {
        if (amount <= 0) return 0;
        float perUnit = ComputeBaseSellPriceF(item);

        float avgMult = 1f;
        if (item.Type == ItemType.Drug && PlayerStats.Instance != null)
        {
            string cityName = PlayerStats.Instance.CurrentCity?.Name ?? "";
            avgMult = PlayerStats.Instance.GetSaturationAverageMult(cityName, item.Name, amount, item.RiskTier);
        }

        long total = (long)Mathf.RoundToInt(perUnit * avgMult * amount);
        return (int)Mathf.Clamp(total, 1, 999_999_999);
    }

    [ContextMenu("Clear Runtime Inventory")]
    public void ClearRuntimeInventory()
    {
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.ClearDealerInventory(this);
    }
}

