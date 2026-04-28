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

    // World state
    public int inGameDay;
    public List<SavedDealerState> dealerStates = new List<SavedDealerState>();
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
}

[Serializable]
public class SavedDealerState
{
    public string dealerName;
    public List<SavedItemInstance> inventory = new List<SavedItemInstance>();
}
