using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Partial class: per-run statistics for the endgame summary + leaderboard.
// All counters reset at character creation via ResetRunStats().
public partial class PlayerStats
{
    // ---- Money (cumulative) ----
    public long TotalSalesRevenue { get; private set; }
    public long TotalDrugSpend { get; private set; }
    public long TotalEquipmentSpend { get; private set; }
    public long TotalInterestPaid { get; private set; }
    public long TotalDebtPaid { get; private set; }
    public long TotalConfiscatedCash { get; private set; }
    public long TotalFinesPaid { get; private set; }
    public long TotalCombatCashLoss { get; private set; }
    public long TotalBribesPaid { get; private set; }
    public long TotalTravelSpend { get; private set; }
    public long TotalBorrowed { get; private set; }

    // ---- Drugs ----
    public int TotalDrugsBought { get; private set; }
    public int TotalDrugsSold { get; private set; }
    public int BiggestSingleSale { get; private set; }
    private readonly Dictionary<string, int> _drugSoldByName = new Dictionary<string, int>();

    // ---- Combat / cop outcomes ----
    public int CombatWins { get; private set; }
    public int CombatLosses { get; private set; }
    public int TimesEscaped { get; private set; }
    public int TimesBribedSuccessfully { get; private set; }

    // ---- Heat ----
    public float PeakHeat { get; private set; }

    // ---- Travel ----
    private readonly HashSet<string> _visitedCityNames = new HashSet<string>();
    public IReadOnlyCollection<string> VisitedCityNames => _visitedCityNames;
    public int UniqueCitiesVisited => _visitedCityNames.Count;

    // ---- Time ----
    // Day on which debt was cleared. -1 if not yet cleared.
    public int DayDebtCleared { get; private set; } = -1;

    // ---- Misc ----
    public int TotalClicks { get; private set; }

    // ---- Click tracking ----
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TotalClicks++;
    }

    // ---- Public increment API ----
    public void RecordDrugBuy(int qty, int totalCost)
    {
        if (qty <= 0) return;
        TotalDrugsBought += qty;
        TotalDrugSpend += totalCost;
    }

    public void RecordDrugSell(string drugName, int qty, int totalRevenue)
    {
        if (qty <= 0) return;
        TotalDrugsSold += qty;
        TotalSalesRevenue += totalRevenue;
        if (totalRevenue > BiggestSingleSale) BiggestSingleSale = totalRevenue;
        if (!string.IsNullOrEmpty(drugName))
        {
            _drugSoldByName.TryGetValue(drugName, out int prev);
            _drugSoldByName[drugName] = prev + qty;
        }
    }

    public void RecordEquipmentBuy(int cost) { if (cost > 0) TotalEquipmentSpend += cost; }
    public void RecordInterestPaid(int amount) { if (amount > 0) TotalInterestPaid += amount; }
    public void RecordDebtPaid(int amount) { if (amount > 0) TotalDebtPaid += amount; }
    public void RecordCashConfiscated(int amount) { if (amount > 0) TotalConfiscatedCash += amount; }
    public void RecordFinePaid(int amount) { if (amount > 0) TotalFinesPaid += amount; }
    public void RecordCombatCashLoss(int amount) { if (amount > 0) TotalCombatCashLoss += amount; }
    public void RecordBribePaid(int amount)
    {
        if (amount <= 0) return;
        TotalBribesPaid += amount;
        TimesBribedSuccessfully++;
    }
    public void RecordTravelSpend(int amount) { if (amount > 0) TotalTravelSpend += amount; }
    public void RecordBorrowed(int amount) { if (amount > 0) TotalBorrowed += amount; }

    public void RecordCombatWin() { CombatWins++; }
    public void RecordCombatLoss() { CombatLosses++; }
    public void RecordEscape() { TimesEscaped++; }

    public void RecordHeatSample(float heat)
    {
        if (heat > PeakHeat) PeakHeat = heat;
    }

    public void RecordCityVisited(string cityName)
    {
        if (!string.IsNullOrEmpty(cityName))
            _visitedCityNames.Add(cityName);
    }

    public void RecordDayDebtCleared(int day)
    {
        if (DayDebtCleared < 0) DayDebtCleared = day;
    }

    public string FavoriteDrug
    {
        get
        {
            if (_drugSoldByName.Count == 0) return null;
            return _drugSoldByName.OrderByDescending(kv => kv.Value).First().Key;
        }
    }

    public int FavoriteDrugQty
    {
        get
        {
            if (_drugSoldByName.Count == 0) return 0;
            return _drugSoldByName.OrderByDescending(kv => kv.Value).First().Value;
        }
    }

    public void ResetRunStats()
    {
        ResetCityHeat();
        TotalSalesRevenue = 0;
        TotalDrugSpend = 0;
        TotalEquipmentSpend = 0;
        TotalInterestPaid = 0;
        TotalDebtPaid = 0;
        TotalConfiscatedCash = 0;
        TotalFinesPaid = 0;
        TotalCombatCashLoss = 0;
        TotalBribesPaid = 0;
        TotalTravelSpend = 0;
        TotalBorrowed = 0;
        TotalDrugsBought = 0;
        TotalDrugsSold = 0;
        BiggestSingleSale = 0;
        _drugSoldByName.Clear();
        CombatWins = 0;
        CombatLosses = 0;
        TimesEscaped = 0;
        TimesBribedSuccessfully = 0;
        PeakHeat = 0f;
        _visitedCityNames.Clear();
        DayDebtCleared = -1;
        TotalClicks = 0;

        // Existing legacy counters in Progression — also reset for a fresh run.
        TimesCaughtByCops = 0;
        TotalCopEncounters = 0;
        CitiesVisited = 0;
    }

    // ---- Save/load helpers ----
    public RunStatsSnapshot CaptureRunStatsSnapshot()
    {
        var snap = new RunStatsSnapshot
        {
            totalSalesRevenue = TotalSalesRevenue,
            totalDrugSpend = TotalDrugSpend,
            totalEquipmentSpend = TotalEquipmentSpend,
            totalInterestPaid = TotalInterestPaid,
            totalDebtPaid = TotalDebtPaid,
            totalConfiscatedCash = TotalConfiscatedCash,
            totalFinesPaid = TotalFinesPaid,
            totalCombatCashLoss = TotalCombatCashLoss,
            totalBribesPaid = TotalBribesPaid,
            totalTravelSpend = TotalTravelSpend,
            totalBorrowed = TotalBorrowed,
            totalDrugsBought = TotalDrugsBought,
            totalDrugsSold = TotalDrugsSold,
            biggestSingleSale = BiggestSingleSale,
            combatWins = CombatWins,
            combatLosses = CombatLosses,
            timesEscaped = TimesEscaped,
            timesBribedSuccessfully = TimesBribedSuccessfully,
            peakHeat = PeakHeat,
            dayDebtCleared = DayDebtCleared,
            totalClicks = TotalClicks,
            visitedCities = new List<string>(_visitedCityNames),
            drugSaleNames = new List<string>(_drugSoldByName.Keys),
            drugSaleCounts = new List<int>(_drugSoldByName.Values),
            cityHeatNames = new List<string>(),
            cityHeatValues = new List<float>()
        };
        CaptureCityHeat(snap.cityHeatNames, snap.cityHeatValues);
        return snap;
    }

    public void RestoreRunStatsSnapshot(RunStatsSnapshot s)
    {
        if (s == null) return;
        TotalSalesRevenue = s.totalSalesRevenue;
        TotalDrugSpend = s.totalDrugSpend;
        TotalEquipmentSpend = s.totalEquipmentSpend;
        TotalInterestPaid = s.totalInterestPaid;
        TotalDebtPaid = s.totalDebtPaid;
        TotalConfiscatedCash = s.totalConfiscatedCash;
        TotalFinesPaid = s.totalFinesPaid;
        TotalCombatCashLoss = s.totalCombatCashLoss;
        TotalBribesPaid = s.totalBribesPaid;
        TotalTravelSpend = s.totalTravelSpend;
        TotalBorrowed = s.totalBorrowed;
        TotalDrugsBought = s.totalDrugsBought;
        TotalDrugsSold = s.totalDrugsSold;
        BiggestSingleSale = s.biggestSingleSale;
        CombatWins = s.combatWins;
        CombatLosses = s.combatLosses;
        TimesEscaped = s.timesEscaped;
        TimesBribedSuccessfully = s.timesBribedSuccessfully;
        PeakHeat = s.peakHeat;
        DayDebtCleared = s.dayDebtCleared;
        TotalClicks = s.totalClicks;

        _visitedCityNames.Clear();
        if (s.visitedCities != null)
            foreach (var c in s.visitedCities) _visitedCityNames.Add(c);

        _drugSoldByName.Clear();
        if (s.drugSaleNames != null && s.drugSaleCounts != null)
        {
            int n = Mathf.Min(s.drugSaleNames.Count, s.drugSaleCounts.Count);
            for (int i = 0; i < n; i++) _drugSoldByName[s.drugSaleNames[i]] = s.drugSaleCounts[i];
        }

        RestoreCityHeat(s.cityHeatNames, s.cityHeatValues);
    }
}

[System.Serializable]
public class RunStatsSnapshot
{
    public long totalSalesRevenue;
    public long totalDrugSpend;
    public long totalEquipmentSpend;
    public long totalInterestPaid;
    public long totalDebtPaid;
    public long totalConfiscatedCash;
    public long totalFinesPaid;
    public long totalCombatCashLoss;
    public long totalBribesPaid;
    public long totalTravelSpend;
    public long totalBorrowed;
    public int totalDrugsBought;
    public int totalDrugsSold;
    public int biggestSingleSale;
    public int combatWins;
    public int combatLosses;
    public int timesEscaped;
    public int timesBribedSuccessfully;
    public float peakHeat;
    public int dayDebtCleared;
    public int totalClicks;
    public List<string> visitedCities = new List<string>();
    public List<string> drugSaleNames = new List<string>();
    public List<int> drugSaleCounts = new List<int>();
    public List<string> cityHeatNames = new List<string>();
    public List<float> cityHeatValues = new List<float>();
}
