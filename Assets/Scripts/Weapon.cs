using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "Scriptable Objects/Weapon")]
public class Weapon : Item
{
    [Tooltip("Damage dealt per attack in combat.")]
    public int Damage;

    [Tooltip("Flat additive bonus to run-success chance during cop encounters (0.10 = +10%).")]
    [Range(0f, 0.5f)]
    public float RunSuccessBonus = 0f;

    [Tooltip("Reduces the minimum bribe fraction the cop will accept — bigger gun, more leverage. " +
             "Subtracted from cop.minBribeFraction. (0.10 = cop accepts 10% lower bribes).")]
    [Range(0f, 0.5f)]
    public float BribeLeverage = 0f;

    [Tooltip("Reduces fines and combat cash losses by this fraction (0.10 = 10% smaller penalty).")]
    [Range(0f, 0.5f)]
    public float PenaltyReduction = 0f;
}
