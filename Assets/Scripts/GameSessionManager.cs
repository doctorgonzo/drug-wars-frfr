using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Tooltip("Assign ALL of your City Scriptable Objects here. This is used for the one-time dealer inventory reset.")]
    [SerializeField] private List<City> allCitiesInGame;

    // Runtime dealer inventories keyed by Dealer SO instance ID.
    // This keeps mutable state OFF the ScriptableObjects so editor play-sessions stay clean.
    private readonly Dictionary<int, List<ItemInstance>> dealerInventories = new Dictionary<int, List<ItemInstance>>();

    private void Awake()
    {
        // Standard Singleton Pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- CORE LOGIC ---
        // This runs ONCE at the start of the game. It loops through every
        // city and every dealer, resetting their inventory from their templates.
        InitializeAllDealers();
    }

    private void InitializeAllDealers()
    {
        dealerInventories.Clear();
        if (allCitiesInGame == null) return;

        foreach (City city in allCitiesInGame)
        {
            if (city.Dealers == null) continue;

            foreach (Dealer dealer in city.Dealers)
            {
                if (dealer != null)
                {
                    InitializeDealerInventory(dealer);
                }
            }
        }
    }

    // Build (or rebuild) a runtime inventory for a single dealer from its SO template.
    public void InitializeDealerInventory(Dealer dealer)
    {
        int key = dealer.GetInstanceID();
        var list = new List<ItemInstance>();
        if (dealer.Inventory != null)
        {
            foreach (var itemAsset in dealer.Inventory)
            {
                if (itemAsset != null)
                    list.Add(new ItemInstance(itemAsset));
            }
        }
        dealerInventories[key] = list;
    }

    // Retrieve the live runtime inventory for a dealer. Initializes on first access if needed.
    public List<ItemInstance> GetRuntimeInventory(Dealer dealer)
    {
        int key = dealer.GetInstanceID();
        if (!dealerInventories.TryGetValue(key, out var inv))
        {
            InitializeDealerInventory(dealer);
            inv = dealerInventories[key];
        }
        return inv;
    }

    // Clear a single dealer's runtime inventory.
    public void ClearDealerInventory(Dealer dealer)
    {
        int key = dealer.GetInstanceID();
        if (dealerInventories.TryGetValue(key, out var inv))
            inv.Clear();
    }

    // ----------------------------------------------------------------
    //  SAVE / LOAD
    // ----------------------------------------------------------------

    public void SaveGame()
    {
        var ps = PlayerStats.Instance;
        var data = new SaveData
        {
            playerName = ps.PlayerName,
            playerWallet = ps.PlayerWallet,
            currentHeat = ps.CurrentHeat,
            timesCaughtByCops = ps.TimesCaughtByCops,
            level = ps.Level,
            currentCityName = ps.CurrentCity != null ? ps.CurrentCity.Name : "",
            trenchcoatName = ps.CurrentTrench != null ? ps.CurrentTrench.Name : "",
            weaponName = ps.CurrentWeapon != null ? ps.CurrentWeapon.Name : "",
            inGameDay = PriceService.InGameDay
        };

        // Player inventory
        foreach (var item in ps.inventory)
        {
            data.inventory.Add(ItemToSaved(item));
        }

        // Dealer inventories
        foreach (City city in allCitiesInGame)
        {
            if (city == null || city.Dealers == null) continue;
            foreach (Dealer dealer in city.Dealers)
            {
                if (dealer == null) continue;
                var state = new SavedDealerState { dealerName = dealer.Name };
                var inv = GetRuntimeInventory(dealer);
                foreach (var item in inv)
                    state.inventory.Add(ItemToSaved(item));
                data.dealerStates.Add(state);
            }
        }

        SaveLoadHelper.WriteToDisk(data);
    }

    public bool LoadGame()
    {
        SaveData data = SaveLoadHelper.ReadFromDisk();
        if (data == null) return false;

        var ps = PlayerStats.Instance;
        ps.PlayerName = data.playerName;
        ps.PlayerWallet = data.playerWallet;
        ps.CurrentHeat = data.currentHeat;
        ps.TimesCaughtByCops = data.timesCaughtByCops;
        ps.Level = data.level;
        PriceService.InGameDay = data.inGameDay;

        // Resolve city
        ps.CurrentCity = allCitiesInGame.FirstOrDefault(c => c.Name == data.currentCityName);

        // Resolve equipment
        foreach (City city in allCitiesInGame)
        {
            if (city.Dealers == null) continue;
            foreach (Dealer dealer in city.Dealers)
            {
                if (dealer == null || dealer.Inventory == null) continue;
                foreach (Item item in dealer.Inventory)
                {
                    if (item is Trenchcoat t && t.Name == data.trenchcoatName)
                        ps.CurrentTrench = t;
                    if (item is Weapon w && w.Name == data.weaponName)
                        ps.CurrentWeapon = w;
                }
            }
        }

        // Player inventory
        ps.inventory.Clear();
        foreach (var saved in data.inventory)
            ps.inventory.Add(SavedToItem(saved));
        ps.NotifyInventoryChanged();

        // Dealer inventories
        dealerInventories.Clear();
        foreach (City city in allCitiesInGame)
        {
            if (city == null || city.Dealers == null) continue;
            foreach (Dealer dealer in city.Dealers)
            {
                if (dealer == null) continue;
                var savedState = data.dealerStates.FirstOrDefault(s => s.dealerName == dealer.Name);
                int key = dealer.GetInstanceID();
                if (savedState != null)
                {
                    var list = new List<ItemInstance>();
                    foreach (var si in savedState.inventory)
                        list.Add(SavedToItem(si));
                    dealerInventories[key] = list;
                }
                else
                {
                    InitializeDealerInventory(dealer);
                }
            }
        }

        Debug.Log("[GameSessionManager] Game loaded successfully.");
        return true;
    }

    // ---- Serialization helpers ----

    private static SavedItemInstance ItemToSaved(ItemInstance item)
    {
        return new SavedItemInstance
        {
            name = item.Name,
            description = item.Description,
            cost = item.Cost,
            amount = item.Amount,
            itemType = (int)item.Type,
            heatValue = item.HeatValue
        };
    }

    private static ItemInstance SavedToItem(SavedItemInstance saved)
    {
        return new ItemInstance
        {
            Name = saved.name,
            Description = saved.description,
            Cost = saved.cost,
            Amount = saved.amount,
            Type = (ItemType)saved.itemType,
            HeatValue = saved.heatValue
        };
    }
}