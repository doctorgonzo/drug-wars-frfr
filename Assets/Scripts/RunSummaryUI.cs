using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunSummaryUI : MonoBehaviour
{
    [Header("Outcome")]
    [Tooltip("True for YouWin scene, false for GameOver scene.")]
    [SerializeField] private bool isVictory;
    [SerializeField] private TMP_Text headlineText;
    [SerializeField] private TMP_Text subheadText;

    [Header("Stats Block (single rich-text block; fill in scene)")]
    [SerializeField] private TMP_Text statsBlockText;

    [Header("Optional individual stat fields (legacy / fine-grained scene layouts)")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text netWorthText;
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private TMP_Text debtText;
    [SerializeField] private TMP_Text daysText;
    [SerializeField] private TMP_Text copsText;
    [SerializeField] private TMP_Text citiesText;
    [SerializeField] private TMP_Text highScoreText;
    [SerializeField] private Image playerPortrait;

    [Header("Leaderboard")]
    [Tooltip("Drop the LeaderboardUI in the scene here so we can inform it of new entries.")]
    [SerializeField] private LeaderboardUI leaderboardUI;
    [Tooltip("Optional: shown when the run cracks the leaderboard. e.g. '#3 ON THE BOARD!'")]
    [SerializeField] private TMP_Text leaderboardRankText;

    [Header("Colors")]
    [SerializeField] private Color victoryColor = new Color(0.2f, 0.9f, 0.3f);
    [SerializeField] private Color defeatColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color sectionHeaderColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color positiveValueColor = new Color(0.4f, 0.95f, 0.4f);
    [SerializeField] private Color negativeValueColor = new Color(0.95f, 0.35f, 0.35f);
    [SerializeField] private Color mutedValueColor = new Color(0.75f, 0.75f, 0.75f);

    [Header("Buttons")]
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "Start";
    [Tooltip("Scene the Play Again button loads. Leave blank to use mainMenuSceneName.")]
    [SerializeField] private string playAgainSceneName = "CharCreation";

    private void Start()
    {
        var ps = PlayerStats.Instance;
        var gt = FindObjectOfType<GameTime>();
        int currentDay = gt != null ? gt.Day : 0;

        // Headline
        if (headlineText != null)
        {
            headlineText.text = isVictory ? "DEBT CLEARED!" : "TIME'S UP";
            headlineText.color = isVictory ? victoryColor : defeatColor;
        }

        // Buttons (always wire so a missing PlayerStats doesn't soft-lock the scene)
        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(PlayAgain);
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);

        if (ps == null)
        {
            if (statsBlockText != null) statsBlockText.text = "<i>(No run data found.)</i>";
            return;
        }

        // Player portrait
        if (playerPortrait != null && ps.PlayerSprite != null)
            playerPortrait.sprite = ps.PlayerSprite;

        int dayDebtCleared = ps.DayDebtCleared > 0 ? ps.DayDebtCleared : currentDay;
        int daysUsed = isVictory ? dayDebtCleared : currentDay;
        int netWorth = ps.NetWorth;

        // Subhead
        if (subheadText != null)
        {
            subheadText.text = isVictory
                ? $"You paid off the shark in <b>{dayDebtCleared}</b> days."
                : $"The shark came to collect on Day <b>{currentDay}</b>.";
        }

        // Legacy individual fields
        if (playerNameText != null) playerNameText.text = ps.PlayerName;
        if (netWorthText != null)   netWorthText.text   = $"Net Worth:  ${netWorth:N0}";
        if (walletText != null)     walletText.text     = $"Cash:  ${ps.PlayerWallet:N0}";
        if (debtText != null)       debtText.text       = ps.IsDebtPaidOff ? "Debt:  CLEARED" : $"Debt:  ${ps.Debt:N0} remaining";
        if (daysText != null)       daysText.text       = $"Days:  {daysUsed} / {ps.DayLimit}";
        if (copsText != null)       copsText.text       = $"Cops:  {ps.TimesCaughtByCops} busted / {ps.TotalCopEncounters} encountered";
        if (citiesText != null)     citiesText.text     = $"Cities visited:  {ps.UniqueCitiesVisited}";

        // Build the rich-text stats block
        if (statsBlockText != null)
            statsBlockText.text = BuildStatsBlock(ps, daysUsed);

        // Leaderboard submission (both win and loss go on the board, ranked by days; loss uses currentDay)
        SubmitLeaderboardEntry(ps, daysUsed, netWorth);

        // Legacy single high-score line — preserve so existing scenes don't show stale text
        if (highScoreText != null)
            UpdateLegacyHighScoreLine(ps.PlayerName, daysUsed);
    }

    private string BuildStatsBlock(PlayerStats ps, int daysUsed)
    {
        long totalRevenue = ps.TotalSalesRevenue;
        long totalDrugSpend = ps.TotalDrugSpend;
        long drugProfit = totalRevenue - totalDrugSpend;
        long totalLostToCops = ps.TotalConfiscatedCash + ps.TotalFinesPaid + ps.TotalCombatCashLoss;

        var sb = new System.Text.StringBuilder();

        AppendSection(sb, "TIMELINE");
        AppendRow(sb, "Days survived", $"{daysUsed} / {ps.DayLimit}");
        if (ps.DayDebtCleared > 0)
            AppendRow(sb, "Day debt cleared", $"Day {ps.DayDebtCleared}", positiveValueColor);
        AppendRow(sb, "Cities visited", $"{ps.UniqueCitiesVisited} ({JoinCities(ps.VisitedCityNames)})");

        AppendSection(sb, "MONEY");
        AppendRow(sb, "Net worth", $"${ps.NetWorth:N0}", positiveValueColor);
        AppendRow(sb, "Cash on hand", $"${ps.PlayerWallet:N0}");
        AppendRow(sb, "Debt remaining", ps.IsDebtPaidOff ? "<color=#66FF66>CLEARED</color>" : $"${ps.Debt:N0}", ps.IsDebtPaidOff ? positiveValueColor : negativeValueColor);
        AppendRow(sb, "Drug sales revenue", $"${totalRevenue:N0}", positiveValueColor);
        AppendRow(sb, "Drug spending", $"${totalDrugSpend:N0}");
        AppendRow(sb, "Drug profit", $"${drugProfit:N0}", drugProfit >= 0 ? positiveValueColor : negativeValueColor);
        AppendRow(sb, "Equipment spent", $"${ps.TotalEquipmentSpend:N0}");
        AppendRow(sb, "Travel fares", $"${ps.TotalTravelSpend:N0}");
        AppendRow(sb, "Loan interest paid", $"${ps.TotalInterestPaid:N0}", negativeValueColor);
        AppendRow(sb, "Debt paid down", $"${ps.TotalDebtPaid:N0}", positiveValueColor);
        AppendRow(sb, "Borrowed from shark", $"${ps.TotalBorrowed:N0}");

        AppendSection(sb, "DRUGS");
        AppendRow(sb, "Total bought", $"{ps.TotalDrugsBought:N0} units");
        AppendRow(sb, "Total sold", $"{ps.TotalDrugsSold:N0} units");
        AppendRow(sb, "Biggest single sale", $"${ps.BiggestSingleSale:N0}", positiveValueColor);
        if (!string.IsNullOrEmpty(ps.FavoriteDrug))
            AppendRow(sb, "Drug of choice", $"{ps.FavoriteDrug} ({ps.FavoriteDrugQty} sold)");

        AppendSection(sb, "HEAT & COPS");
        AppendRow(sb, "Cop encounters", $"{ps.TotalCopEncounters}");
        AppendRow(sb, "Times arrested", $"{ps.TimesCaughtByCops}", ps.TimesCaughtByCops > 0 ? negativeValueColor : positiveValueColor);
        AppendRow(sb, "Successful escapes", $"{ps.TimesEscaped}", positiveValueColor);
        AppendRow(sb, "Successful bribes", $"{ps.TimesBribedSuccessfully}");
        AppendRow(sb, "Combat wins", $"{ps.CombatWins}", positiveValueColor);
        AppendRow(sb, "Combat losses", $"{ps.CombatLosses}", ps.CombatLosses > 0 ? negativeValueColor : mutedValueColor);
        AppendRow(sb, "Cash lost to cops", $"${totalLostToCops:N0}", negativeValueColor);
        AppendRow(sb, "  · confiscated", $"${ps.TotalConfiscatedCash:N0}", mutedValueColor);
        AppendRow(sb, "  · fines paid", $"${ps.TotalFinesPaid:N0}", mutedValueColor);
        AppendRow(sb, "  · combat losses", $"${ps.TotalCombatCashLoss:N0}", mutedValueColor);
        AppendRow(sb, "Bribes paid", $"${ps.TotalBribesPaid:N0}", mutedValueColor);
        AppendRow(sb, "Peak heat", $"{Mathf.RoundToInt(ps.PeakHeat)}");

        AppendSection(sb, "MISC");
        AppendRow(sb, "Total clicks", $"{ps.TotalClicks:N0}");

        return sb.ToString();
    }

    private void AppendSection(System.Text.StringBuilder sb, string title)
    {
        if (sb.Length > 0) sb.AppendLine();
        sb.Append("<size=120%><b><color=#")
          .Append(ColorUtility.ToHtmlStringRGB(sectionHeaderColor))
          .Append('>')
          .Append(title)
          .AppendLine("</color></b></size>");
    }

    private static void AppendRow(System.Text.StringBuilder sb, string label, string value)
    {
        sb.Append(label).Append(":  <b>").Append(value).AppendLine("</b>");
    }

    private static void AppendRow(System.Text.StringBuilder sb, string label, string value, Color valueColor)
    {
        sb.Append(label)
          .Append(":  <b><color=#")
          .Append(ColorUtility.ToHtmlStringRGB(valueColor))
          .Append('>')
          .Append(value)
          .AppendLine("</color></b>");
    }

    private static string JoinCities(IReadOnlyCollection<string> cities)
    {
        if (cities == null || cities.Count == 0) return "—";
        return string.Join(", ", cities.OrderBy(c => c));
    }

    private void SubmitLeaderboardEntry(PlayerStats ps, int daysUsed, int netWorth)
    {
        var entry = new Leaderboard.Entry
        {
            playerName = string.IsNullOrEmpty(ps.PlayerName) ? "—" : ps.PlayerName,
            days = daysUsed,
            netWorth = netWorth,
            isoDate = DateTime.UtcNow.ToString("o"),
            totalClicks = ps.TotalClicks,
            citiesVisited = ps.UniqueCitiesVisited,
            wasVictory = isVictory
        };

        int rank = Leaderboard.TryInsert(entry);

        if (leaderboardRankText != null)
        {
            if (rank >= 0)
            {
                leaderboardRankText.text = $"#{rank + 1} ON THE BOARD!";
                leaderboardRankText.color = victoryColor;
                leaderboardRankText.gameObject.SetActive(true);
            }
            else
            {
                leaderboardRankText.gameObject.SetActive(false);
            }
        }

        if (leaderboardUI != null)
            leaderboardUI.RefreshAndHighlight(entry);
    }

    private void UpdateLegacyHighScoreLine(string playerName, int daysUsed)
    {
        var sorted = Leaderboard.GetSorted();
        if (sorted.Count == 0)
        {
            highScoreText.text = "First on the board!";
            return;
        }
        var top = sorted[0];
        if (top.playerName == playerName && top.days == daysUsed)
            highScoreText.text = $"NEW RECORD!  {daysUsed} days";
        else
            highScoreText.text = $"Best run:  {top.playerName}  {top.days} days";
    }

    private void PlayAgain()
    {
        string scene = string.IsNullOrEmpty(playAgainSceneName) ? mainMenuSceneName : playAgainSceneName;
        SceneManager.LoadScene(scene);
    }

    private void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
