using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Partial class: per-city heat memory. Selling drugs in a city accumulates "exposure" there
// that lingers across visits. Returning to a hot city while still hot causes the player to
// inherit a chunk of base heat on arrival — meaning camping one city becomes risky.
//
// Decay happens every in-game day so the player can always cool a city off by staying away.
public partial class PlayerStats
{
    private readonly Dictionary<string, float> _cityHeat = new Dictionary<string, float>();

    [Header("City Heat Memory")]
    [SerializeField] private float cityHeatGainPerHeatUnit = 0.6f;
    [SerializeField] private float cityHeatDecayPerDay = 8f;
    [SerializeField] private float cityHeatMax = 100f;
    [Tooltip("Player's heat is bumped by (cityHeat * arrivalHeatFactor) when entering a city.")]
    [SerializeField] private float cityHeatArrivalFactor = 0.35f;

    public float CityHeatMax => cityHeatMax;

    public void BumpCityHeat(string cityName, float heatUnits)
    {
        if (string.IsNullOrEmpty(cityName) || heatUnits <= 0f) return;
        _cityHeat.TryGetValue(cityName, out float current);
        current = Mathf.Clamp(current + heatUnits * cityHeatGainPerHeatUnit, 0f, cityHeatMax);
        _cityHeat[cityName] = current;
    }

    public float GetCityHeat(string cityName)
    {
        if (string.IsNullOrEmpty(cityName)) return 0f;
        _cityHeat.TryGetValue(cityName, out float v);
        return v;
    }

    public void DecayAllCityHeat()
    {
        if (_cityHeat.Count == 0) return;
        var keys = new List<string>(_cityHeat.Keys);
        foreach (var k in keys)
        {
            float v = _cityHeat[k] - cityHeatDecayPerDay;
            if (v <= 0f) _cityHeat.Remove(k);
            else _cityHeat[k] = v;
        }
    }

    // Returns the amount of player heat added by entering a hot city, or 0 if cold.
    public int ApplyCityHeatOnArrival(string cityName)
    {
        float v = GetCityHeat(cityName);
        if (v <= 0f) return 0;
        int playerHeatBump = Mathf.RoundToInt(v * cityHeatArrivalFactor);
        if (playerHeatBump <= 0) return 0;
        CurrentHeat = Mathf.Clamp(CurrentHeat + playerHeatBump, 0f, 100f);
        return playerHeatBump;
    }

    public void ResetCityHeat()
    {
        _cityHeat.Clear();
    }

    public string DescribeCityHeat(string cityName)
    {
        float v = GetCityHeat(cityName);
        if (v < 15f) return "QUIET";
        if (v < 35f) return "WARM";
        if (v < 60f) return "HOT";
        return "BURNING";
    }

    // Save/load support — flat parallel lists for JsonUtility compatibility.
    public void CaptureCityHeat(List<string> outNames, List<float> outValues)
    {
        outNames.Clear();
        outValues.Clear();
        foreach (var kv in _cityHeat)
        {
            outNames.Add(kv.Key);
            outValues.Add(kv.Value);
        }
    }

    public void RestoreCityHeat(List<string> names, List<float> values)
    {
        _cityHeat.Clear();
        if (names == null || values == null) return;
        int n = Mathf.Min(names.Count, values.Count);
        for (int i = 0; i < n; i++)
            _cityHeat[names[i]] = values[i];
    }
}
