using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Tooltip("Assign ALL of your City Scriptable Objects here.")]
    [SerializeField] private List<City> allCitiesInGame;

    [Tooltip("Every Trenchcoat SO in the game — same set you give CharCreationUI and EquipmentShop.")]
    [SerializeField] private Trenchcoat[] allTrenchcoats;

    [Tooltip("Every Weapon SO in the game — same set you give CharCreationUI and EquipmentShop.")]
    [SerializeField] private Weapon[] allWeapons;

    [Tooltip("Player character sprites in the same order as CharCreationUI.charSprites.")]
    [SerializeField] private Sprite[] playerSprites;

    public IReadOnlyList<Trenchcoat> AllTrenchcoats => allTrenchcoats;
    public IReadOnlyList<Weapon> AllWeapons => allWeapons;
    public IReadOnlyList<Sprite> PlayerSprites => playerSprites;
    public IReadOnlyList<City> AllCities => allCitiesInGame;
    public City FindCityByName(string name) => allCitiesInGame?.FirstOrDefault(c => c != null && c.Name == name);

    // Runtime dealer inventories keyed by Dealer SO instance ID.
    private readonly Dictionary<int, List<ItemInstance>> dealerInventories = new Dictionary<int, List<ItemInstance>>();
    // Day index of each dealer's last restock, keyed by Dealer SO instance ID.
    private readonly Dictionary<int, int> dealerLastRestockDay = new Dictionary<int, int>();

    private int _pendingLoadDay = 0;
    private bool _gameTimeSubscribed = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeAllDealers();
        SceneManager.sceneLoaded += EnsureGameTimeSubscriptionOnSceneLoad;
        TryEnsureGameTimeSubscription();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= EnsureGameTimeSubscriptionOnSceneLoad;
        if (_gameTimeSubscribed && GameTime.Instance != null)
            GameTime.Instance.DayChanged -= HandleDayChanged;
    }

    private void EnsureGameTimeSubscriptionOnSceneLoad(Scene scene, LoadSceneMode mode)
    {
        TryEnsureGameTimeSubscription();
    }

    private void TryEnsureGameTimeSubscription()
    {
        if (_gameTimeSubscribed) return;
        var gt = GameTime.Instance ?? FindObjectOfType<GameTime>();
        if (gt != null)
        {
            gt.DayChanged += HandleDayChanged;
            _gameTimeSubscribed = true;
        }
    }

    private void HandleDayChanged(GameTime.GameDateTime dt)
    {
        if (allCitiesInGame == null) return;
        foreach (City city in allCitiesInGame)
        {
            if (city == null || city.Dealers == null) continue;
            foreach (Dealer dealer in city.Dealers)
            {
                if (dealer == null) continue;
                int interval = dealer.restockIntervalDays;
                if (interval <= 0) continue;

                int key = dealer.GetInstanceID();
                if (!dealerLastRestockDay.TryGetValue(key, out int lastDay))
                    lastDay = dt.day;

                if (dt.day - lastDay >= interval)
                {
                    InitializeDealerInventory(dealer);
                    dealerLastRestockDay[key] = dt.day;
                }
            }
        }
    }

    private void InitializeAllDealers()
    {
        dealerInventories.Clear();
        dealerLastRestockDay.Clear();
        if (allCitiesInGame == null) return;

        foreach (City city in allCitiesInGame)
        {
            if (city.Dealers == null) continue;
            foreach (Dealer dealer in city.Dealers)
            {
                if (dealer != null)
                    InitializeDealerInventory(dealer);
            }
        }
    }

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

        // Resolve player sprite index
        int spriteIdx = 0;
        if (ps.PlayerSprite != null && playerSprites != null)
        {
            for (int i = 0; i < playerSprites.Length; i++)
            {
                if (playerSprites[i] == ps.PlayerSprite) { spriteIdx = i; break; }
            }
        }

        var data = new SaveData
        {
            playerName          = ps.PlayerName,
            playerSpriteIndex   = spriteIdx,
            playerWallet        = ps.PlayerWallet,
            currentHeat         = ps.CurrentHeat,
            timesCaughtByCops   = ps.TimesCaughtByCops,
            level               = ps.Level,
            currentCityName     = ps.CurrentCity  != null ? ps.CurrentCity.Name  : "",
            trenchcoatName      = ps.CurrentTrench != null ? ps.CurrentTrench.Name : "",
            weaponName          = ps.CurrentWeapon != null ? ps.CurrentWeapon.Name : "",
            inGameDay           = PriceService.InGameDay,
            runSeed             = PriceService.RunSeed,
            debt                = ps.Debt,
            runStats            = ps.CaptureRunStatsSnapshot()
        };

        foreach (var item in ps.inventory)
            data.inventory.Add(ItemToSaved(item));

        foreach (City city in allCitiesInGame)
        {
            if (city == null || city.Dealers == null) continue;
            foreach (Dealer dealer in city.Dealers)
            {
                if (dealer == null) continue;
                int dealerKey = dealer.GetInstanceID();
                var state = new SavedDealerState
                {
                    dealerName = dealer.Name,
                    lastRestockDay = dealerLastRestockDay.TryGetValue(dealerKey, out int last) ? last : 0
                };
                foreach (var item in GetRuntimeInventory(dealer))
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
        if (ps == null)
        {
            var go = new GameObject("PlayerStats");
            go.AddComponent<PlayerStats>();
            ps = PlayerStats.Instance;
        }

        ps.PlayerName         = data.playerName;
        ps.PlayerWallet       = data.playerWallet;
        ps.CurrentHeat        = data.currentHeat;
        ps.TimesCaughtByCops  = data.timesCaughtByCops;
        ps.Level              = data.level;
        ps.Debt               = data.debt;
        ps.RestoreRunStatsSnapshot(data.runStats);

        // Player sprite
        if (playerSprites != null && data.playerSpriteIndex >= 0 && data.playerSpriteIndex < playerSprites.Length)
            ps.PlayerSprite = playerSprites[data.playerSpriteIndex];

        // Trenchcoat — search the master list, not just dealer inventories
        if (allTrenchcoats != null)
            ps.CurrentTrench = allTrenchcoats.FirstOrDefault(t => t != null && t.Name == data.trenchcoatName);

        // Weapon — same
        if (allWeapons != null)
            ps.CurrentWeapon = allWeapons.FirstOrDefault(w => w != null && w.Name == data.weaponName);

        // City
        ps.CurrentCity = allCitiesInGame.FirstOrDefault(c => c.Name == data.currentCityName);

        // Day — restored after scene load so GameTime.Awake() doesn't overwrite it
        _pendingLoadDay = data.inGameDay;
        PriceService.InGameDay = data.inGameDay;
        PriceService.RunSeed = data.runSeed;
        SceneManager.sceneLoaded += RestoreDayAfterSceneLoad;

        // Initialize all dealers first so we have a complete item template map for lookups
        InitializeAllDealers();
        // Save the templates before we clear dealerInventories
        var templateInventories = new Dictionary<int, List<ItemInstance>>(dealerInventories);

        // Player inventory
        ps.inventory.Clear();
        foreach (var saved in data.inventory)
            ps.inventory.Add(SavedToItem(saved, templateInventories));
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
                        list.Add(SavedToItem(si, templateInventories));
                    dealerInventories[key] = list;
                    dealerLastRestockDay[key] = savedState.lastRestockDay;
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

    // Fires once after the city scene loads — restores day overwritten by GameTime.Awake().
    private void RestoreDayAfterSceneLoad(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= RestoreDayAfterSceneLoad;
        PriceService.InGameDay = _pendingLoadDay;
        var gt = FindObjectOfType<GameTime>();
        if (gt != null)
            gt.SetTime(new GameTime.GameDateTime(_pendingLoadDay, 0, 0, 0), invokeEvents: false);
        _pendingLoadDay = 0;
    }

    // ---- Serialization helpers ----

    private static SavedItemInstance ItemToSaved(ItemInstance item)
    {
        return new SavedItemInstance
        {
            name        = item.Name,
            description = item.Description,
            cost        = item.Cost,
            amount      = item.Amount,
            itemType    = (int)item.Type,
            heatValue   = item.HeatValue,
            avgPurchasePrice = item.AvgPurchasePrice
        };
    }

    private ItemInstance SavedToItem(SavedItemInstance saved, Dictionary<int, List<ItemInstance>> templateInventories)
    {
        ItemType itemType = (ItemType)saved.itemType;
        Sprite restoredImage = null;
        float buyHeatMultiplier = 1f;
        int riskTier = 0;
        int unitsPerSlot = 30;

        // Search through the template inventories for a matching template
        ItemInstance template = FindItemTemplate(saved.name, itemType, templateInventories);

        if (template != null)
        {
            restoredImage = template.Image;
            buyHeatMultiplier = template.BuyHeatMultiplier;
            riskTier = template.RiskTier;
            unitsPerSlot = template.UnitsPerSlot;
        }

        return new ItemInstance
        {
            Name        = saved.name,
            Description = saved.description,
            Cost        = saved.cost,
            Amount      = saved.amount,
            Type        = itemType,
            HeatValue   = saved.heatValue,
            Image       = restoredImage,
            BuyHeatMultiplier = buyHeatMultiplier,
            RiskTier    = riskTier,
            UnitsPerSlot = unitsPerSlot,
            AvgPurchasePrice = saved.avgPurchasePrice
        };
    }

    // Search through the template inventories to find a template item by name
    private ItemInstance FindItemTemplate(string itemName, ItemType itemType, Dictionary<int, List<ItemInstance>> templateInventories)
    {
        // Check weapons first
        if (itemType == ItemType.Weapon && allWeapons != null)
        {
            foreach (var weapon in allWeapons)
            {
                if (weapon != null && weapon.Name == itemName)
                    return new ItemInstance(weapon);
            }
        }

        // Check trenchcoats
        if (itemType == ItemType.Trenchcoat && allTrenchcoats != null)
        {
            foreach (var trench in allTrenchcoats)
            {
                if (trench != null && trench.Name == itemName)
                    return new ItemInstance(trench);
            }
        }

        // Check template inventories for drugs
        foreach (var inv in templateInventories.Values)
        {
            foreach (var item in inv)
            {
                if (item != null && item.Name == itemName && item.Type == itemType)
                    return item;
            }
        }

        return null;
    }
}
