using DrugWars.NPC; // <- the Cop ScriptableObject we wrote earlier
using System;
using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private TMP_Text bribeAskText;   // "Officer demands $X"
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
    private bool _searchPrerolled;  // true when RollOpening pre-computed a search result we haven't used yet
    private int _haggleCount;
    [SerializeField] private int maxHaggles = 3;
    [SerializeField] private int maxFailedPayAttempts = 4;
    private int _failedPayAttempts;
    private string _lastBribeLine;
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
    private bool copStunned;
    private int copEmpoweredTurns;
    private int copPowerCooldown;
    private readonly List<string> _combatLog = new List<string>();
    private const int CombatLogMaxLines = 6;
    private GameObject _combatActionsContainer;
    private Button _strikeBtn, _specialBtn, _fleeBtn, _bribeBtn;
    private bool _inPlayerTurn;

    private enum CombatPlayerAction { Strike, Special, Flee, Bribe }
    private enum CopCombatAction    { Strike, PowerStrike, Shakedown, CallBackup, StandDown }

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
        OnPlayerArrested = HandleArrest;
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
        activeStance = opening.stance;
        hostilityRunPenalty = 0f;
        _haggleCount = 0;
        _failedPayAttempts = 0;
        _lastBribeLine = null;
        _searchPrerolled = opening.opening == CopOpeningAction.Search;
        rng = seededRng ?? new System.Random();

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.TotalCopEncounters++;

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
        int rawAsk = opening.bribeDemand > 0 ? opening.bribeDemand : currentCop.ComputeBribeDemand(playerCash);
        // Hard drugs = 2x bribe, medium drugs = 1.5x — carrying heroin costs you
        float riskBribeMult = 1f + currentSeed.contrabandRiskLevel * 0.5f;
        askAmount = Mathf.RoundToInt(rawAsk * riskBribeMult);

        // Wire listeners once per panel show
        bribeSlider.onValueChanged.RemoveAllListeners();
        bribeSlider.onValueChanged.AddListener(v => bribeInput.SetTextWithoutNotify(((int)v).ToString()));

        bribeInput.onValueChanged.RemoveAllListeners();
        bribeInput.onValueChanged.AddListener(s =>
        {
            if (!int.TryParse(s, out var amt)) amt = 0;
            amt = Mathf.Clamp(amt, 0, SafeGetCash());
            bribeSlider.SetValueWithoutNotify(amt);
        });

        bribeInput.onEndEdit.RemoveAllListeners();
        bribeInput.onEndEdit.AddListener(s =>
        {
            if (!int.TryParse(s, out var amt)) amt = 0;
            amt = Mathf.Clamp(amt, 0, SafeGetCash());
            bribeSlider.value = amt;
            bribeInput.SetTextWithoutNotify(amt.ToString());
        });

        bribePayButton.onClick.RemoveAllListeners();
        bribePayButton.onClick.AddListener(() => OnBribePayClicked(GetCurrentBribeOffer()));

        bribeHaggleButton.onClick.RemoveAllListeners();
        bribeHaggleButton.onClick.AddListener(OnBribeHaggleClicked);

        bribeRefuseButton.onClick.RemoveAllListeners();
        bribeRefuseButton.onClick.AddListener(OnBribeRefuseClicked);

        // Initial demand line goes to the outer dialogue area
        copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesDemandBribe, $"That heat isn't cheap to forget. {Money(askAmount)} and you walk.")}\"";
        RefreshBribePanelUI();
    }

    // Pull the offer from the input field first (most recent typing wins), fall back to slider.
    private int GetCurrentBribeOffer()
    {
        if (int.TryParse(bribeInput.text, out var amt))
            return Mathf.Clamp(amt, 0, SafeGetCash());
        return Mathf.Clamp((int)bribeSlider.value, 0, SafeGetCash());
    }

    // Refresh the *informational* panel UI: current ask, cash hint, slider/input bounds.
    // Dialogue (bribeAskText is a header in the panel; copDialogueText is the outer area)
    // is set by the caller for each branch.
    private void RefreshBribePanelUI()
    {
        int playerCash = SafeGetCash();
        bribeAskText.text = $"<b>{currentCop.displayName} demands {Money(askAmount)}</b>";
        bribeHintText.text = $"You have {Money(playerCash)}.";

        bribeSlider.minValue = 0;
        bribeSlider.maxValue = Mathf.Max(1, playerCash);
        bribeSlider.wholeNumbers = true;
        int defaultOffer = Mathf.Min(askAmount, playerCash);
        bribeSlider.SetValueWithoutNotify(defaultOffer);
        bribeInput.SetTextWithoutNotify(defaultOffer.ToString());
    }

    // RandomLine that avoids repeating the last bribe line so back-to-back dialogue doesn't look stuck.
    private string PickBribeLine(string[] pool, string fallback)
    {
        string picked = RandomLine(pool, fallback, _lastBribeLine);
        _lastBribeLine = picked;
        return picked;
    }

    private void OnBribePayClicked(int offer)
    {
        int cash = SafeGetCash();
        offer = Mathf.Clamp(offer, 0, cash);

        float minFrac = currentCop.minBribeFraction;
        bool adequate = offer >= Mathf.CeilToInt(askAmount * minFrac);
        bool overpay  = offer >= askAmount;

        // Overpay sharply boosts acceptance — a stubborn cop shouldn't reject more than asked
        float acceptChance = Mathf.Clamp01(currentCop.corruption + UnityEngine.Random.Range(-currentCop.bribeAcceptanceJitter, currentCop.bribeAcceptanceJitter));
        if (overpay) acceptChance = Mathf.Max(acceptChance, 0.9f);
        bool accepted = adequate && (rng.NextDouble() < acceptChance);

        if (accepted)
        {
            SpendPlayerCash?.Invoke(offer);
            copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAcceptBribe, "Fine. We never met.")}\"";
            EndEncounter(success: true);
            return;
        }

        // Cop's patience runs out after too many failed pay attempts
        _failedPayAttempts++;
        if (_failedPayAttempts >= maxFailedPayAttempts)
        {
            copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesRejectBribe, "Done playing games.")}\"";
            bribePanel.SetActive(false);
            bool goesViolent = rng.NextDouble() < (0.3 + currentCop.violence * 0.4);
            if (goesViolent) StartCombat();
            else PerformSearch();
            return;
        }

        if (overpay)
        {
            // Player paid more than asked — don't raise the ask, the cop is just being difficult
            copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesRejectBribe, "Even that's not enough today.")}\"";
        }
        else if (adequate)
        {
            // Adequate but unlucky roll — small bump to the ask
            int playerCash = SafeGetCash();
            int newAsk = Mathf.Min(currentCop.ComputeBribeDemand(playerCash), askAmount + Mathf.RoundToInt((askAmount - offer) * 0.5f));
            askAmount = Mathf.Max(askAmount, newAsk);
            copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesRejectBribe, $"I need a little more to forget this. {Money(askAmount)}.")}\"";
        }
        else
        {
            // Inadequate offer — counter-offer goes up
            int playerCash = SafeGetCash();
            int newAsk = Mathf.Min(currentCop.ComputeBribeDemand(playerCash), askAmount + Mathf.RoundToInt((askAmount - offer) * 0.7f));
            askAmount = Mathf.Max(askAmount, newAsk);
            copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesRejectBribe, $"Not enough. Make it {Money(askAmount)}.")}\"";
        }

        RefreshBribePanelUI();
    }

    private void OnBribeHaggleClicked()
    {
        _haggleCount++;

        // Out of haggle attempts — cop loses patience and goes straight to search/attack
        if (_haggleCount > maxHaggles)
        {
            copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesRejectBribe, "Enough stalling.")}\"";
            bribeHaggleButton.interactable = false;
            bribePanel.SetActive(false);
            bool goesViolent = rng.NextDouble() < (0.3 + currentCop.violence * 0.4);
            if (goesViolent) StartCombat();
            else PerformSearch();
            return;
        }

        // Lower ask if cop is corrupt/friendly; raise if honest/hostile
        float haggle = Mathf.Lerp(0.85f, 0.60f, currentCop.corruption);
        int playerCash = SafeGetCash();
        int newAsk = Mathf.Clamp(Mathf.RoundToInt(askAmount * haggle), 10, Mathf.Max(10, playerCash));
        if (newAsk < askAmount)
        {
            askAmount = newAsk;
            string lastChance = _haggleCount >= maxHaggles ? " Last chance." : "";
            copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesDemandBribe, $"Fine. {Money(newAsk)}.{lastChance}")}\"";
            if (_haggleCount >= maxHaggles)
                bribeHaggleButton.interactable = false;
        }
        else
        {
            askAmount = askAmount + Mathf.RoundToInt(askAmount * 0.10f);
            copDialogueText.text = $"{currentCop.displayName}: \"{PickBribeLine(currentCop.linesRejectBribe, "You're wasting my time. Price just went up.")}\"";
        }

        RefreshBribePanelUI();
    }

    private void OnBribeRefuseClicked()
    {
        // Refusing pushes to Search or Attack based on cop's diligence/violence
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
        SearchOutcome outcome;
        int steal;
        if (_searchPrerolled)
        {
            outcome = opening.searchResult;
            steal = opening.stealAmount;
            _searchPrerolled = false;
        }
        else
        {
            currentCop.ResolveSearch(currentSeed, rng, out outcome, out steal);
        }

        switch (outcome)
        {
            case SearchOutcome.Steal:
            {
                int actualSteal = Mathf.Min(steal, SafeGetCash());
                SpendPlayerCash?.Invoke(actualSteal);
                OnCopStoleCash?.Invoke(actualSteal);
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, "Move along.")}\"" +
                    FormatLossSummary(actualSteal, "confiscated", null);
                EndEncounter(success: false);
                break;
            }

            case SearchOutcome.Arrest:
            {
                int arrestFine = Mathf.RoundToInt(PlayerStats.Instance.PlayerWallet * 0.20f);
                string searchDrugList = BuildDrugConfiscationList();
                SpendPlayerCash?.Invoke(arrestFine);
                OnPlayerArrested?.Invoke();
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesArrest, "You're under arrest.")}\"" +
                    FormatLossSummary(arrestFine, "fine (20%)", searchDrugList);
                EndEncounter(success: false);
                break;
            }

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

        // Hard drugs make it much harder to run — cop isn't letting a heroin dealer walk
        chance -= currentSeed.contrabandRiskLevel * 0.15f;

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
                    int runArrestFine = Mathf.RoundToInt(PlayerStats.Instance.PlayerWallet * 0.15f);
                    string runDrugList = BuildDrugConfiscationList();
                    SpendPlayerCash?.Invoke(runArrestFine);
                    OnPlayerArrested?.Invoke();
                    copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesArrest, "That's it—you're under arrest.")}\"" +
                        FormatLossSummary(runArrestFine, "fine (15%)", runDrugList);
                    EndEncounter(success: false);
                }
                else
                {
                    // Cop lets it go — player escapes with a warning
                    copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, "Get out of here. Don't let me see you again.")}\"";
                    EndEncounter(success: true);
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
        inCombat            = true;
        copStunned          = false;
        copEmpoweredTurns   = 0;
        copPowerCooldown    = 0;
        _combatLog.Clear();

        attackButton.gameObject.SetActive(false);
        runButton.interactable   = false;
        bribeButton.interactable = false;
        bribePanel.SetActive(false);

        int armor = PlayerStats.Instance.CurrentTrench != null ? PlayerStats.Instance.CurrentTrench.ArmorValue : 0;
        playerMaxHP = 50 + armor * 5;
        playerHP    = playerMaxHP;

        float stanceMult = activeStance switch
        {
            CopStance.Friendly => 0.7f,
            CopStance.Neutral  => 1.0f,
            CopStance.Hostile  => 1.4f,
            _                  => 1f
        };
        copMaxHP  = Mathf.RoundToInt((40 + currentCop.violence * 60f) * stanceMult);
        copHP     = copMaxHP;
        copDamage = Mathf.RoundToInt((5  + currentCop.violence * 15f) * stanceMult);

        if (combatPanel != null) combatPanel.SetActive(true);
        if (combatLogText != null)
        {
            combatLogText.enableAutoSizing = false;
            combatLogText.fontSize         = 13f;
            combatLogText.text             = "";
        }
        UpdateCombatUI();

        copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "You picked the wrong day.")}\"";

        BuildCombatActionUI();
        AppendLog($"Combat! {currentCop.displayName} — {copMaxHP} HP");
        StartPlayerTurn();
    }

    private void BuildCombatActionUI()
    {
        if (_combatActionsContainer != null) Destroy(_combatActionsContainer);

        _combatActionsContainer = new GameObject("CombatActions");
        _combatActionsContainer.transform.SetParent(combatPanel.transform, false);

        var rt = _combatActionsContainer.AddComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0f, 0f);
        rt.anchorMax       = new Vector2(1f, 0f);
        rt.pivot           = new Vector2(0.5f, 0f);
        rt.sizeDelta       = new Vector2(0f, 120f);
        rt.anchoredPosition = new Vector2(0f, 8f);

        var grid = _combatActionsContainer.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(140f, 50f);
        grid.spacing         = new Vector2(8f, 8f);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment  = TextAnchor.LowerCenter;
        grid.padding         = new RectOffset(8, 8, 8, 8);

        string wName       = (PlayerStats.Instance.CurrentWeapon?.name ?? "").ToLower();
        string specialLabel = wName.Contains("shotgun")                           ? "Buckshot"  :
                              wName.Contains("handgun") || wName.Contains("gun") ? "Aimed Shot" :
                                                                                    "Improvise";

        _strikeBtn  = MakeCombatButton(_combatActionsContainer, "Strike",      new Color(0.25f, 0.45f, 0.75f));
        _specialBtn = MakeCombatButton(_combatActionsContainer, specialLabel,  new Color(0.65f, 0.50f, 0.05f));
        _fleeBtn    = MakeCombatButton(_combatActionsContainer, "Flee",        new Color(0.55f, 0.30f, 0.05f));
        _bribeBtn   = MakeCombatButton(_combatActionsContainer, "Bribe",       new Color(0.15f, 0.45f, 0.20f));

        _strikeBtn.onClick.AddListener(() => TryPlayerAction(CombatPlayerAction.Strike));
        _specialBtn.onClick.AddListener(() => TryPlayerAction(CombatPlayerAction.Special));
        _fleeBtn.onClick.AddListener(() => TryPlayerAction(CombatPlayerAction.Flee));
        _bribeBtn.onClick.AddListener(() => TryPlayerAction(CombatPlayerAction.Bribe));

        _bribeBtn.interactable = currentCop.corruption >= 0.3f;
    }

    private Button MakeCombatButton(GameObject parent, string label, Color bg)
    {
        var go  = new GameObject(label + "Btn");
        go.transform.SetParent(parent.transform, false);

        var img = go.AddComponent<Image>();
        img.color = bg;

        var btn    = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = bg;
        colors.highlightedColor = bg * 1.25f;
        colors.pressedColor     = bg * 0.75f;
        colors.disabledColor    = new Color(0.25f, 0.25f, 0.25f, 0.5f);
        btn.colors       = colors;
        btn.targetGraphic = img;

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var txt       = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text      = label;
        txt.fontSize  = 15f;
        txt.fontStyle = FontStyles.Bold;
        txt.color     = Color.white;
        txt.alignment = TextAlignmentOptions.Center;

        var txtRt              = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin        = Vector2.zero;
        txtRt.anchorMax        = Vector2.one;
        txtRt.sizeDelta        = Vector2.zero;
        txtRt.anchoredPosition = Vector2.zero;

        return btn;
    }

    private void SetCombatActionsActive(bool active)
    {
        if (_strikeBtn  != null) _strikeBtn.interactable  = active;
        if (_specialBtn != null) _specialBtn.interactable = active;
        if (_fleeBtn    != null) _fleeBtn.interactable    = active;
        if (_bribeBtn   != null) _bribeBtn.interactable   = active && currentCop.corruption >= 0.3f;
    }

    private void TryPlayerAction(CombatPlayerAction action)
    {
        if (!_inPlayerTurn || !inCombat) return;
        _inPlayerTurn = false;
        SetCombatActionsActive(false);
        StartCoroutine(DoPlayerAction(action));
    }

    private IEnumerator DoPlayerAction(CombatPlayerAction action)
    {
        int weaponDmg = PlayerStats.Instance.CurrentWeapon != null ? PlayerStats.Instance.CurrentWeapon.Damage : 5;
        int armor     = PlayerStats.Instance.CurrentTrench != null ? PlayerStats.Instance.CurrentTrench.ArmorValue : 0;
        string wName  = (PlayerStats.Instance.CurrentWeapon?.name ?? "").ToLower();

        switch (action)
        {
            case CombatPlayerAction.Strike:
            {
                int v   = Mathf.Max(1, weaponDmg / 4);
                int dmg = Mathf.Max(1, weaponDmg + UnityEngine.Random.Range(-v, v + 1));
                copHP   = Mathf.Max(0, copHP - dmg);
                AppendLog($"You strike for {dmg} damage.");
                break;
            }

            case CombatPlayerAction.Special:
            {
                if (wName.Contains("shotgun"))
                {
                    int h1 = Mathf.Max(1, Mathf.RoundToInt(weaponDmg * 0.80f));
                    int h2 = Mathf.Max(1, Mathf.RoundToInt(weaponDmg * 0.60f));
                    copHP  = Mathf.Max(0, copHP - h1 - h2);
                    AppendLog($"BUCKSHOT — {h1} + {h2} = {h1 + h2} damage!");
                    if (UnityEngine.Random.value < 0.40f)
                    {
                        copStunned = true;
                        AppendLog($"{currentCop.displayName} is STUNNED!");
                    }
                }
                else if (wName.Contains("handgun") || wName.Contains("gun") || wName.Contains("pistol"))
                {
                    int dmg = weaponDmg * 2;
                    copHP   = Mathf.Max(0, copHP - dmg);
                    AppendLog($"AIMED SHOT — {dmg} damage!");
                }
                else
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(weaponDmg * 0.60f));
                    copHP   = Mathf.Max(0, copHP - dmg);
                    AppendLog($"You improvise for {dmg} damage.");
                    if (UnityEngine.Random.value < 0.65f)
                    {
                        copStunned = true;
                        AppendLog($"{currentCop.displayName} is WINDED!");
                    }
                }
                break;
            }

            case CombatPlayerAction.Flee:
            {
                float chance = Mathf.Clamp01(0.35f - currentCop.violence * 0.20f);
                if (UnityEngine.Random.value < chance)
                {
                    AppendLog("You break away and escape!");
                    UpdateCombatUI();
                    yield return new WaitForSeconds(0.8f);
                    HandleCombatEscape();
                    yield break;
                }
                int freeHit = Mathf.Max(1, Mathf.RoundToInt(copDamage * 1.5f) - Mathf.RoundToInt(armor * 0.5f));
                playerHP    = Mathf.Max(0, playerHP - freeHit);
                AppendLog($"Escape failed! {currentCop.displayName} hits you for {freeHit}.");
                UpdateCombatUI();
                if (playerHP <= 0) { yield return new WaitForSeconds(0.6f); HandleCombatLoss(); yield break; }
                break;
            }

            case CombatPlayerAction.Bribe:
            {
                int offer = Mathf.Max(50, Mathf.RoundToInt(askAmount * 0.5f));
                offer     = Mathf.Min(offer, SafeGetCash());
                if (offer <= 0) { AppendLog("You're broke — nothing to offer."); break; }

                SpendPlayerCash?.Invoke(offer);
                if (UnityEngine.Random.value < Mathf.Clamp01(currentCop.corruption * 1.4f))
                {
                    AppendLog($"You slip ${offer:N0} to {currentCop.displayName}. They back off.");
                    UpdateCombatUI();
                    yield return new WaitForSeconds(0.8f);
                    HandleCombatEscape();
                    yield break;
                }
                copEmpoweredTurns += 2;
                AppendLog($"${offer:N0} rejected! {currentCop.displayName} is ENRAGED (+50% dmg).");
                break;
            }
        }

        UpdateCombatUI();
        yield return new WaitForSeconds(0.5f);

        if (copHP <= 0) { HandleCombatWin(); yield break; }

        yield return StartCoroutine(DoCopTurn());
    }

    private IEnumerator DoCopTurn()
    {
        AppendLog($"— {currentCop.displayName}'s turn —");
        yield return new WaitForSeconds(0.7f);

        if (copStunned)
        {
            AppendLog($"{currentCop.displayName} is stunned — skips turn.");
            copStunned = false;
            yield return new WaitForSeconds(0.5f);
            StartPlayerTurn();
            yield break;
        }

        int   armor   = PlayerStats.Instance.CurrentTrench != null ? PlayerStats.Instance.CurrentTrench.ArmorValue : 0;
        float empMult = copEmpoweredTurns > 0 ? 1.5f : 1.0f;
        if (copEmpoweredTurns  > 0) copEmpoweredTurns--;
        if (copPowerCooldown   > 0) copPowerCooldown--;

        switch (PickCopAction())
        {
            case CopCombatAction.Strike:
            {
                int v   = Mathf.Max(1, copDamage / 4);
                int dmg = Mathf.Max(1, Mathf.RoundToInt((copDamage + UnityEngine.Random.Range(-v, v + 1)) * empMult) - Mathf.RoundToInt(armor * 0.5f));
                playerHP = Mathf.Max(0, playerHP - dmg);
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesAttack, "Take that!")}\"";
                AppendLog($"{currentCop.displayName} strikes for {dmg} damage.");
                break;
            }
            case CopCombatAction.PowerStrike:
            {
                copPowerCooldown = 3;
                int dmg = Mathf.Max(1, Mathf.RoundToInt(copDamage * 2f * empMult) - Mathf.RoundToInt(armor * 0.5f));
                playerHP = Mathf.Max(0, playerHP - dmg);
                copDialogueText.text = $"{currentCop.displayName}: \"DOWN!\"";
                AppendLog($"POWER STRIKE — {dmg} damage!");
                break;
            }
            case CopCombatAction.Shakedown:
            {
                int taken = Mathf.RoundToInt(SafeGetCash() * 0.20f);
                if (taken > 0) { SpendPlayerCash?.Invoke(taken); AppendLog($"{currentCop.displayName} SHAKES YOU DOWN — ${taken:N0} taken!"); }
                else AppendLog($"{currentCop.displayName} tries to shake you down... you're broke.");
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesDemandBribe, "Pay up.")}\"";
                break;
            }
            case CopCombatAction.CallBackup:
            {
                copEmpoweredTurns += 2;
                AppendLog($"{currentCop.displayName} calls for backup! (+50% dmg, 2 turns)");
                copDialogueText.text = $"{currentCop.displayName}: \"All units, need assistance!\"";
                break;
            }
            case CopCombatAction.StandDown:
            {
                AppendLog($"{currentCop.displayName} hesitates and lowers their guard.");
                copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesWarn, "...Just walk away.")}\"";
                break;
            }
        }

        UpdateCombatUI();
        yield return new WaitForSeconds(0.5f);

        if (playerHP <= 0) { HandleCombatLoss(); yield break; }

        StartPlayerTurn();
    }

    private CopCombatAction PickCopAction()
    {
        var opts = new List<(CopCombatAction a, float w)> { (CopCombatAction.Strike, 1.0f) };

        if (currentCop.violence > 0.6f && copPowerCooldown <= 0)
            opts.Add((CopCombatAction.PowerStrike, currentCop.violence * 0.5f));

        if (currentCop.corruption > 0.5f && currentCop.greed > 0.6f && SafeGetCash() > 100)
            opts.Add((CopCombatAction.Shakedown, currentCop.corruption * 0.4f));

        if (currentCop.diligence > 0.7f)
            opts.Add((CopCombatAction.CallBackup, currentCop.diligence * 0.25f));

        if (currentCop.wFriendly > 0.4f && activeStance == CopStance.Friendly)
            opts.Add((CopCombatAction.StandDown, 0.15f));

        float total = 0f;
        foreach (var (_, w) in opts) total += w;
        float roll = (float)rng.NextDouble() * total;
        float acc  = 0f;
        foreach (var (a, w) in opts) { acc += w; if (roll < acc) return a; }
        return CopCombatAction.Strike;
    }

    private void StartPlayerTurn()
    {
        _inPlayerTurn = true;
        SetCombatActionsActive(true);
        AppendLog("Your turn.");
    }

    private void AppendLog(string line)
    {
        _combatLog.Add(line);
        while (_combatLog.Count > CombatLogMaxLines) _combatLog.RemoveAt(0);
        if (combatLogText != null) combatLogText.text = string.Join("\n", _combatLog);
    }

    private void HandleCombatWin()
    {
        inCombat = false;
        AppendLog($"Victory! {currentCop.displayName} is down.");
        copDialogueText.text = $"{currentCop.displayName} is down! You got away.";
        UpdateCombatUI();

        var heatManager = FindObjectOfType<HeatManager>();
        float halfMax = heatManager != null ? heatManager.MaxHeat * 0.5f : 50f;
        PlayerStats.Instance.CurrentHeat = halfMax;

        EndEncounter(success: true);
    }

    private void HandleCombatLoss()
    {
        inCombat = false;
        float lossPct  = 0.25f + currentSeed.contrabandRiskLevel * 0.10f;
        int   cashLoss = Mathf.RoundToInt(PlayerStats.Instance.PlayerWallet * lossPct);
        string combatDrugList = BuildDrugConfiscationList();
        SpendPlayerCash?.Invoke(cashLoss);
        int pct = Mathf.RoundToInt(lossPct * 100f);
        AppendLog($"Beaten. Lost ${cashLoss:N0} ({pct}%).");
        if (combatDrugList != null) AppendLog($"Seized: {combatDrugList}");
        copDialogueText.text = $"{currentCop.displayName}: \"{RandomLine(currentCop.linesArrest, "Should've cooperated.")}\"" +
            FormatLossSummary(cashLoss, $"taken ({pct}%)", combatDrugList);
        UpdateCombatUI();

        OnPlayerArrested?.Invoke();
        EndEncounter(success: false);
    }

    private void HandleCombatEscape()
    {
        inCombat = false;
        var heatManager = FindObjectOfType<HeatManager>();
        float halfMax = heatManager != null ? heatManager.MaxHeat * 0.5f : 50f;
        PlayerStats.Instance.CurrentHeat = halfMax;
        EndEncounter(success: true);
    }

    private void UpdateCombatUI()
    {
        if (playerHPBar  != null) { playerHPBar.maxValue  = playerMaxHP; playerHPBar.value  = playerHP; }
        if (copHPBar     != null) { copHPBar.maxValue     = copMaxHP;    copHPBar.value     = copHP;    }
        if (playerHPText != null) playerHPText.text = $"HP: {playerHP}/{playerMaxHP}";
        if (copHPText    != null) copHPText.text    = $"HP: {copHP}/{copMaxHP}";
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

    private string BuildDrugConfiscationList()
    {
        var drugs = new List<string>();
        foreach (var item in PlayerStats.Instance.inventory)
        {
            if (item.Type == ItemType.Drug && item.Amount > 0)
                drugs.Add($"{item.Name} x{item.Amount}");
        }
        return drugs.Count > 0 ? string.Join(", ", drugs) : null;
    }

    private string FormatLossSummary(int cashLost, string cashLabel, string drugList)
    {
        var parts = new List<string>();
        if (cashLost > 0)
            parts.Add($"${cashLost:N0} {cashLabel}");
        if (drugList != null)
            parts.Add($"Drugs seized: {drugList}");
        if (parts.Count == 0) return "";
        return "\n<color=#FF4444>" + string.Join("\n", parts) + "</color>";
    }

    private void HandleArrest()
    {
        PlayerStats.Instance.TimesCaughtByCops++;

        // Confiscate all drugs
        PlayerStats.Instance.inventory.RemoveAll(i => i.Type == ItemType.Drug && i.Amount > 0);
        PlayerStats.Instance.NotifyInventoryChanged();
    }

    private void EndEncounter(bool success)
    {
        if (runCooldownRoutine != null)
        {
            StopCoroutine(runCooldownRoutine);
            runCooldownRoutine = null;
            if (cachedRunLabel != null) cachedRunLabel.text = "Try to Run";
        }
        runButton.interactable         = false;
        attackButton.interactable      = false;
        bribeButton.interactable       = false;
        bribePayButton.interactable    = false;
        bribeHaggleButton.interactable = false;
        bribeRefuseButton.interactable = false;
        SetCombatActionsActive(false);

        StartCoroutine(FadeAndExit(success));
    }

    private IEnumerator FadeAndExit(bool success)
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

        if (success)
            OnEscaped?.Invoke();
        else
            OnEncounterResolved?.Invoke();
    }

    private string RandomLine(string[] pool, string fallback, string avoidLine = null)
    {
        if (pool != null && pool.Length > 0)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int i = UnityEngine.Random.Range(0, pool.Length);
                if (string.IsNullOrWhiteSpace(pool[i])) continue;
                if (avoidLine != null && pool[i] == avoidLine && pool.Length > 1) continue;
                return pool[i];
            }
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
