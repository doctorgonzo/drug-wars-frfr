using System;
using System.Collections.Generic;
using UnityEngine;

public enum ContractState
{
    Offered = 0,
    Accepted = 1,
    Completed = 2,
    Failed = 3,
}

// Runtime data class for a dealer contract. Lives in ContractManager (offers + active),
// serialized into RunStatsSnapshot.contracts via ContractsSnapshot. Dealer reference is
// stored by name (resolved through GameSessionManager.FindDealerByName at load time)
// because Unity's JsonUtility can't serialize SO references.
[Serializable]
public class Contract
{
    public string dealerName;
    public string drugName;
    public int quantityRequired;
    public int totalPayment;
    public int advancePaid;     // 0 until accepted; equals totalPayment when delivered
    public int issuedDay;
    public int deadlineDay;
    public ContractState state;

    public int RemainingPayment => Mathf.Max(0, totalPayment - advancePaid);
    public int DaysLeft(int currentDay) => Mathf.Max(0, deadlineDay - currentDay);
}

[Serializable]
public class ContractsSnapshot
{
    // Active (accepted) contracts the player is working on
    public List<Contract> activeContracts = new List<Contract>();
    // Pending offers, parallel arrays keyed by dealer name (resolved on restore)
    public List<string> offerDealerNames = new List<string>();
    public List<Contract> offers = new List<Contract>();
    // Failure penalties: dealer name -> day on which the penalty expires
    public List<string> penaltyDealerNames = new List<string>();
    public List<int> penaltyExpiryDays = new List<int>();
}
