using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    private const string PrefsKey = "Achievements_v1";
    private static readonly Color ToastColor = new Color(1f, 0.85f, 0.2f);

    private Achievement[] _all;
    private bool _loaded;
    private readonly HashSet<string> _unlocked = new HashSet<string>();
    private readonly Queue<Achievement> _toastQueue = new Queue<Achievement>();
    private bool _showingToast;
    private bool _subscribed;
    private bool _checking;

    private static readonly HashSet<string> IgnoredScenes = new HashSet<string>
    {
        "Startup", "Intro", "CharCreation", "GameOver", "YouWin"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AchievementManager");
        go.AddComponent<AchievementManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadUnlocked();
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySubscribe();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySubscribe();
        CheckAll();
    }

    // ------------------------------------------------------------------
    //  Event wiring
    // ------------------------------------------------------------------

    private void TrySubscribe()
    {
        if (_subscribed) return;
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        ps.OnWalletChanged += OnWalletChanged;
        ps.OnInventoryChanged += OnInventoryChanged;
        ps.OnDebtChanged += OnDebtChanged;
        PlayerStats.OnRunStatRecorded += CheckAll;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        PlayerStats.OnRunStatRecorded -= CheckAll;
        if (!_subscribed) return;
        var ps = PlayerStats.Instance;
        if (ps != null)
        {
            ps.OnWalletChanged -= OnWalletChanged;
            ps.OnInventoryChanged -= OnInventoryChanged;
            ps.OnDebtChanged -= OnDebtChanged;
        }
        _subscribed = false;
    }

    private void OnWalletChanged(int _) => CheckAll();
    private void OnInventoryChanged() => CheckAll();
    private void OnDebtChanged(int _) => CheckAll();

    // ------------------------------------------------------------------
    //  Achievement data
    // ------------------------------------------------------------------

    private Achievement[] All()
    {
        if (_loaded) return _all;
        var gsm = GameSessionManager.Instance;
        if (gsm == null)
        {
            _all = new Achievement[0];
            return _all;
        }
        var list = gsm.AllAchievements;
        _all = list != null ? list.Where(a => a != null).ToArray() : new Achievement[0];
        _loaded = true;
        return _all;
    }

    // ------------------------------------------------------------------
    //  Evaluation
    // ------------------------------------------------------------------

    private void CheckAll()
    {
        if (_checking) return;
        if (IgnoredScenes.Contains(SceneManager.GetActiveScene().name)) return;
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        _checking = true;
        try
        {
            foreach (var a in All())
            {
                if (_unlocked.Contains(a.Id)) continue;
                if (!Evaluate(a, ps)) continue;

                _unlocked.Add(a.Id);
                SaveUnlocked();

                if (a.CashReward > 0)
                    ps.PlayerWallet += a.CashReward;

                QueueToast(a);
                Debug.Log($"[Achievement] Unlocked: {a.Title}");
            }
        }
        finally
        {
            _checking = false;
        }
    }

    private bool Evaluate(Achievement a, PlayerStats ps)
    {
        if (a.Stat == AchievementStat.OwnsTrenchcoat)
            return ps.CurrentTrench != null &&
                   string.Equals(ps.CurrentTrench.Name, a.RequiredItemName, StringComparison.OrdinalIgnoreCase);

        if (a.Stat == AchievementStat.OwnsWeapon)
            return ps.CurrentWeapon != null &&
                   string.Equals(ps.CurrentWeapon.Name, a.RequiredItemName, StringComparison.OrdinalIgnoreCase);

        if (a.Stat == AchievementStat.DayDebtCleared && ps.DayDebtCleared < 0)
            return false;

        float value = ReadStat(a.Stat, ps);
        switch (a.Comparison)
        {
            case ComparisonType.GreaterThanOrEqual: return value >= a.Threshold;
            case ComparisonType.LessThanOrEqual:    return value <= a.Threshold;
            case ComparisonType.Equal:              return Mathf.Approximately(value, a.Threshold);
            default:                                return false;
        }
    }

    private float ReadStat(AchievementStat stat, PlayerStats ps)
    {
        switch (stat)
        {
            case AchievementStat.PlayerWallet:             return ps.PlayerWallet;
            case AchievementStat.NetWorth:                 return ps.NetWorth;
            case AchievementStat.CurrentHeat:              return ps.CurrentHeat;
            case AchievementStat.DebtAmount:               return ps.Debt;

            case AchievementStat.TotalSalesRevenue:        return ps.TotalSalesRevenue;
            case AchievementStat.TotalDrugSpend:           return ps.TotalDrugSpend;
            case AchievementStat.TotalEquipmentSpend:      return ps.TotalEquipmentSpend;
            case AchievementStat.TotalBribesPaid:          return ps.TotalBribesPaid;
            case AchievementStat.TotalBorrowed:            return ps.TotalBorrowed;
            case AchievementStat.TotalDebtPaid:            return ps.TotalDebtPaid;
            case AchievementStat.TotalConfiscatedCash:     return ps.TotalConfiscatedCash;
            case AchievementStat.TotalFinesPaid:           return ps.TotalFinesPaid;
            case AchievementStat.TotalCombatCashLoss:      return ps.TotalCombatCashLoss;
            case AchievementStat.TotalTravelSpend:         return ps.TotalTravelSpend;
            case AchievementStat.TotalInterestPaid:        return ps.TotalInterestPaid;

            case AchievementStat.TotalDrugsBought:         return ps.TotalDrugsBought;
            case AchievementStat.TotalDrugsSold:           return ps.TotalDrugsSold;
            case AchievementStat.BiggestSingleSale:        return ps.BiggestSingleSale;

            case AchievementStat.CombatWins:               return ps.CombatWins;
            case AchievementStat.CombatLosses:             return ps.CombatLosses;
            case AchievementStat.TimesEscaped:             return ps.TimesEscaped;
            case AchievementStat.TimesBribedSuccessfully:  return ps.TimesBribedSuccessfully;
            case AchievementStat.TotalCopEncounters:       return ps.TotalCopEncounters;
            case AchievementStat.TimesCaughtByCops:        return ps.TimesCaughtByCops;

            case AchievementStat.PeakHeat:                 return ps.PeakHeat;

            case AchievementStat.UniqueCitiesVisited:       return ps.UniqueCitiesVisited;
            case AchievementStat.DayDebtCleared:           return ps.DayDebtCleared;

            default: return 0f;
        }
    }

    // ------------------------------------------------------------------
    //  Toast queue
    // ------------------------------------------------------------------

    private void QueueToast(Achievement a)
    {
        _toastQueue.Enqueue(a);
        if (!_showingToast) StartCoroutine(DrainToastQueue());
    }

    private IEnumerator DrainToastQueue()
    {
        _showingToast = true;
        while (_toastQueue.Count > 0)
        {
            var a = _toastQueue.Dequeue();
            string msg = "ACHIEVEMENT UNLOCKED!\n" + a.Title;
            if (a.CashReward > 0)
                msg += $"\n+${a.CashReward:N0}";

            if (ToastUI.Instance != null)
                ToastUI.Instance.Show(msg, ToastColor);

            if (a.CashReward > 0 && JuiceFX.Instance != null)
            {
                var handler = UnityEngine.Object.FindObjectOfType<CityUIHandler>();
                if (handler != null && handler.WalletRect != null)
                    JuiceFX.Instance.CoinBurstAtUI(handler.WalletRect,
                        Mathf.Clamp(a.CashReward / 500, 3, 20), ToastColor);
            }

            yield return new WaitForSeconds(3.5f);
        }
        _showingToast = false;
    }

    // ------------------------------------------------------------------
    //  Persistence (PlayerPrefs — survives across runs)
    // ------------------------------------------------------------------

    private void LoadUnlocked()
    {
        _unlocked.Clear();
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<AchievementSaveData>(json);
        if (data?.ids != null)
            foreach (var id in data.ids)
                _unlocked.Add(id);
    }

    private void SaveUnlocked()
    {
        var data = new AchievementSaveData { ids = new List<string>(_unlocked) };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------
    //  Public API
    // ------------------------------------------------------------------

    public bool IsUnlocked(Achievement a) => a != null && _unlocked.Contains(a.Id);
    public bool IsUnlocked(string id) => _unlocked.Contains(id);
    public int UnlockedCount => _unlocked.Count;
    public int TotalCount => All().Length;
    public IReadOnlyList<Achievement> GetAll() => All();
    public IEnumerable<Achievement> GetUnlocked() => All().Where(a => _unlocked.Contains(a.Id));

    public void ResetAll()
    {
        _unlocked.Clear();
        SaveUnlocked();
    }

    public void ReloadAchievements()
    {
        _loaded = false;
        All();
    }

    [Serializable]
    private class AchievementSaveData
    {
        public List<string> ids = new List<string>();
    }
}
