using DrugWars.NPC; // <- the Cop ScriptableObject we wrote earlier
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CopEncounterUIManager : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] private Image copPortrait;
    [SerializeField] private TMP_Text copDialogueText;
    [SerializeField] private Image playerPortrait;

    [Header("Action Buttons (Right Panel)")]
    [SerializeField] private Button runButton;
    [SerializeField] private Button attackButton; // stub for future combat
    [SerializeField] private Button bribeButton;

    [Header("Bribe UI (inline, not a popup)")]
    [SerializeField] private GameObject bribePanel;   // enable/disable whole group
    [SerializeField] private TMP_Text bribeAskText;   // “Officer demands $X”
    [SerializeField] private TMP_InputField bribeInput;
    [SerializeField] private Slider bribeSlider;
    [SerializeField] private Button bribePayButton;
    [SerializeField] private Button bribeHaggleButton;
    [SerializeField] private Button bribeRefuseButton;
    [SerializeField] private TMP_Text bribeHintText;  // small helper text (optional)

    [Header("Combat UI")]
    [SerializeField] private GameObject combatPanel;       // enable when combat starts
    [SerializeField] private Slider playerHPBar;
    [SerializeField] private Slider copHPBar;
    [SerializeField] private TMP_Text playerHPText;
    [SerializeField] private TMP_Text copHPText;
    [SerializeField] private TMP_Text combatLogText;

    [Header("Fade")]
    [SerializeField] private CanvasGroup fadeGroup;   // optional; leave null to skip fade
    [SerializeField] private float fadeDuration = 0.35f;

    // ---- External hooks you can wire from your game systems ----
    public Func<int> GetPlayerCash;              // MUST return current player cash
    public Action<int> SpendPlayerCash;          // deduct cash
    public Func<bool> PlayerHasContraband;       // tells search/arrest text which to show
    public Action OnEncounterResolved;           // scene exit callback (e.g., load map)
    public Action<int> OnCopStoleCash;           // telemetry/UI ping
    public Action OnPlayerArrested;              // handle arrest flow
    public Action OnEscaped;                     // handle escape result
    // ------------------------------------------------------------

    private Cop currentCop;
    private CopEncounterSeed currentSeed;
    private CopOpeningDecision opening;
    private System.Random rng;
    [SerializeField] private Cop[] availableCops;
    private bool isRunOnCooldown = false;
    [SerializeField] private float runCooldown = 3f; // seconds between attempts
    private Coroutine runCooldownRoutine;
    private CopStance activeStance;        // starts as opening.stance, escalates on fail
    private float hostilityRunPenalty = 0f; // reduces run chance after each failed try
    [SerializeField] private float hostilityPenaltyPerFail = 0.12f; // 12% per fail feels punchy
    [SerializeField] private float hostilityPenaltyMax = 0.36f;
    // cache
    private int askAmount;
    private TMP_Text cachedRunLabel;

    // combat state
    private int playerHP;
    private int playerMaxHP;
    private int copHP;
    private int copMaxHP;
    private int copDamage;
    private bool inCombat;

    private void Start()
    {
        var seed = CopEncounterData.Seed;

        // Pick a random cop type
        Cop cop = availableCops[UnityEngine.Random.Range(0, availableCops.Length)];

        // Have that cop roll its stance & opening action
        var startRng = new System.Random();
        var decision = cop.RollOpening(seed, startRng);
        GetPlayerCash = () => PlayerStats.Instance.PlayerWallet;
        SpendPlayerCash = amt =>
        {
            PlayerStats.Instance.PlayerWallet = Mathf.Max(0, PlayerStats.Instance.PlayerWallet - Mathf.Max(0, amt));
            PlayerStats.Instance.NotifyInventoryChanged();
        };
        OnEscaped = ReturnToLastCity;
        OnEncounterResolved = ReturnToLastCity;
        StartEncounter(cop, decision, seed);
    }

    private void ReturnToLastCity()
    {
        StartCoroutine(ReturnAfterDelay(2.0f)); // 2-second delay feels nice
    }

    private IEnumerator ReturnAfterDelay(float delaySeconds)
    {
        // Wait for dialogue to finish
        yield return new WaitForSeconds(delaySeconds);

        // Trigger fade-out
        if (FadeController.Instance != null)
        {
            FadeController.Instance.FadeOut(1f); // 1-second fade
        }
            
        else
            Debug.LogWarning("No FadeController found in scene!");

        // Give the fade time to complete
        yield return new WaitForSeconds(1.1f);
        FadeController.Instance.FadeIn(1f);
        var target = CopEncounterData.ReturnSceneName;
        if (!string.IsNullOrEmpty(target))
            UnityEngine.SceneManagement.SceneManager.LoadScene(target);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("City_Hub");
    }

    // ---- Public entry point ----
    public void StartEncounter(Cop cop, CopOpeningDecision decision, CopEncounterSeed seed, System.Random seededRng = null)
    {
        currentCop = cop;
        currentSeed = seed;
        opening = decision;
        activeStance = opening.stance; // track stance that can escalate during the encounter
        hostilityRunPenalty = 0f;
        rng = seededRng ?? new System.Random();

        // Dialogue: opening line
        copDialogueText.text = BuildOpeningLine(decision);

        // Buttons
        cachedRunLabel = runButton.GetComponentInChildren<TMP_Text>();
        runButton.onClick.RemoveAllListeners();
        runButton.onClick.AddListener(OnRunClicked);

        attackButton.onClick.RemoveAllListeners();
        attackButton.onClick.AddListener(OnAttackClicked);

        bribeButton.onClick.RemoveAllListeners();
        bribeButton.onClick.AddListener(ShowBribePanel);

        // Initial availability: only show Bribe if either (a) opening is DemandBribe, or (b) cop is bribeable by design
        bool canBribeNow = decision.opening == CopOpeningAction.DemandBribe || currentCop.corruption > 0.05f;
        bribeButton.gameObject.SetActive(canBribeNow);

        // If the opening is DemandBribe, auto-show the inline panel
        if (decision.opening == CopOpeningAction.DemandBribe)
            ShowBribePanel();
        else
            bribePanel.SetActive(false);
    }

    // ---------------- Bribe flow ----------------

    private void ShowBribePanel()
    {
        bribePanel.SetActive(true);

        int playerCash = SafeGetCash();
        askAmount = opening.bribeDemand > 0 ? opening.bribeDemand : currentCop.ComputeBribeDemand(playerCash);

        bribeAskText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesDemandBribe, $"That heat isn’t cheap to forget. {Money(askAmount)} and you walk.")}\"";
        bribeHintText.text = $"You have {Money(playerCash)}.";

        // Configure slider/input to player cash
        bribeSlider.minValue = 0;
        bribeSlider.maxValue = Mathf.Max(askAmount, playerCash);
        bribeSlider.wholeNumbers = true;
        bribeSlider.value = Mathf.Min(askAmount, playerCash);

        bribeInput.text = Mathf.Min(askAmount, playerCash).ToString();

        bribeSlider.onValueChanged.RemoveAllListeners();
        bribeSlider.onValueChanged.AddListener(v => bribeInput.text = ((int)v).ToString());

        bribeInput.onEndEdit.RemoveAllListeners();
        bribeInput.onEndEdit.AddListener(s =>
        {
            if (!int.TryParse(s, out var amt)) amt = 0;
            amt = Mathf.Clamp(amt, 0, SafeGetCash());
            bribeSlider.value = amt;
            bribeInput.text = amt.ToString();
        });

        // Buttons
        bribePayButton.onClick.RemoveAllListeners();
        bribePayButton.onClick.AddListener(() => OnBribePayClicked((int)bribeSlider.value));

        bribeHaggleButton.onClick.RemoveAllListeners();
        bribeHaggleButton.onClick.AddListener(OnBribeHaggleClicked);

        bribeRefuseButton.onClick.RemoveAllListeners();
        bribeRefuseButton.onClick.AddListener(OnBribeRefuseClicked);
    }

    private void OnBribePayClicked(int offer)
    {
        int cash = SafeGetCash();
        offer = Mathf.Clamp(offer, 0, cash);

        // Compute adequacy: same idea as earlier — adequate if >= ask * minFraction
        float minFrac = currentCop.minBribeFraction;
        bool adequate = offer >= Mathf.CeilToInt(askAmount * minFrac);

        // Chance to accept = corruption +/- jitter
        float acceptChance = Mathf.Clamp01(currentCop.corruption + UnityEngine.Random.Range(-currentCop.bribeAcceptanceJitter, currentCop.bribeAcceptanceJitter));
        bool accepted = adequate && (rng.NextDouble() < acceptChance);

        if (accepted)
        {
            SpendPlayerCash?.Invoke(offer);
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAcceptBribe, "Fine. We never met.")}\"";
            EndEncounter(success: true);
            return;
        }

        // Not accepted → either demand more or escalate based on stance/greed
        if (adequate)
        {
            // still rejected (low corruption): escalate to Search or Attack depending on violence/diligence
            bool goesViolent = rng.NextDouble() < (0.35 + currentCop.violence * 0.4);
            if (goesViolent)
            {
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "Enough. Hands where I can see 'em!")}\"";
                StartCombat();
            }
            else
            {
                // Search
                PerformSearch();
            }
        }
        else
        {
            // Demand more (counter-offer)
            int playerCash = SafeGetCash();
            int newAsk = Mathf.Min(currentCop.ComputeBribeDemand(playerCash), Mathf.Max(askAmount, offer + Mathf.RoundToInt((askAmount - offer) * 0.7f)));
            askAmount = newAsk;

            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesRejectBribe, $"Not enough. Make it {Money(newAsk)}.")}\"";
            bribeHintText.text = $"You have {Money(playerCash)}.";
            bribeSlider.maxValue = Mathf.Max(newAsk, playerCash);
            bribeSlider.value = Mathf.Min(newAsk, playerCash);
            bribeInput.text = bribeSlider.value.ToString();
        }
    }

    private void OnBribeHaggleClicked()
    {
        // Lower ask a bit if cop is corrupt or friendly; otherwise maybe raise!
        float haggle = Mathf.Lerp(0.85f, 0.60f, currentCop.corruption); // more corrupt => lower quicker
        int playerCash = SafeGetCash();
        int newAsk = Mathf.Clamp(Mathf.RoundToInt(askAmount * haggle), 10, playerCash);
        if (newAsk < askAmount)
        {
            askAmount = newAsk;
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesDemandBribe, $"Fine. {Money(newAsk)}. Last chance.")}\"";
        }
        else
        {
            // backfire slightly for honest/hostile types
            askAmount = Mathf.Min(playerCash, askAmount + Mathf.RoundToInt(askAmount * 0.10f));
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesRejectBribe, "You’re wasting my time. Price just went up.")}\"";
        }

        bribeSlider.maxValue = Mathf.Max(askAmount, playerCash);
        bribeSlider.value = Mathf.Min(askAmount, playerCash);
        bribeInput.text = bribeSlider.value.ToString();
        bribeHintText.text = $"You have {Money(playerCash)}.";
    }

    private void OnBribeRefuseClicked()
    {
        // Refusing pushes to Search or Attack based on cop’s diligence/violence
        bool goesSearch = rng.NextDouble() < Mathf.Lerp(0.25f, 0.75f, currentCop.diligence);
        if (goesSearch)
        {
            PerformSearch();
        }
        else
        {
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "Bad choice.")}\"";
            StartCombat();
        }
    }

    private void PerformSearch()
    {
        currentCop.ResolveSearch(currentSeed, rng, out var outcome, out var steal);

        switch (outcome)
        {
            case SearchOutcome.Steal:
                SpendPlayerCash?.Invoke(Mathf.Min(steal, SafeGetCash()));
                OnCopStoleCash?.Invoke(steal);
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, $"Confiscated {Money(steal)}. Move along.")}\"";
                EndEncounter(success: false); // you “survive” but it’s a loss
                break;

            case SearchOutcome.Arrest:
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesArrest, "You’re under arrest.")}\"";
                OnPlayerArrested?.Invoke();
                EndEncounter(success: false);
                break;

            default:
                // None (no contraband and not corrupt enough to steal)
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, "Keep your nose clean.")}\"";
                EndEncounter(success: true);
                break;
        }
    }

    // ---------------- Run flow ----------------

    private void OnRunClicked()
    {
        if (isRunOnCooldown) return;

        // Base chance from the cop (by stance & prior encounters)
        float chance = currentCop.GetRunSuccessChance(activeStance, currentSeed.priorCopEncounters);

        // Apply hostility penalty from previous failures
        chance = Mathf.Clamp01(chance - hostilityRunPenalty);

        bool escaped = UnityEngine.Random.value < chance;

        // Start cooldown timer either way
        if (runCooldownRoutine != null) StopCoroutine(runCooldownRoutine);
        runCooldownRoutine = StartCoroutine(RunCooldownTimer(cachedRunLabel));

        if (escaped)
        {
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, "Tch—he got away!")}\"";
            OnEscaped?.Invoke();
            EndEncounter(success: true);
            return;
        }

        // ---- FAILED RUN: escalate hostility & react ----
        EscalateHostility();                       // bump Friendly→Neutral→Hostile
        hostilityRunPenalty = Mathf.Min(hostilityPenaltyMax, hostilityRunPenalty + hostilityPenaltyPerFail);

        // Dialogue based on NEW stance
        if (activeStance == CopStance.Hostile)
        {
            // Hostile after failure → attack or hard threat
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "Freeze! You're not going anywhere.")}\"";
            StartCombat();
            return;
        }
        else
        {
            // Non-hostile escalates to pressure/search
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, "Try that again and it's cuffs.")}\"";
            bool goesSearch = UnityEngine.Random.value < Mathf.Lerp(0.3f, 0.8f, currentCop.diligence);
            if (goesSearch)
            {
                PerformSearch();
            }
            else
            {
                // Soft escalate to arrest sometimes even if not hostile
                bool arrestsNow = UnityEngine.Random.value < 0.35f;
                if (arrestsNow)
                {
                    copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesArrest, "That’s it—you're under arrest.")}\"";
                    OnPlayerArrested?.Invoke();
                    EndEncounter(success: false);
                }
            }
        }
    }

    // ------------------------------------
    // Cooldown coroutine (updates Run button text)
    // ------------------------------------
    private IEnumerator RunCooldownTimer(TMP_Text runLabel)
    {
        isRunOnCooldown = true;
        runButton.interactable = false;

        float timeLeft = runCooldown;
        while (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            if (runLabel != null)
                runLabel.text = $"Run ({Mathf.CeilToInt(timeLeft)}s)";
            yield return null;
        }

        isRunOnCooldown = false;
        runButton.interactable = true;
        if (runLabel != null)
            runLabel.text = "Try to Run";
    }

    // ---------------- Attack / Combat ----------------
    private void OnAttackClicked()
    {
        if (inCombat) return;
        StartCombat();
    }

    private void StartCombat()
    {
        inCombat = true;

        // Disable non-combat buttons
        runButton.interactable = false;
        bribeButton.interactable = false;
        bribePanel.SetActive(false);

        // Player stats
        int armor = PlayerStats.Instance.CurrentTrench != null ? PlayerStats.Instance.CurrentTrench.ArmorValue : 0;
        playerMaxHP = 50 + armor * 5;
        playerHP = playerMaxHP;

        // Cop stats scale with violence and stance
        float stanceMult = activeStance switch
        {
            CopStance.Friendly => 0.7f,
            CopStance.Neutral => 1.0f,
            CopStance.Hostile => 1.4f,
            _ => 1f
        };
        copMaxHP = Mathf.RoundToInt((40 + currentCop.violence * 60f) * stanceMult);
        copHP = copMaxHP;
        copDamage = Mathf.RoundToInt((5 + currentCop.violence * 15f) * stanceMult);

        // Show combat panel
        if (combatPanel != null) combatPanel.SetActive(true);
        if (combatLogText != null)
        {
            combatLogText.enableAutoSizing = true;
            combatLogText.text = "";
        }
        UpdateCombatUI();

        copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "You picked the wrong day.")}\"";

        // Re-wire attack button for combat hits
        attackButton.onClick.RemoveAllListeners();
        attackButton.onClick.AddListener(OnCombatAttack);

        var attackLabel = attackButton.GetComponentInChildren<TMP_Text>();
        if (attackLabel != null) attackLabel.text = "Strike";
    }

    private void OnCombatAttack()
    {
        if (!inCombat) return;
        StartCoroutine(CombatTurn());
    }

    private IEnumerator CombatTurn()
    {
        attackButton.interactable = false;

        // Player attacks cop
        int playerDmg = PlayerStats.Instance.CurrentWeapon != null ? PlayerStats.Instance.CurrentWeapon.Damage : 5;
        int variance = Mathf.Max(1, playerDmg / 4);
        int actualPlayerDmg = playerDmg + UnityEngine.Random.Range(-variance, variance + 1);
        actualPlayerDmg = Mathf.Max(1, actualPlayerDmg);
        copHP = Mathf.Max(0, copHP - actualPlayerDmg);

        if (combatLogText != null)
            combatLogText.text = $"You deal {actualPlayerDmg} damage!";
        UpdateCombatUI();

        if (copHP <= 0)
        {
            copDialogueText.text = $"{currentCop.displayName} is down! You escaped.";
            if (combatLogText != null)
                combatLogText.text = $"{currentCop.displayName} defeated! You got away.";
            inCombat = false;

            // Beating a cop leaves you at 50% heat
            PlayerStats.Instance.CurrentHeat = 50f;

            OnEscaped?.Invoke();
            EndEncounter(success: true);
            yield break;
        }

        yield return new WaitForSeconds(0.6f);

        // Cop attacks player
        int armor = PlayerStats.Instance.CurrentTrench != null ? PlayerStats.Instance.CurrentTrench.ArmorValue : 0;
        int copVariance = Mathf.Max(1, copDamage / 4);
        int actualCopDmg = copDamage + UnityEngine.Random.Range(-copVariance, copVariance + 1);
        int reduction = Mathf.RoundToInt(armor * 0.5f);
        actualCopDmg = Mathf.Max(1, actualCopDmg - reduction);
        playerHP = Mathf.Max(0, playerHP - actualCopDmg);

        if (combatLogText != null)
            combatLogText.text = $"{currentCop.displayName} deals {actualCopDmg} damage!";
        copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "Is that all you got?")}\"";
        UpdateCombatUI();

        if (playerHP <= 0)
        {
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesArrest, "Should've cooperated.")}\"";
            inCombat = false;

            // Lose a percentage of cash as penalty
            int cashLoss = Mathf.RoundToInt(PlayerStats.Instance.PlayerWallet * 0.25f);
            SpendPlayerCash?.Invoke(cashLoss);

            if (combatLogText != null)
                combatLogText.text = $"You were beaten. Lost ${cashLoss:N0}.";

            OnPlayerArrested?.Invoke();
            EndEncounter(success: false);
            yield break;
        }

        attackButton.interactable = true;
    }

    private void UpdateCombatUI()
    {
        if (playerHPBar != null)
        {
            playerHPBar.maxValue = playerMaxHP;
            playerHPBar.value = playerHP;
        }
        if (copHPBar != null)
        {
            copHPBar.maxValue = copMaxHP;
            copHPBar.value = copHP;
        }
        if (playerHPText != null)
            playerHPText.text = $"HP: {playerHP}/{playerMaxHP}";
        if (copHPText != null)
            copHPText.text = $"HP: {copHP}/{copMaxHP}";
    }

    private void EscalateHostility()
    {
        switch (activeStance)
        {
            case CopStance.Friendly:
                activeStance = CopStance.Neutral;
                break;
            case CopStance.Neutral:
                activeStance = CopStance.Hostile;
                break;
            case CopStance.Hostile:
                // already maxed
                break;
        }
    }

    private void EndEncounter(bool success)
    {
        // Disable interaction
        runButton.interactable = false;
        attackButton.interactable = false;
        bribeButton.interactable = false;
        bribePayButton.interactable = false;
        bribeHaggleButton.interactable = false;
        bribeRefuseButton.interactable = false;

        StartCoroutine(FadeAndExit());
    }

    private IEnumerator FadeAndExit()
    {
        if (fadeGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                fadeGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
                yield return null;
            }
        }
        OnEncounterResolved?.Invoke();
    }

    private string RandomLine(string[] pool, string fallback)
    {
        if (pool != null && pool.Length > 0)
        {
            int i = UnityEngine.Random.Range(0, pool.Length);
            if (!string.IsNullOrWhiteSpace(pool[i])) return pool[i];
        }
        return fallback;
    }

    private int SafeGetCash() => Mathf.Max(0, GetPlayerCash?.Invoke() ?? 0);
    private static string Money(int amt) => $"${amt:n0}";
    private string BuildOpeningLine(CopOpeningDecision d)
    {
        switch (d.opening)
        {
            case CopOpeningAction.DemandBribe:
                int ask = d.bribeDemand > 0 ? d.bribeDemand : currentCop.ComputeBribeDemand(SafeGetCash());
                return $"{currentCop.displayName}: \"{RandomLine(currentCop.linesDemandBribe, $"Stop. We can make this go away for {Money(ask)}.")}\"";
            case CopOpeningAction.Search:
                return $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, "Stop right there. Search time.")}\"";
            case CopOpeningAction.Attack:
                return $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "Down on the ground!")}\"";
            default:
                return $"{currentCop.displayName}: \"...\"";
        }
    }
}
