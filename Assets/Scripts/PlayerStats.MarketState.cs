using System.Collections.Generic;
using UnityEngine;

// Per-(city, drug) market saturation. Selling N units bumps saturation; saturation
// decays daily. Sell-price multiplier = max(floor, 1 - slope * saturation), so the
// 20th unit you sell in a city today fetches less than the 1st. Forces players to
// spread sales across cities and days instead of one-shotting the debt.
public partial class PlayerStats
{
    private readonly Dictionary<string, float> _marketSaturation = new Dictionary<string, float>();

    // Tuning constants (visible to other systems via the helpers below).
    public const float MarketSatFloor = 0.30f;   // Sell mult never drops below 30% of normal.
    public const float MarketSatSlope = 0.70f;   // Bigger = sharper price drop per unit saturation.
    public const float MarketSatDecay = 0.60f;   // Per-day retention (0.6 = 40% recovery daily).

    // Per-unit saturation bump scales with risk tier — premium drugs saturate faster
    // because the market for them is thinner.
    public static float SaturationPerUnit(int riskTier)
    {
        switch (riskTier)
        {
            case 2: return 0.030f; // Hard (Crack/Heroin): ~33 units → market floored
            case 1: return 0.015f; // Medium (LSD/Ecstasy): ~66 units → market floored
            default: return 0.005f; // Safe (Weed/Shrooms): ~200 units → market floored
        }
    }

    private static string MarketKey(string city, string drug) => $"{city}|{drug}";

    public float GetMarketSaturation(string city, string drug)
    {
        if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(drug)) return 0f;
        return _marketSaturation.TryGetValue(MarketKey(city, drug), out float v) ? v : 0f;
    }

    // Mult at the given saturation, floored.
    public static float SaturationToMult(float saturation) =>
        Mathf.Max(MarketSatFloor, 1f - MarketSatSlope * saturation);

    // Average multiplier for a sale of `amount` units at the current saturation.
    // Linear approximation: avg of the start mult and the end mult after the bump.
    public float GetSaturationAverageMult(string city, string drug, int amount, int riskTier)
    {
        if (amount <= 0) return 1f;
        float s0 = GetMarketSaturation(city, drug);
        float s1 = s0 + amount * SaturationPerUnit(riskTier);
        return 0.5f * (SaturationToMult(s0) + SaturationToMult(s1));
    }

    public void BumpMarketSaturation(string city, string drug, int amount, int riskTier)
    {
        if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(drug) || amount <= 0) return;
        string key = MarketKey(city, drug);
        _marketSaturation.TryGetValue(key, out float v);
        _marketSaturation[key] = v + amount * SaturationPerUnit(riskTier);
    }

    public void DecayMarketSaturation()
    {
        if (_marketSaturation.Count == 0) return;
        var keys = new List<string>(_marketSaturation.Keys);
        foreach (var k in keys)
        {
            float v = _marketSaturation[k] * MarketSatDecay;
            if (v < 0.01f) _marketSaturation.Remove(k);
            else _marketSaturation[k] = v;
        }
    }

    public void ResetMarketState()
    {
        _marketSaturation.Clear();
    }

    // Save/load — written into RunStatsSnapshot's parallel lists.
    internal void CaptureMarketSaturation(List<string> keys, List<float> values)
    {
        if (keys == null || values == null) return;
        keys.Clear();
        values.Clear();
        foreach (var kv in _marketSaturation)
        {
            keys.Add(kv.Key);
            values.Add(kv.Value);
        }
    }

    internal void RestoreMarketSaturation(List<string> keys, List<float> values)
    {
        _marketSaturation.Clear();
        if (keys == null || values == null) return;
        int n = Mathf.Min(keys.Count, values.Count);
        for (int i = 0; i < n; i++)
            _marketSaturation[keys[i]] = values[i];
    }
}
