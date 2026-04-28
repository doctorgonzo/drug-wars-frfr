using UnityEngine;

// Core singleton shell. See partial class files for organized members:
//   PlayerStats.Identity.cs   — name, sprite
//   PlayerStats.Equipment.cs  — trenchcoat, weapon
//   PlayerStats.Economy.cs    — wallet, inventory, slots, events
//   PlayerStats.Progression.cs — heat, level, city, cop encounters, contraband
public partial class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        inventory.Clear();
    }
}

