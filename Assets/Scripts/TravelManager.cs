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

    private bool isTraveling;

    private void Start()
    {
        if (travelUIParent != null)
            travelUIParent.SetActive(true);
        travelButton.onClick.AddListener(OnTravelButtonClicked);
        PopulateCityDropdown();
        CheckTravelStatus();
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
        var gameTime = FindObjectOfType<GameTime>();
        if (gameTime != null)
            gameTime.AddHours(travelTimeHours);

        // Update city
        PlayerStats.Instance.CurrentCity = destinationCity;

        // Auto-save before loading
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.SaveGame();

        // Fade out
        if (FadeController.Instance != null)
            FadeController.Instance.FadeOut(fadeDuration);
        yield return new WaitForSeconds(fadeDuration + 0.1f);

        // Load the destination scene
        SceneManager.LoadScene(destinationCity.SceneName);

        // Fade back in (runs in the new scene via DontDestroyOnLoad FadeController)
        if (FadeController.Instance != null)
            FadeController.Instance.FadeIn(fadeDuration);

        isTraveling = false;
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
    }

    private void HandleWalletChanged(int _)
    {
        // Guard against being called during teardown
        if (!this || gameObject == null || !isActiveAndEnabled) return;
        CheckTravelStatus();
    }
}
