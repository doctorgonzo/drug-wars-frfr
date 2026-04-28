using UnityEngine;

public enum DrugRiskTier { Safe = 0, Medium = 1, Hard = 2 }

[CreateAssetMenu(fileName = "Drug", menuName = "Scriptable Objects/Drug")]
public class Drug : Item
{
    [Tooltip("Heat generated per unit when selling this drug.")]
    [Range(1, 20)]
    public int HeatValue = 5;

    [Tooltip("Buying is quieter than selling — heat on buy = HeatValue × this fraction.")]
    [Range(0.1f, 1.0f)]
    public float BuyHeatMultiplier = 0.4f;

    [Tooltip("Determines cop encounter difficulty when this drug is found. Affects bribe size, run chance, and arrest severity.")]
    public DrugRiskTier RiskTier = DrugRiskTier.Safe;
}

