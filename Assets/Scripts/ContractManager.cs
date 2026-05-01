using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Generates contract offers when dealers restock, tracks accepted contracts and their
// deadlines, applies failure penalties to dealer sell ratios. Auto-spawned, no Editor
// wiring required. State persists across saves via RunStatsSnapshot.contracts.
public class ContractManager : MonoBehaviour
{
    public static ContractManager Instance { get; private set; }

    // Tuning constants
    private const float OfferChance = 0.35f;          // Per-restock chance to roll an offer
    private const float SellRatioPenaltyMult = 0.85f; // -15% sell ratio after a failure
    private const int FailurePenaltyDays = 10;
    private const float AdvanceFraction = 0.5f;
    private const float PaymentMultMin = 1.4f;        // × baseCost × quantity
    private const float PaymentMultMax = 2.2f;
    private const int MinDays = 2;
    private const int MaxDays = 4;

    // Per-dealer (instance ID) current pending offer
    private readonly Dictionary<int, Contract> _offers = new Dictionary<int, Contract>();
    // Player's accepted contracts
    private readonly List<Contract> _active = new List<Contract>();
    // Failure penalty: dealer instance ID -> day the penalty expires
    private readonly Dictionary<int, int> _failurePenaltyExpiry = new Dictionary<int, int>();

    public event Action OnContractsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("ContractManager");
        go.AddComponent<ContractManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ----- Offer generation -----

    // Called by GameSessionManager.HandleDayChanged when a dealer restocks. Rolls a new
    // offer (or clears the existing one). If the dealer already has an ACCEPTED contract
    // with the player, no new offer is generated until that one resolves.
    public void OnDealerRestocked(Dealer dealer)
    {
        if (dealer == null) return;
        int id = dealer.GetInstanceID();

        // Don't replace an active contract slot with a new offer
        if (_active.Any(c => c.dealerName == dealer.Name && c.state == ContractState.Accepted))
        {
            _offers.Remove(id);
            return;
        }

        if (UnityEngine.Random.value > OfferChance)
        {
            _offers.Remove(id);
            return;
        }

        var inventory = dealer.Inventory;
        if (inventory == null || inventory.Length == 0) { _offers.Remove(id); return; }

        var drugs = inventory.OfType<Drug>().ToArray();
        if (drugs.Length == 0) { _offers.Remove(id); return; }

        var drug = drugs[UnityEngine.Random.Range(0, drugs.Length)];
        int qty = RollQuantityForRiskTier(drug.RiskTier);
        int days = UnityEngine.Random.Range(MinDays, MaxDays + 1);
        int payment = RollPayment(drug, qty);
        int currentDay = GameTime.Instance != null ? GameTime.Instance.Day : 1;

        _offers[id] = new Contract
        {
            dealerName = dealer.Name,
            drugName = drug.Name,
            quantityRequired = qty,
            totalPayment = payment,
            advancePaid = 0,
            issuedDay = currentDay,
            deadlineDay = currentDay + days,
            state = ContractState.Offered,
        };

        OnContractsChanged?.Invoke();
    }

    private static int RollQuantityForRiskTier(DrugRiskTier tier)
    {
        switch (tier)
        {
            case DrugRiskTier.Safe:   return UnityEngine.Random.Range(30, 61); // 30-60
            case DrugRiskTier.Medium: return UnityEngine.Random.Range(15, 31); // 15-30
            case DrugRiskTier.Hard:   return UnityEngine.Random.Range(8, 19);  // 8-18
            default:                  return 20;
        }
    }

    private static int RollPayment(Drug drug, int quantity)
    {
        float mult = UnityEngine.Random.Range(PaymentMultMin, PaymentMultMax);
        return Mathf.RoundToInt(drug.Cost * quantity * mult);
    }

    // ----- Player actions -----

    public Contract GetOfferForDealer(Dealer dealer)
    {
        if (dealer == null) return null;
        return _offers.TryGetValue(dealer.GetInstanceID(), out var c) ? c : null;
    }

    public Contract GetActiveContractForDealer(Dealer dealer)
    {
        if (dealer == null) return null;
        return _active.FirstOrDefault(c => c.dealerName == dealer.Name && c.state == ContractState.Accepted);
    }

    public bool AcceptOffer(Dealer dealer)
    {
        var offer = GetOfferForDealer(dealer);
        if (offer == null || offer.state != ContractState.Offered) return false;
        if (PlayerStats.Instance == null) return false;

        offer.state = ContractState.Accepted;
        offer.advancePaid = Mathf.RoundToInt(offer.totalPayment * AdvanceFraction);
        PlayerStats.Instance.PlayerWallet += offer.advancePaid;

        _offers.Remove(dealer.GetInstanceID());
        _active.Add(offer);
        OnContractsChanged?.Invoke();
        return true;
    }

    public bool DeclineOffer(Dealer dealer)
    {
        if (dealer == null) return false;
        if (!_offers.ContainsKey(dealer.GetInstanceID())) return false;
        _offers.Remove(dealer.GetInstanceID());
        OnContractsChanged?.Invoke();
        return true;
    }

    // True iff the player has enough of the requested drug to deliver right now.
    public bool CanDeliver(Dealer dealer, out Contract contract, out int playerHas)
    {
        playerHas = 0;
        contract = GetActiveContractForDealer(dealer);
        if (contract == null) return false;
        if (PlayerStats.Instance == null) return false;

        // Copy to a local — C# disallows capturing out parameters inside lambdas.
        var c = contract;
        var item = PlayerStats.Instance.inventory
            .FirstOrDefault(i => i.Type == ItemType.Drug && i.Name == c.drugName);
        playerHas = item != null ? item.Amount : 0;
        return playerHas >= contract.quantityRequired;
    }

    public bool TryDeliver(Dealer dealer)
    {
        if (!CanDeliver(dealer, out var contract, out _)) return false;
        var ps = PlayerStats.Instance;

        // Local copy for lambda capture (out params can't be captured).
        var c = contract;
        var item = ps.inventory.FirstOrDefault(i => i.Type == ItemType.Drug && i.Name == c.drugName);
        if (item == null) return false;

        item.ChangeAmount(-contract.quantityRequired);
        ps.inventory.RemoveAll(i => i.Amount <= 0);

        int finalPayment = contract.RemainingPayment;
        ps.PlayerWallet += finalPayment;
        contract.advancePaid = contract.totalPayment;
        contract.state = ContractState.Completed;
        _active.Remove(contract);
        ps.NotifyInventoryChanged();

        OnContractsChanged?.Invoke();
        return true;
    }

    // ----- Tick & penalties -----

    public void OnDayChanged(int day)
    {
        // Mark expired contracts as Failed
        var failed = _active
            .Where(c => c.state == ContractState.Accepted && day > c.deadlineDay)
            .ToList();
        foreach (var c in failed)
        {
            c.state = ContractState.Failed;
            var dealer = GameSessionManager.Instance?.FindDealerByName(c.dealerName);
            if (dealer != null)
                _failurePenaltyExpiry[dealer.GetInstanceID()] = day + FailurePenaltyDays;
            _active.Remove(c);
        }

        // Drop expired penalties
        var expired = _failurePenaltyExpiry
            .Where(kv => kv.Value <= day)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var id in expired) _failurePenaltyExpiry.Remove(id);

        if (failed.Count > 0 || expired.Count > 0) OnContractsChanged?.Invoke();
    }

    // 0.85f for 10 days after a missed delivery; 1.0f otherwise.
    public float GetSellRatioPenaltyMult(Dealer dealer)
    {
        if (dealer == null) return 1f;
        return _failurePenaltyExpiry.ContainsKey(dealer.GetInstanceID()) ? SellRatioPenaltyMult : 1f;
    }

    public IReadOnlyList<Contract> GetActiveContracts() => _active;

    // ----- Reset & persistence -----

    public void ResetForNewRun()
    {
        _offers.Clear();
        _active.Clear();
        _failurePenaltyExpiry.Clear();
        OnContractsChanged?.Invoke();
    }

    public ContractsSnapshot CaptureSnapshot()
    {
        var snap = new ContractsSnapshot();
        snap.activeContracts.AddRange(_active);

        foreach (var kv in _offers)
        {
            var d = FindDealerById(kv.Key);
            if (d == null) continue;
            snap.offerDealerNames.Add(d.Name);
            snap.offers.Add(kv.Value);
        }
        foreach (var kv in _failurePenaltyExpiry)
        {
            var d = FindDealerById(kv.Key);
            if (d == null) continue;
            snap.penaltyDealerNames.Add(d.Name);
            snap.penaltyExpiryDays.Add(kv.Value);
        }
        return snap;
    }

    public void RestoreSnapshot(ContractsSnapshot snap)
    {
        _offers.Clear();
        _active.Clear();
        _failurePenaltyExpiry.Clear();
        if (snap == null) return;

        if (snap.activeContracts != null)
            _active.AddRange(snap.activeContracts);

        if (snap.offerDealerNames != null && snap.offers != null)
        {
            int n = Mathf.Min(snap.offerDealerNames.Count, snap.offers.Count);
            for (int i = 0; i < n; i++)
            {
                var d = GameSessionManager.Instance?.FindDealerByName(snap.offerDealerNames[i]);
                if (d != null) _offers[d.GetInstanceID()] = snap.offers[i];
            }
        }

        if (snap.penaltyDealerNames != null && snap.penaltyExpiryDays != null)
        {
            int n = Mathf.Min(snap.penaltyDealerNames.Count, snap.penaltyExpiryDays.Count);
            for (int i = 0; i < n; i++)
            {
                var d = GameSessionManager.Instance?.FindDealerByName(snap.penaltyDealerNames[i]);
                if (d != null) _failurePenaltyExpiry[d.GetInstanceID()] = snap.penaltyExpiryDays[i];
            }
        }

        OnContractsChanged?.Invoke();
    }

    private static Dealer FindDealerById(int id)
    {
        var gsm = GameSessionManager.Instance;
        if (gsm?.AllCities == null) return null;
        foreach (var city in gsm.AllCities)
        {
            if (city?.Dealers == null) continue;
            foreach (var d in city.Dealers)
                if (d != null && d.GetInstanceID() == id) return d;
        }
        return null;
    }
}
