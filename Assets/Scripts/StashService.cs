using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Per-city stash. Holds drugs (or any ItemInstance) outside the player's trenchcoat.
// Stashed items:
//   - don't count toward trenchcoat slot capacity
//   - are invisible to cops (they only confiscate from PlayerStats.inventory)
//   - are only accessible from the city where they were deposited
//
// Auto-spawned, no Editor wiring required.
public class StashService : MonoBehaviour
{
    public static StashService Instance { get; private set; }

    private readonly Dictionary<string, List<ItemInstance>> _stashesByCity = new Dictionary<string, List<ItemInstance>>();

    public event Action OnStashChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("StashService");
        go.AddComponent<StashService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ----- Read API -----

    public List<ItemInstance> GetStash(string cityName)
    {
        if (string.IsNullOrEmpty(cityName)) return new List<ItemInstance>();
        return _stashesByCity.TryGetValue(cityName, out var list) ? list : new List<ItemInstance>();
    }

    public int GetTotalUnitsStashed(string cityName)
    {
        if (string.IsNullOrEmpty(cityName)) return 0;
        if (!_stashesByCity.TryGetValue(cityName, out var list)) return 0;
        int total = 0;
        foreach (var it in list)
            if (it != null && it.Type == ItemType.Drug) total += it.Amount;
        return total;
    }

    public IReadOnlyDictionary<string, List<ItemInstance>> AllStashes() => _stashesByCity;

    // ----- Mutation API -----

    private List<ItemInstance> GetOrCreateStash(string cityName)
    {
        if (!_stashesByCity.TryGetValue(cityName, out var list))
        {
            list = new List<ItemInstance>();
            _stashesByCity[cityName] = list;
        }
        return list;
    }

    // Move from player inventory to stash. Returns actual amount moved.
    public int Deposit(string cityName, ItemInstance playerItem, int amount)
    {
        if (string.IsNullOrEmpty(cityName)) return 0;
        if (playerItem == null || amount <= 0) return 0;
        if (PlayerStats.Instance == null) return 0;

        int actual = Mathf.Min(amount, playerItem.Amount);
        if (actual <= 0) return 0;

        var stash = GetOrCreateStash(cityName);
        var existing = stash.FirstOrDefault(i => i != null && i.MatchesStack(playerItem));
        if (existing != null)
        {
            // Weighted-average the avg purchase price across the merged stack
            int oldQty = existing.Amount;
            int newQty = oldQty + actual;
            float oldAvg = existing.AvgPurchasePrice;
            float depositAvg = playerItem.AvgPurchasePrice;
            existing.AvgPurchasePrice = (int) ((oldAvg * oldQty + depositAvg * actual) / Mathf.Max(1, newQty));
            existing.ChangeAmount(actual);
        }
        else
        {
            var copy = new ItemInstance(playerItem, amountOverride: actual);
            stash.Add(copy);
        }

        playerItem.ChangeAmount(-actual);
        PlayerStats.Instance.inventory.RemoveAll(i => i.Amount <= 0);
        PlayerStats.Instance.NotifyInventoryChanged();
        OnStashChanged?.Invoke();
        return actual;
    }

    // Move from stash to player inventory. Caps at trenchcoat capacity for drugs.
    // Returns actual amount moved.
    public int Withdraw(string cityName, ItemInstance stashItem, int amount)
    {
        if (string.IsNullOrEmpty(cityName) || stashItem == null || amount <= 0) return 0;
        var ps = PlayerStats.Instance;
        if (ps == null) return 0;

        int actual = Mathf.Min(amount, stashItem.Amount);
        if (actual <= 0) return 0;

        // Drugs are slot-bound — don't pull more than the trenchcoat fits. Slot math is
        // per-quality, so a Pure withdrawal only reserves slots against existing Pure stacks.
        if (stashItem.Type == ItemType.Drug)
        {
            int maxBuyable = ps.GetMaxBuyableAmount(stashItem.Name, stashItem.Quality, stashItem.UnitsPerSlot, stashItem.RiskTier);
            actual = Mathf.Min(actual, maxBuyable);
            if (actual <= 0) return 0;
        }

        var existing = ps.inventory.FirstOrDefault(i => i != null && i.MatchesStack(stashItem));
        if (existing != null)
        {
            int oldQty = existing.Amount;
            int newQty = oldQty + actual;
            float oldAvg = existing.AvgPurchasePrice;
            float withdrawAvg = stashItem.AvgPurchasePrice;
            existing.AvgPurchasePrice = (int)(oldAvg * oldQty + withdrawAvg * actual) / Mathf.Max(1, newQty);
            existing.ChangeAmount(actual);
        }
        else
        {
            var copy = new ItemInstance(stashItem, amountOverride: actual);
            ps.inventory.Add(copy);
        }

        stashItem.ChangeAmount(-actual);
        var stash = GetOrCreateStash(cityName);
        stash.RemoveAll(i => i.Amount <= 0);

        ps.NotifyInventoryChanged();
        OnStashChanged?.Invoke();
        return actual;
    }

    // ----- Reset & persistence -----

    public void ResetForNewRun()
    {
        _stashesByCity.Clear();
        OnStashChanged?.Invoke();
    }

    public StashesSnapshot CaptureSnapshot()
    {
        var snap = new StashesSnapshot();
        foreach (var kv in _stashesByCity)
        {
            if (kv.Value == null || kv.Value.Count == 0) continue;
            var entry = new StashCityEntry { cityName = kv.Key };
            foreach (var item in kv.Value)
            {
                if (item == null) continue;
                entry.items.Add(new SavedItemInstance
                {
                    name = item.Name,
                    description = item.Description,
                    cost = item.Cost,
                    amount = item.Amount,
                    itemType = (int)item.Type,
                    heatValue = item.HeatValue,
                    avgPurchasePrice = item.AvgPurchasePrice
                });
            }
            snap.entries.Add(entry);
        }
        return snap;
    }

    public void RestoreSnapshot(StashesSnapshot snap)
    {
        _stashesByCity.Clear();
        if (snap?.entries == null) { OnStashChanged?.Invoke(); return; }

        var gsm = GameSessionManager.Instance;
        foreach (var entry in snap.entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.cityName) || entry.items == null) continue;
            var list = new List<ItemInstance>();
            foreach (var saved in entry.items)
            {
                if (saved == null) continue;
                ItemInstance restored = null;
                if (gsm != null)
                    restored = gsm.ResolveItemByName(saved.name, (ItemType)saved.itemType);

                if (restored != null)
                {
                    restored.Amount = saved.amount;
                    restored.AvgPurchasePrice = saved.avgPurchasePrice;
                    restored.HeatValue = saved.heatValue;
                    list.Add(restored);
                }
                else
                {
                    // Couldn't resolve template — fall back to a bare ItemInstance from the saved data.
                    list.Add(new ItemInstance
                    {
                        Name = saved.name,
                        Description = saved.description,
                        Cost = saved.cost,
                        Amount = saved.amount,
                        Type = (ItemType)saved.itemType,
                        HeatValue = saved.heatValue,
                        AvgPurchasePrice = saved.avgPurchasePrice
                    });
                }
            }
            if (list.Count > 0) _stashesByCity[entry.cityName] = list;
        }
        OnStashChanged?.Invoke();
    }
}

[Serializable]
public class StashesSnapshot
{
    public List<StashCityEntry> entries = new List<StashCityEntry>();
}

[Serializable]
public class StashCityEntry
{
    public string cityName;
    public List<SavedItemInstance> items = new List<SavedItemInstance>();
}
