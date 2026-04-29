using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TravelManager : MonoBehaviour
{
    [Header("Travel Settings")]
    [SerializeField] private int travelUnlockThreshold = 20000;
    [SerializeField] private int travelCost = 500;
    [SerializeField] private List<City> allCities;
    [Tooltip("Hours of in-game time consumed per trip.")]
    [SerializeField] private int travelTimeHours = 6;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 1f;

    [Header("UI References")]
    [SerializeField] private GameObject travelUIParent;
    [SerializeField] private TMP_Dropdown cityDropdown;
    [SerializeField] private Button travelButton;
    [Tooltip("Optional text showing the travel fare.")]
    [SerializeField] private TMP_Text travelCostText;

    [Header("City Preview")]
    [SerializeField] private GameObject cityPreviewPanel;
    [SerializeField] private TMP_Text previewCityName;
    [SerializeField] private TMP_Text previewPopulation;
    [SerializeField] private TMP_Text previewCOL;
    [SerializeField] private TMP_Text previewFavDrug;
    [SerializeField] private float previewHoldSeconds = 3f;
    [SerializeField] private float previewFadeSeconds = 0.5f;

    private CanvasGroup previewCanvasGroup;
    private Coroutine previewFadeCoroutine;
    private bool isTraveling;
    private bool dropdownWasExpanded;

    private void Start()
    {
        if (travelUIParent != null)
            travelUIParent.SetActive(true);
        travelButton.onClick.AddListener(OnTravelButtonClicked);
        PopulateCityDropdown();
        CheckTravelStatus();
        cityDropdown.onValueChanged.AddListener(OnCityDropdownChanged);
        if (cityPreviewPanel != null)
        {
            previewCanvasGroup = cityPreviewPanel.GetComponent<CanvasGroup>();
            if (previewCanvasGroup == null)
                previewCanvasGroup = cityPreviewPanel.AddComponent<CanvasGroup>();
            cityPreviewPanel.SetActive(false);
        }
        ConfigurePreviewText();
    }

    private void OnTravelButtonClicked()
    {
        if (isTraveling) return;
        StartCoroutine(TravelSequence());
    }

    // Public method to be called whenever the player's wallet changes
    public void CheckTravelStatus()
    {
        if (travelUIParent == null) return;

        // Update fare display
        if (travelCostText != null)
            travelCostText.text = $"Fare: ${travelCost:N0}";

        // Grey out the button if they can't cover the fare
        if (travelButton != null)
            travelButton.interactable = PlayerStats.Instance != null
                && PlayerStats.Instance.PlayerWallet >= travelCost
                && !isTraveling;
    }

    private void PopulateCityDropdown()
    {
        cityDropdown.ClearOptions();

        List<string> options = new List<string>();
        foreach (var city in allCities)
        {
            if (PlayerStats.Instance != null && city == PlayerStats.Instance.CurrentCity)
                continue;
            options.Add($"{city.Name}  —  ${travelCost:N0}");
        }
        cityDropdown.AddOptions(options);
    }

    private IEnumerator TravelSequence()
    {
        // Parse city name from dropdown (format: "CityName  —  $X,XXX")
        string raw = cityDropdown.options[cityDropdown.value].text;
        string selectedCityName = raw.Contains("\u2014")
            ? raw.Substring(0, raw.IndexOf("\u2014")).Trim()
            : raw.Trim();
        City destinationCity = allCities.FirstOrDefault(city => city.Name == selectedCityName);

        if (destinationCity == null || string.IsNullOrEmpty(destinationCity.SceneName))
        {
            Debug.LogError($"Could not find a city or scene name for '{selectedCityName}'!");
            yield break;
        }

        if (PlayerStats.Instance.PlayerWallet < travelCost)
        {
            Debug.LogWarning("[TravelManager] Not enough cash for travel.");
            yield break;
        }

        isTraveling = true;
        travelButton.interactable = false;

        // Deduct fare
        PlayerStats.Instance.PlayerWallet -= travelCost;

        // Advance in-game time
        var gameTime = GameTime.Instance ?? FindObjectOfType<GameTime>();
        if (gameTime != null)
            gameTime.AddHours(travelTimeHours);

        // Update city
        PlayerStats.Instance.CurrentCity = destinationCity;
        PlayerStats.Instance.CitiesVisited++;

        // Auto-save before loading
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.SaveGame();

        if (FadeController.Instance != null)
            FadeController.Instance.FadeOut(fadeDuration);
        yield return new WaitForSeconds(fadeDuration + 0.1f);

        SceneManager.LoadScene(destinationCity.SceneName);
        isTraveling = false;
    }

    private void ConfigurePreviewText()
    {
        ConfigureAutoSize(previewCityName, 10f, 22f, FontStyles.Bold);
        ConfigureAutoSize(previewPopulation, 8f, 16f, FontStyles.Normal);
        ConfigureAutoSize(previewCOL, 8f, 16f, FontStyles.Normal);
        ConfigureAutoSize(previewFavDrug, 8f, 16f, FontStyles.Normal);
    }

    private static void ConfigureAutoSize(TMP_Text label, float min, float max, FontStyles style)
    {
        if (label == null) return;
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
        label.fontStyle = style;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void Update()
    {
        bool isExpanded = cityDropdown.IsExpanded;
        if (dropdownWasExpanded && !isExpanded)
            ShowCityPreview(cityDropdown.value);
        dropdownWasExpanded = isExpanded;
    }

    private void OnCityDropdownChanged(int index) => ShowCityPreview(index);

    private void ShowCityPreview(int index)
    {
        if (cityPreviewPanel == null) return;

        if (cityDropdown.options.Count == 0)
        {
            cityPreviewPanel.SetActive(false);
            return;
        }

        string raw = cityDropdown.options[index].text;
        string cityName = raw.Contains("\u2014")
            ? raw.Substring(0, raw.IndexOf("\u2014")).Trim()
            : raw.Trim();
        City city = allCities.FirstOrDefault(c => c.Name == cityName);

        if (city == null)
        {
            cityPreviewPanel.SetActive(false);
            return;
        }

        if (previewCityName != null)
            previewCityName.text = city.Name;

        if (previewPopulation != null)
            previewPopulation.text = $"Pop: {city.Population:N0}";

        if (previewCOL != null)
        {
            string colLabel = city.costOfLiving < 0.75f ? "Low"
                : city.costOfLiving < 1.25f ? "Moderate"
                : city.costOfLiving < 1.75f ? "High"
                : "Very High";
            previewCOL.text = $"Cost of Living: {colLabel}";
        }

        if (previewFavDrug != null)
        {
            string drugName = city.FavoriteDrug != null ? city.FavoriteDrug.Name : "—";
            previewFavDrug.text = $"Hot Drug: {drugName} ({city.favoriteDrugDemandMultiplier:F1}x demand)";
        }

        if (previewCanvasGroup != null)
        {
            cityPreviewPanel.SetActive(true);
            previewCanvasGroup.alpha = 1f;
            if (previewFadeCoroutine != null) StopCoroutine(previewFadeCoroutine);
            previewFadeCoroutine = StartCoroutine(FadeOutPreview());
        }
    }

    private IEnumerator FadeOutPreview()
    {
        yield return new WaitForSeconds(previewHoldSeconds);

        float elapsed = 0f;
        while (elapsed < previewFadeSeconds)
        {
            elapsed += Time.deltaTime;
            previewCanvasGroup.alpha = 1f - (elapsed / previewFadeSeconds);
            yield return null;
        }

        cityPreviewPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnWalletChanged += HandleWalletChanged;
    }

    private void OnDisable()
    {
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnWalletChanged -= HandleWalletChanged;
    }

    private void OnDestroy()
    {
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnWalletChanged -= HandleWalletChanged;
        cityDropdown.onValueChanged.RemoveListener(OnCityDropdownChanged);
    }

    private void HandleWalletChanged(int _)
    {
        // Guard against being called during teardown
        if (!this || gameObject == null || !isActiveAndEnabled) return;
        CheckTravelStatus();
    }
}
