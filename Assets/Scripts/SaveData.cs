using System;
using System.Collections.Generic;

// Serializable snapshot of all player and world state needed to resume a game.
[Serializable]
public class SaveData
{
    // Player identity
    public string playerName;
    public int playerSpriteIndex; // index into the sprite array — sprites can't be serialized

    // Economy
    public int playerWallet;
    public List<SavedItemInstance> inventory = new List<SavedItemInstance>();

    // Equipment (stored by name so we can look them up from Resources/SOs at load time)
    public string trenchcoatName;
    public string weaponName;

    // Progression
    public float currentHeat;
    public int timesCaughtByCops;
    public int level;
    public string currentCityName;
    public int debt;

    // World state
    public int inGameDay;
    public int runSeed;
    public List<SavedDealerState> dealerStates = new List<SavedDealerState>();

    // Per-run statistics snapshot (endgame summary + leaderboard).
    public RunStatsSnapshot runStats;
}

[Serializable]
public class SavedItemInstance
{
    public string name;
    public string description;
    public int cost;
    public int amount;
    public int itemType; // cast of ItemType enum
    public int heatValue;
    public int avgPurchasePrice;
    // Quality stored as (DrugQuality + 1) so 0 means "field absent in this save" — JsonUtility
    // defaults missing ints to 0, and we want old saves to load as Standard (the previous
    // single-quality assumption), not Cut. Read code maps 0 → Standard.
    public int qualityPlus1;
}

[Serializable]
public class SavedDealerState
{
    public string dealerName;
    public int lastRestockDay;
    public long lifetimeBusiness; // total $ business with this dealer (drives rep tier)
    public List<SavedItemInstance> inventory = new List<SavedItemInstance>();
}
