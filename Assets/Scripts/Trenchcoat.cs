using UnityEngine;

[CreateAssetMenu(fileName = "Trenchcoat", menuName = "Scriptable Objects/Trenchcoat")]
public class Trenchcoat : Item
{
    public int StorageSlots;
    public int ArmorValue;

    [Tooltip("Per-RiskTier multiplier on the drug's UnitsPerSlot. Index 0 = Safe, 1 = Medium, 2 = Hard. " +
             "Lower values = drug takes more slots to carry. Cheap trenchcoats penalize harder drugs; " +
             "premium trenchcoats slightly boost them so the gear progression matters.")]
    public float[] RiskTierCapacityMultipliers = new float[] { 1f, 1f, 1f };

    public float GetCapacityMultiplier(int riskTier)
    {
        if (RiskTierCapacityMultipliers == null || RiskTierCapacityMultipliers.Length == 0) return 1f;
        int idx = Mathf.Clamp(riskTier, 0, RiskTierCapacityMultipliers.Length - 1);
        return Mathf.Max(0.01f, RiskTierCapacityMultipliers[idx]);
    }
}
