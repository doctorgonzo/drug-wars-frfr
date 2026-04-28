using UnityEngine;

// Partial class: equipment-related state (trenchcoat, weapon)
public partial class PlayerStats
{
    [SerializeField] private Trenchcoat currentTrench;
    [SerializeField] private Weapon currentWeapon;

    public Trenchcoat CurrentTrench { get => currentTrench; set => currentTrench = value; }
    public Weapon CurrentWeapon { get => currentWeapon; set => currentWeapon = value; }
}
