using System;
using UnityEngine;

namespace DrugWars.NPC
{
    public enum CopStance { Friendly, Neutral, Hostile }
    public enum CopOpeningAction { DemandBribe, Search, Attack }
    public enum SearchOutcome { None, Steal, Arrest }

    [Serializable]
    public struct CopEncounterSeed
    {
        public int priorCopEncounters;
        public int playerCash;
        public bool playerHasContraband;
        public int playerLevel;
        public int heatAtTrigger; // usually 100
        // 0 = clean/safe, 1 = medium drugs (LSD/Ecstasy), 2 = hard drugs (Crack/Heroin)
        public int contrabandRiskLevel;
    }

    [Serializable]
    public struct CopOpeningDecision
    {
        public CopStance stance;
        public CopOpeningAction opening;

        public int bribeDemand;
        public SearchOutcome searchResult;
        public int stealAmount;
    }

    [CreateAssetMenu(fileName = "NewCop", menuName = "DrugWars/NPC/Cop")]
    public class Cop : ScriptableObject
    {
        [Header("Presentation")]
        public string displayName = "Officer";
        [TextArea] public string description;
        public Sprite portrait;

        [Header("Personality Traits (0..1)")]
        [Range(0f, 1f)] public float corruption = 0.35f; // higher = more likely to steal or accept bribes
        [Range(0f, 1f)] public float violence = 0.25f;   // higher = more likely to attack
        [Range(0f, 1f)] public float greed = 0.5f;       // higher = demands more money
        [Range(0f, 1f)] public float diligence = 0.45f;  // higher = prefers search/arrest to bribes

        [Header("Stance Roll Weights")]
        [Range(0f, 1f)] public float wFriendly = 0.25f;
        [Range(0f, 1f)] public float wNeutral = 0.5f;
        [Range(0f, 1f)] public float wHostile = 0.25f;

        [Tooltip("How much having more cash pushes the cop toward being hostile (shake-down).")]
        public AnimationCurve cashToHostile = AnimationCurve.Linear(0, 0.0f, 5000, 0.15f);
        public AnimationCurve cashToFriendly = AnimationCurve.Linear(0, 0.1f, 5000, 0.0f);
        public AnimationCurve repeatsToHostile = AnimationCurve.Linear(0, 0.0f, 20, 0.25f);

        [Header("Opening Behaviour (0..1 weights)")]
        public float friendlyDemandsBribe = 0.6f;
        public float friendlySearch = 0.35f;

        public float neutralDemandsBribe = 0.45f;
        public float neutralSearch = 0.45f;

        public float hostileDemandsBribe = 0.25f;
        public float hostileSearch = 0.35f;

        [Header("Bribe Settings")]
        public int bribeBase = 100;
        public AnimationCurve bribeByCash = AnimationCurve.Linear(0, 0.8f, 5000, 3f);
        public AnimationCurve bribeByGreed = AnimationCurve.Linear(0, 0.9f, 1, 1.6f);
        [Range(0.5f, 1.2f)] public float minBribeFraction = 0.9f;
        [Range(0f, 1f)] public float bribeAcceptanceJitter = 0.15f;

        [Header("Search Outcomes")]
        public AnimationCurve stealChanceByCorruption = AnimationCurve.Linear(0, 0.15f, 1, 0.85f);
        [Range(0.05f, 0.5f)] public float stealPctMin = 0.1f;
        [Range(0.05f, 0.9f)] public float stealPctMax = 0.4f;
        [Range(0f, 1f)] public float stealEvenWithoutContrabandBias = 0.15f;

        [Header("Dialogue Lines (optional)")]
        public string[] linesWarn;
        public string[] linesDemandBribe;
        public string[] linesAcceptBribe;
        public string[] linesRejectBribe;
        public string[] linesArrest;
        public string[] linesAttack;

        // --------------------------------------------------------------------
        //  ROLL LOGIC
        // --------------------------------------------------------------------
        public CopStance RollStance(in CopEncounterSeed seed, System.Random rng = null)
        {
            rng ??= new System.Random();

            float f = Mathf.Max(0.0001f, wFriendly);
            float n = Mathf.Max(0.0001f, wNeutral);
            float h = Mathf.Max(0.0001f, wHostile);

            f += cashToFriendly.Evaluate(seed.playerCash);
            h += cashToHostile.Evaluate(seed.playerCash);
            h += repeatsToHostile.Evaluate(seed.priorCopEncounters);

            float sum = f + n + h;
            f /= sum; n /= sum; h /= sum;

            double roll = rng.NextDouble();
            if (roll < f) return CopStance.Friendly;
            if (roll < f + n) return CopStance.Neutral;
            return CopStance.Hostile;
        }

        public CopOpeningDecision RollOpening(in CopEncounterSeed seed, System.Random rng = null, bool prerollSearchOutcome = true)
        {
            rng ??= new System.Random();

            CopOpeningDecision result = new CopOpeningDecision
            {
                stance = RollStance(seed, rng),
                opening = CopOpeningAction.DemandBribe,
                bribeDemand = 0,
                searchResult = SearchOutcome.None,
                stealAmount = 0
            };

            float wDemand, wSearch;
            switch (result.stance)
            {
                case CopStance.Friendly:
                    wDemand = friendlyDemandsBribe;
                    wSearch = friendlySearch;
                    break;
                case CopStance.Neutral:
                    wDemand = neutralDemandsBribe;
                    wSearch = neutralSearch;
                    break;
                default:
                    wDemand = hostileDemandsBribe;
                    wSearch = hostileSearch;
                    break;
            }

            float wAttack = 1f - (wDemand + wSearch);
            wAttack += violence * 0.25f;
            wDemand += greed * 0.2f;
            wSearch += Mathf.Max(0f, diligence - 0.5f) * 0.2f;

            float sum = wDemand + wSearch + wAttack;
            wDemand /= sum; wSearch /= sum; wAttack /= sum;

            double r = rng.NextDouble();
            if (r < wDemand) result.opening = CopOpeningAction.DemandBribe;
            else if (r < wDemand + wSearch) result.opening = CopOpeningAction.Search;
            else result.opening = CopOpeningAction.Attack;

            if (result.opening == CopOpeningAction.DemandBribe)
                result.bribeDemand = ComputeBribeDemand(seed.playerCash);
            else if (result.opening == CopOpeningAction.Search && prerollSearchOutcome)
                ResolveSearch(seed, rng, out result.searchResult, out result.stealAmount);

            return result;
        }

        public int ComputeBribeDemand(int playerCash)
        {
            float cashMult = Mathf.Max(0.5f, bribeByCash.Evaluate(playerCash));
            float greedMult = Mathf.Clamp(bribeByGreed.Evaluate(greed), 0.5f, 3f);
            float corrFloor = Mathf.Lerp(1.0f, 0.75f, corruption);

            int ask = Mathf.RoundToInt(bribeBase * cashMult * greedMult * corrFloor);
            int cap = Mathf.RoundToInt(playerCash * Mathf.Lerp(0.85f, 0.45f, corruption));

            return Mathf.Clamp(ask, Mathf.Min(50, playerCash), Mathf.Max(ask, cap));
        }

        public void ResolveSearch(in CopEncounterSeed seed, System.Random rng, out SearchOutcome outcome, out int stealAmount)
        {
            stealAmount = 0;

            // Hard drugs: if found, it's always an arrest — no buying your way out of heroin
            if (seed.playerHasContraband && seed.contrabandRiskLevel >= 2)
            {
                outcome = SearchOutcome.Arrest;
                return;
            }

            float pSteal = stealChanceByCorruption.Evaluate(corruption);

            if (!seed.playerHasContraband)
                pSteal *= stealEvenWithoutContrabandBias;

            // Medium drugs bump steal/arrest chance significantly
            if (seed.contrabandRiskLevel == 1)
                pSteal = Mathf.Min(1f, pSteal * 1.5f);

            double roll = rng.NextDouble();

            if (!seed.playerHasContraband && roll >= pSteal)
            {
                outcome = SearchOutcome.None;
                return;
            }

            if (roll < pSteal)
            {
                outcome = SearchOutcome.Steal;
                float pct = Mathf.Lerp(stealPctMin, stealPctMax, (float)rng.NextDouble());
                stealAmount = Mathf.Clamp(Mathf.RoundToInt(seed.playerCash * pct), 1, seed.playerCash);
            }
            else
            {
                outcome = seed.playerHasContraband ? SearchOutcome.Arrest : SearchOutcome.None;
            }
        }

        public float GetRunSuccessChance(CopStance stance, int priorEncounters)
        {
            // Base chance by stance
            float baseChance = stance switch
            {
                CopStance.Friendly => 0.85f,
                CopStance.Neutral => 0.60f,
                CopStance.Hostile => 0.25f,
                _ => 0.5f
            };

            // Adjust by cop traits
            baseChance -= violence * 0.3f;             // violent cops are better at catching you
            baseChance -= Mathf.Clamp01(diligence - 0.5f) * 0.15f; // diligent cops pursue more effectively

            // Reduce chance slightly for repeat offenders
            baseChance -= Mathf.Clamp01(priorEncounters / 15f) * 0.2f;

            return Mathf.Clamp01(baseChance);
        }
    }
}
