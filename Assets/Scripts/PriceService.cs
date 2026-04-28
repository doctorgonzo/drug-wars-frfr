using System;

public static class PriceService
{
    public static int InGameDay = 1;

    // 🔹 Plug-in provider for "what day is it?" Defaults to real-world if not set.
    public static Func<int> DayProvider = () =>
    {
        // fallback (only used if you don't wire GameTime)
        return int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
    };

    private static float StableRandom01(string key)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < key.Length; i++)
                hash = hash * 31 + key[i];

            var rng = new System.Random(hash);
            return (float)rng.NextDouble();
        }
    }

    // Returns a deterministic daily number in [-v, +v] using *in-game day*
    public static float DailyVolatility(string cityName, string itemName, float volatility)
    {
        if (volatility <= 0f) return 0f;
        string dayKey = $"day:{InGameDay}";           // 🔁 uses in-game day now
        float u = StableRandom01($"{dayKey}|{cityName}|{itemName}|vol");
        return (u * 2f - 1f) * volatility;
    }

    public enum MarketEvent { None, Boom, Bust }

    // Deterministic daily event per (city, itemType) using *in-game day*
    public static MarketEvent DailyEvent(string cityName, ItemType type, float boomChance, float bustChance)
    {
        string dayKey = $"day:{InGameDay}";           // 🔁 uses in-game day now
        float u = StableRandom01($"{dayKey}|{cityName}|{type}|evt");

        if (u < boomChance) return MarketEvent.Boom;
        if (u < boomChance + bustChance) return MarketEvent.Bust;
        return MarketEvent.None;
    }
}
