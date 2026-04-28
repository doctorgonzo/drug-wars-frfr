using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunSummaryUI : MonoBehaviour
{
    [Header("Outcome")]
    [SerializeField] private bool isVictory;
    [SerializeField] private TMP_Text headlineText;
    [SerializeField] private TMP_Text subheadText;

    [Header("Stats")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text netWorthText;
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private TMP_Text debtText;
    [SerializeField] private TMP_Text daysText;
    [SerializeField] private TMP_Text copsText;
    [SerializeField] private TMP_Text citiesText;

    [Header("High Score")]
    [SerializeField] private TMP_Text highScoreText;

    [Header("Colors")]
    [SerializeField] private Color victoryColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color defeatColor = new Color(0.9f, 0.2f, 0.2f);

    [Header("Buttons")]
    [SerializeField] private Button playAgainButton;
    [SerializeField] private string mainMenuSceneName = "Start";

    private const string WinHighScoreKey = "HighScore_NetWorth";
    private const string WinHighScoreNameKey = "HighScore_Name";

    private void Start()
    {
        var ps = PlayerStats.Instance;
        var gt = FindObjectOfType<GameTime>();

        if (headlineText != null)
        {
            headlineText.text = isVictory ? "DEBT CLEARED!" : "TIME'S UP";
            headlineText.color = isVictory ? victoryColor : defeatColor;
        }

        if (ps == null)
        {
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(PlayAgain);
            return;
        }

        int netWorth = ps.NetWorth;
        int day = gt != null ? gt.Day : 0;

        if (subheadText != null)
            subheadText.text = isVictory
                ? $"You paid off the shark in {day} days."
                : $"The shark came to collect on Day {day}.";

        if (playerNameText != null) playerNameText.text = ps.PlayerName;
        if (netWorthText != null)   netWorthText.text   = $"Net Worth:  ${netWorth:N0}";
        if (walletText != null)     walletText.text     = $"Cash:  ${ps.PlayerWallet:N0}";
        if (debtText != null)       debtText.text       = ps.IsDebtPaidOff ? "Debt:  CLEARED" : $"Debt:  ${ps.Debt:N0} remaining";
        if (daysText != null)       daysText.text       = $"Days:  {day} / {ps.DayLimit}";

        if (copsText != null)
            copsText.text = $"Cops:  {ps.TimesCaughtByCops} busted / {ps.TotalCopEncounters} encountered";

        if (citiesText != null)
            citiesText.text = $"Cities visited:  {ps.CitiesVisited}";

        if (isVictory) UpdateHighScore(netWorth, ps.PlayerName);

        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(PlayAgain);
    }

    private void UpdateHighScore(int netWorth, string playerName)
    {
        int prev = PlayerPrefs.GetInt(WinHighScoreKey, 0);
        if (netWorth > prev)
        {
            PlayerPrefs.SetInt(WinHighScoreKey, netWorth);
            PlayerPrefs.SetString(WinHighScoreNameKey, playerName);
            PlayerPrefs.Save();

            if (highScoreText != null)
                highScoreText.text = $"NEW HIGH SCORE!  ${netWorth:N0}";
        }
        else
        {
            string prevName = PlayerPrefs.GetString(WinHighScoreNameKey, "—");
            if (highScoreText != null)
                highScoreText.text = $"Best run:  {prevName}  ${prev:N0}";
        }
    }

    private void PlayAgain()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
