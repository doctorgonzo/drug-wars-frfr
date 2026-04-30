using System.Collections.Generic;
using UnityEngine;

// One-shot daily market tip. Each in-game day deterministically rolls a single tip pointing at
// a city + drug pair: either "BUY here, it's cheap today" (DealBuy) or "SELL here, premium prices"
// (HotSell). Players have to commit to traveling to cash in — the time + fare cost is the trade.
//
// Determinism: hashed on RunSeed + InGameDay so it's stable across save/reload but changes each
// run. Uses GameSessionManager.AllCities + dealer inventories to pick a target.
public enum DailyTipType { None, DealBuy, HotSell }

public class DailyTip
{
    public DailyTipType Type;
    public string CityName;
    public string DrugName;
    public float Multiplier; // applied at the matching price chain step

    public bool Matches(string cityName, string drugName)
    {
        return Type != DailyTipType.None
            && !string.IsNullOrEmpty(cityName)
            && cityName == CityName
            && drugName == DrugName;
    }

    public string ToHeadline()
    {
        switch (Type)
        {
            case DailyTipType.DealBuy:
                int discountPct = Mathf.RoundToInt((1f - Multiplier) * 100f);
                return $"<color=#66FF66>TIP</color> — Word is {DrugName} is moving cheap in {CityName} today. Save ~{discountPct}% if you go.";
            case DailyTipType.HotSell:
                int premiumPct = Mathf.RoundToInt((Multiplier - 1f) * 100f);
                return $"<color=#FFCC44>TIP</color> — Buyers in {CityName} are paying ~{premiumPct}% over market for {DrugName} today.";
            default:
                return "";
        }
    }
}

public static class DailyTipService
{
    private static int _cachedDay = -1;
    private static int _cachedSeed;
    private static DailyTip _cached;

    // Hashed deterministic [0,1).
    private static float Roll(string key)
    {
        unchecked
        {
            int hash = 19;
            for (int i = 0; i < key.Length; i++)
                hash = hash * 31 + key[i];
            var rng = new System.Random(hash);
            return (float)rng.NextDouble();
        }
    }

    public static DailyTip GetTodaysTip()
    {
        if (_cached != null && _cachedDay == PriceService.InGameDay && _cachedSeed == PriceService.RunSeed)
            return _cached;

        var gsm = GameSessionManager.Instance;
        if (gsm == null || gsm.AllCities == null || gsm.AllCities.Count == 0)
        {
            _cached = new DailyTip { Type = DailyTipType.None };
            _cachedDay = PriceService.InGameDay;
            _cachedSeed = PriceService.RunSeed;
            return _cached;
        }

        string baseKey = $"tip|run:{PriceService.RunSeed}|day:{PriceService.InGameDay}";
        float existChance = Roll(baseKey + "|exists");
        // 65% chance a tip exists on a given day
        if (existChance > 0.65f)
        {
            _cached = new DailyTip { Type = DailyTipType.None };
            _cachedDay = PriceService.InGameDay;
            _cachedSeed = PriceService.RunSeed;
            return _cached;
        }

        // Pick a city
        var validCities = new List<City>();
        foreach (var c in gsm.AllCities)
            if (c != null && !string.IsNullOrEmpty(c.Name)) validCities.Add(c);
        if (validCities.Count == 0)
        {
            _cached = new DailyTip { Type = DailyTipType.None };
            _cachedDay = PriceService.InGameDay;
            _cachedSeed = PriceService.RunSeed;
            return _cached;
        }
        int cityIdx = Mathf.FloorToInt(Roll(baseKey + "|city") * validCities.Count);
        cityIdx = Mathf.Clamp(cityIdx, 0, validCities.Count - 1);
        var city = validCities[cityIdx];

        // Pick a drug seen in that city's dealer inventories (so the tip is relevant)
        var drugsInCity = new List<Drug>();
        var seen = new HashSet<string>();
        if (city.Dealers != null)
        {
            foreach (var dealer in city.Dealers)
            {
                if (dealer == null || dealer.Inventory == null) continue;
                foreach (var item in dealer.Inventory)
                {
                    if (item is Drug d && !seen.Contains(d.Name))
                    {
                        seen.Add(d.Name);
                        drugsInCity.Add(d);
                    }
                }
            }
        }
        if (drugsInCity.Count == 0)
        {
            _cached = new DailyTip { Type = DailyTipType.None };
            _cachedDay = PriceService.InGameDay;
            _cachedSeed = PriceService.RunSeed;
            return _cached;
        }
        int drugIdx = Mathf.FloorToInt(Roll(baseKey + "|drug") * drugsInCity.Count);
        drugIdx = Mathf.Clamp(drugIdx, 0, drugsInCity.Count - 1);
        var drug = drugsInCity[drugIdx];

        // Pick tip type & magnitude
        bool isDealBuy = Roll(baseKey + "|type") < 0.5f;
        float magnitudeRoll = Roll(baseKey + "|mag");
        DailyTipType type;
        float multiplier;
        if (isDealBuy)
        {
            type = DailyTipType.DealBuy;
            // 0.65–0.80 (i.e. 20%–35% discount)
            multiplier = Mathf.Lerp(0.65f, 0.80f, magnitudeRoll);
        }
        else
        {
            type = DailyTipType.HotSell;
            // 1.25–1.45 (i.e. 25%–45% premium)
            multiplier = Mathf.Lerp(1.25f, 1.45f, magnitudeRoll);
        }

        _cached = new DailyTip
        {
            Type = type,
            CityName = city.Name,
            DrugName = drug.Name,
            Multiplier = multiplier
        };
        _cachedDay = PriceService.InGameDay;
        _cachedSeed = PriceService.RunSeed;
        return _cached;
    }

    public static void InvalidateCache()
    {
        _cached = null;
        _cachedDay = -1;
    }
}
