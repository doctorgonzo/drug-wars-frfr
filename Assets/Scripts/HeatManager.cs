using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HeatManager : MonoBehaviour
{
    [Header("Heat Settings")]
    [Tooltip("The heat level that will trigger a cop encounter.")]
    [SerializeField] private int maxHeat = 100;
    [Tooltip("The name of the scene to load when max heat is reached.")]
    [SerializeField] private string copSceneName = "CopEncounter";

    [Header("Heat Decay")]
    [Tooltip("The time in seconds after a transaction before heat starts to decay.")]
    [SerializeField] private float decayCooldown = 10f;
    [Tooltip("The amount of heat that decays per second after the cooldown.")]
    [SerializeField] private float heatDecayPerSecond = 1f;

    [Header("UI References")]
    [Tooltip("The UI slider that visually represents the current heat level.")]
    [SerializeField] private Slider heatSlider;
    [Tooltip("The TextMeshPro text element that displays the heat percentage.")]
    [SerializeField] private TMP_Text heatText;

    [Header("Decay Visual Feedback")]
    [Tooltip("Optional Image on the slider fill to tint during decay states.")]
    [SerializeField] private Image heatFillImage;
    [Tooltip("Optional status text showing cooldown/decay state.")]
    [SerializeField] private TMP_Text heatStatusText;
    [Tooltip("Optional text showing current contraband risk level.")]
    [SerializeField] private TMP_Text riskLevelText;
    [SerializeField] private Color normalColor = new Color(1f, 0.3f, 0.1f, 1f);
    [SerializeField] private Color cooldownColor = new Color(1f, 0.6f, 0f, 1f);
    [SerializeField] private Color decayingColor = new Color(0.3f, 0.8f, 1f, 1f);
    [SerializeField] private float pulseSpeed = 3f;

    private float cooldownTimer;
    private enum HeatState { Idle, Cooldown, Decaying }
    private HeatState currentState = HeatState.Idle;
    private Coroutine decayRoutine;

    // Fill image idle colors — shift with risk tier of what you're carrying
    private static readonly Color fillColor0 = new Color(1f, 0.3f, 0.1f, 1f);     // safe — orange-red
    private static readonly Color fillColor1 = new Color(1f, 0.55f, 0f, 1f);      // medium — amber
    private static readonly Color fillColor2 = new Color(0.9f, 0.05f, 0.05f, 1f); // hard — crimson

    // Risk label text colors — separate from fill (CLEAN should look neutral, not alarming)
    private static readonly Color labelColorClean  = new Color(0.55f, 0.85f, 0.55f, 1f); // soft green
    private static readonly Color labelColorMed    = new Color(1f, 0.75f, 0.1f, 1f);     // amber
    private static readonly Color labelColorHigh   = new Color(0.95f, 0.15f, 0.15f, 1f); // red

    private void Start()
    {
        if (heatSlider != null)
            heatSlider.maxValue = maxHeat;
        ConfigureTextAutoSize();
        UpdateHeatDisplay();
        UpdateRiskDisplay();
        cooldownTimer = decayCooldown;
        EnsureDecayRunning();
    }

    private void ConfigureTextAutoSize()
    {
        SetAutoSize(heatText, 8f, 20f);
        SetAutoSize(heatStatusText, 7f, 16f);
        SetAutoSize(riskLevelText, 7f, 16f);
    }

    private static void SetAutoSize(TMP_Text label, float min, float max)
    {
        if (label == null) return;
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void OnEnable()
    {
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged -= OnInventoryChanged;
    }

    private void OnInventoryChanged()
    {
        UpdateRiskDisplay();
        normalColor = GetFillColor(GetInventoryRiskLevel());
        if (currentState == HeatState.Idle && heatFillImage != null)
            heatFillImage.color = normalColor;
    }

    private int GetInventoryRiskLevel()
    {
        var inv = PlayerStats.Instance?.inventory;
        if (inv == null) return 0;
        int max = 0;
        foreach (var item in inv)
        {
            if (item.Type == ItemType.Drug && item.Amount > 0)
                max = Mathf.Max(max, item.RiskTier);
        }
        return max;
    }

    private static Color GetFillColor(int level) => level switch
    {
        1 => fillColor1,
        2 => fillColor2,
        _ => fillColor0
    };

    private void UpdateRiskDisplay()
    {
        if (riskLevelText == null) return;
        int level = GetInventoryRiskLevel();
        riskLevelText.text = level switch
        {
            0 => "CLEAN",
            1 => "MED RISK",
            2 => "HIGH RISK",
            _ => "CLEAN"
        };
        riskLevelText.color = level switch
        {
            1 => labelColorMed,
            2 => labelColorHigh,
            _ => labelColorClean
        };
    }

    private IEnumerator DecayLoop()
    {
        while (PlayerStats.Instance.CurrentHeat > 0)
        {
            if (cooldownTimer > 0)
            {
                cooldownTimer -= Time.deltaTime;
                SetHeatState(HeatState.Cooldown);
            }
            else
            {
                PlayerStats.Instance.CurrentHeat -= heatDecayPerSecond * Time.deltaTime;
                PlayerStats.Instance.CurrentHeat = Mathf.Max(0, PlayerStats.Instance.CurrentHeat);
                UpdateHeatDisplay();
                SetHeatState(HeatState.Decaying);
            }

            UpdateFillVisual();
            yield return null;
        }

        // Heat reached zero — reset visuals and stop
        SetHeatState(HeatState.Idle);
        UpdateFillVisual();
        UpdateHeatDisplay();
        decayRoutine = null;
    }

    private void EnsureDecayRunning()
    {
        if (decayRoutine == null && PlayerStats.Instance.CurrentHeat > 0)
            decayRoutine = StartCoroutine(DecayLoop());
    }

    private void SetHeatState(HeatState state)
    {
        if (currentState == state) return;
        currentState = state;

        if (heatStatusText != null)
        {
            switch (state)
            {
                case HeatState.Cooldown:
                    heatStatusText.text = "COOLING DOWN...";
                    heatStatusText.gameObject.SetActive(true);
                    break;
                case HeatState.Decaying:
                    heatStatusText.text = "DECAYING";
                    heatStatusText.gameObject.SetActive(true);
                    break;
                default:
                    heatStatusText.gameObject.SetActive(false);
                    break;
            }
        }
    }

    private void UpdateFillVisual()
    {
        if (heatFillImage == null) return;

        switch (currentState)
        {
            case HeatState.Cooldown:
                // Pulse between normal and cooldown color
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                heatFillImage.color = Color.Lerp(normalColor, cooldownColor, t);
                break;
            case HeatState.Decaying:
                // Pulse alpha to indicate active decay
                float alpha = Mathf.Lerp(0.5f, 1f, (Mathf.Sin(Time.time * pulseSpeed * 1.5f) + 1f) * 0.5f);
                heatFillImage.color = new Color(decayingColor.r, decayingColor.g, decayingColor.b, alpha);
                break;
            default:
                heatFillImage.color = normalColor;
                break;
        }
    }

    public void AddHeat(int heatAmount)
    {
        if (heatAmount <= 0) return;

        // Any time a transaction happens, reset the decay cooldown
        cooldownTimer = decayCooldown;

        PlayerStats.Instance.CurrentHeat += heatAmount;
        EnsureDecayRunning();
        PlayerStats.Instance.CurrentHeat = Mathf.Clamp(PlayerStats.Instance.CurrentHeat, 0, maxHeat);

        UpdateHeatDisplay();
        CheckForCops();
    }

    public void UpdateHeatDisplay()
    {
        if (heatSlider != null)
        {
            heatSlider.value = PlayerStats.Instance.CurrentHeat;
        }
        if (heatText != null)
        {
            float heatPercentage = PlayerStats.Instance.CurrentHeat / maxHeat;
            // Display as an integer percentage, e.g., "Heat: 75%"
            heatText.text = $"Heat: {heatPercentage:P0}";
        }
    }

    private void CheckForCops()
    {
        if (PlayerStats.Instance.CurrentHeat >= maxHeat)
        {
            // Reset heat immediately
            PlayerStats.Instance.CurrentHeat = 0;
            CopEncounterData.ReturnSceneName = SceneManager.GetActiveScene().name;
            // Build the encounter seed from your current PlayerStats
            int riskLevel = GetInventoryRiskLevel();
            var seed = new DrugWars.NPC.CopEncounterSeed
            {
                priorCopEncounters = PlayerStats.Instance.TimesCaughtByCops,
                playerCash = PlayerStats.Instance.PlayerWallet,
                playerHasContraband = PlayerStats.Instance.HasContraband,
                playerLevel = PlayerStats.Instance.Level,
                heatAtTrigger = maxHeat,
                contrabandRiskLevel = riskLevel
            };

            // Handoff to the cop scene
            CopEncounterData.Seed = seed;

            // Load the encounter scene
            SceneManager.LoadScene(copSceneName);
        }
    }

}
