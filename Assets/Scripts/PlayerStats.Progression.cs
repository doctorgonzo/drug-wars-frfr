using System;
using System.Linq;
using UnityEngine;

// Partial class: progression-related state (heat, level, city, cop encounters, contraband, debt)
public partial class PlayerStats
{
    [SerializeField] private City currentCity;
    [SerializeField] private bool weaponsAreContraband = false;

    [Header("Loan Shark")]
    [SerializeField] private int startingDebt = 50000;
    [SerializeField] private int dayLimit = 30;
    [Tooltip("Daily interest rate applied to remaining debt (e.g. 0.05 = 5%).")]
    [SerializeField] private float dailyInterestRate = 0.05f;

    public float CurrentHeat { get; set; }
    public int TimesCaughtByCops { get; set; }
    public int CurrentCash { get; set; }
    public int Level { get; set; }

    // Debt
    private int debt;
    public int Debt
    {
        get => debt;
        set
        {
            if (debt == value) return;
            debt = value;
            OnDebtChanged?.Invoke(debt);
        }
    }
    public int DayLimit => dayLimit;
    public float DailyInterestRate => dailyInterestRate;
    public event Action<int> OnDebtChanged;

    public bool IsDebtPaidOff => Debt <= 0;

    public void InitializeDebt()
    {
        Debt = startingDebt;
    }

    public void ApplyDailyInterest()
    {
        if (Debt <= 0) return;
        int interest = Mathf.Max(1, Mathf.RoundToInt(Debt * dailyInterestRate));
        Debt += interest;
    }

    public void PayDebt(int amount)
    {
        amount = Mathf.Clamp(amount, 0, Mathf.Min(amount, PlayerWallet));
        if (amount <= 0) return;
        PlayerWallet -= amount;
        Debt = Mathf.Max(0, Debt - amount);
    }

    public City CurrentCity { get => currentCity; set => currentCity = value; }

    public bool HasContraband =>
        inventory != null &&
        inventory.Any(it =>
            it != null &&
            it.Amount > 0 &&
            (it.Type == ItemType.Drug || (weaponsAreContraband && it.Type == ItemType.Weapon)));
}
