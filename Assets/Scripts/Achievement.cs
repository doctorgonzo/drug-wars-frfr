using UnityEngine;

public enum AchievementStat
{
    PlayerWallet,
    NetWorth,
    CurrentHeat,
    DebtAmount,

    TotalSalesRevenue,
    TotalDrugSpend,
    TotalEquipmentSpend,
    TotalBribesPaid,
    TotalBorrowed,
    TotalDebtPaid,
    TotalConfiscatedCash,
    TotalFinesPaid,
    TotalCombatCashLoss,
    TotalTravelSpend,
    TotalInterestPaid,

    TotalDrugsBought,
    TotalDrugsSold,
    BiggestSingleSale,

    CombatWins,
    CombatLosses,
    TimesEscaped,
    TimesBribedSuccessfully,
    TotalCopEncounters,
    TimesCaughtByCops,

    PeakHeat,

    UniqueCitiesVisited,
    DayDebtCleared,

    OwnsTrenchcoat,
    OwnsWeapon,
}

public enum ComparisonType
{
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal
}

[CreateAssetMenu(fileName = "New Achievement", menuName = "Drug Wars/Achievement")]
public class Achievement : ScriptableObject
{
    [Header("Display")]
    public string Title;
    [TextArea(2, 4)]
    public string Description;
    public Sprite Icon;

    [Header("Unlock Condition")]
    [Tooltip("Which player stat to track.")]
    public AchievementStat Stat;

    [Tooltip("How to compare the stat against the threshold.")]
    public ComparisonType Comparison = ComparisonType.GreaterThanOrEqual;

    [Tooltip("Numeric threshold (e.g. 50000 for '$50k wallet', 5 for '5 combat wins').")]
    public float Threshold;

    [Header("Item Match (OwnsTrenchcoat / OwnsWeapon only)")]
    [Tooltip("Exact Item.Name to match (e.g. 'Leather', 'Shotgun').")]
    public string RequiredItemName;

    [Header("Reward")]
    [Tooltip("Cash bonus on unlock. 0 = no reward.")]
    public int CashReward;

    [Header("Presentation")]
    [Tooltip("Hidden in achievement lists until unlocked.")]
    public bool IsSecret;

    [Tooltip("Sort order in lists (lower = first).")]
    public int SortOrder;

    public string Id => name;
}
