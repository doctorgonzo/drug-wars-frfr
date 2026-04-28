using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartUIHandler : MonoBehaviour
{
    [SerializeField] GameObject optionsPanel;
    [SerializeField] GameObject[] mainMenuButtons;
    [SerializeField] TMP_Dropdown graphicsDropdown;
    [Tooltip("Optional: a 'Continue' button that loads the last save. Hide it if no save exists.")]
    [SerializeField] Button continueButton;
    private int startingGraphicsQuality = 1; //medium quality by default
    private int initialQuality;
    private int currentGraphicsQuality;
    private bool graphicsChanged = false;

    private void Start()
    {
        initialQuality = QualitySettings.GetQualityLevel();
        graphicsDropdown.value = initialQuality;

        // Show or hide the Continue button based on whether a save file exists
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(SaveLoadHelper.SaveExists());
            continueButton.onClick.AddListener(HandleContinue);
        }
    }

    public void HandleStart()
    {
        SceneManager.LoadScene("CharCreation");
    }

    public void HandleContinue()
    {
        if (GameSessionManager.Instance != null && GameSessionManager.Instance.LoadGame())
        {
            var city = PlayerStats.Instance.CurrentCity;
            if (city != null && !string.IsNullOrEmpty(city.SceneName))
                SceneManager.LoadScene(city.SceneName);
            else
                Debug.LogError("[StartUI] Loaded save but current city has no scene name.");
        }
        else
        {
            Debug.LogWarning("[StartUI] No save file found or load failed.");
        }
    }
    
    public void HandleOptions()
    {
        foreach (var item in mainMenuButtons)
        {
            item.SetActive(false);
        }
        optionsPanel.SetActive(true);
        graphicsChanged = false;
    }

    public void HandleQuit()
    {
        Application.Quit();
    }

    public void HandleBack()
    {
        if (graphicsChanged)
            QualitySettings.SetQualityLevel(initialQuality);
        optionsPanel.SetActive(false);
        foreach (var go in mainMenuButtons) go.SetActive(true);
    }

    public void HandleSave()
    {
        PlayerPrefs.SetInt("qualityLevel", QualitySettings.GetQualityLevel());
        PlayerPrefs.Save();
        graphicsChanged = false;
        optionsPanel.SetActive(false);
        foreach (var go in mainMenuButtons) go.SetActive(true);
    }

    public void HandleGraphicsDropdownChanged()
    {
        graphicsChanged = true;
        QualitySettings.SetQualityLevel(graphicsDropdown.value);
    }
}
