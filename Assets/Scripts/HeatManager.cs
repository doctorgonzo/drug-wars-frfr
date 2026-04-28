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
    [SerializeField] private Color normalColor = new Color(1f, 0.3f, 0.1f, 1f);
    [SerializeField] private Color cooldownColor = new Color(1f, 0.6f, 0f, 1f);
    [SerializeField] private Color decayingColor = new Color(0.3f, 0.8f, 1f, 1f);
    [SerializeField] private float pulseSpeed = 3f;

    private float cooldownTimer;
    private enum HeatState { Idle, Cooldown, Decaying }
    private HeatState currentState = HeatState.Idle;
    private Coroutine decayRoutine;

    private void Start()
    {
        if (heatSlider != null)
        {
            heatSlider.maxValue = maxHeat;
        }
        UpdateHeatDisplay();
        // Start the cooldown timer so heat doesn't decay immediately
        cooldownTimer = decayCooldown;
        EnsureDecayRunning();
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
            var seed = new DrugWars.NPC.CopEncounterSeed
            {
                priorCopEncounters = PlayerStats.Instance.TimesCaughtByCops,
                playerCash = PlayerStats.Instance.PlayerWallet,     // <- use your real wallet
                playerHasContraband = PlayerStats.Instance.HasContraband,    // <- the simple list check above
                playerLevel = PlayerStats.Instance.Level,
                heatAtTrigger = maxHeat
            };

            // Handoff to the cop scene
            CopEncounterData.Seed = seed;

            // Load the encounter scene
            SceneManager.LoadScene(copSceneName);
        }
    }

}
