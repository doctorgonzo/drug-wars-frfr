using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// End-of-run summary screen.
//
// Two ways this gets onto the YouWin / GameOver scenes:
//   1. Auto-spawn (zero Editor wiring): a RuntimeInitializeOnLoadMethod hooks SceneManager.sceneLoaded,
//      and when "YouWin" or "GameOver" loads, we instantiate a RunSummaryUI GameObject ourselves and
//      build the entire UI from code in Start().
//   2. Manual wiring (legacy / custom layouts): drop a RunSummaryUI on the scene and wire any of the
//      [SerializeField] fields. Whatever's wired is used; whatever isn't is built from code if needed.
//
// `isVictory` is derived from the scene name regardless of the Inspector value, so you don't have to
// remember to flip a checkbox per scene.
public class RunSummaryUI : MonoBehaviour
{
    [Header("Outcome (auto-derived from scene name)")]
    [Tooltip("True for YouWin, false for GameOver. Overridden at runtime by scene name match.")]
    [SerializeField] private bool isVictory;
    [SerializeField] private TMP_Text headlineText;
    [SerializeField] private TMP_Text subheadText;

    [Header("Stats Block")]
    [SerializeField] private TMP_Text statsBlockText;

    [Header("Optional individual stat fields (legacy)")]
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
    [SerializeField] private LeaderboardUI leaderboardUI;
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
    [SerializeField] private string mainMenuSceneName = "Startup";
    [SerializeField] private string playAgainSceneName = "CharCreation";

    private const string YouWinSceneName = "YouWin";
    private const string GameOverSceneName = "GameOver";

    // ---- Auto-spawn ----
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterAutoSpawn()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        var current = SceneManager.GetActiveScene();
        if (IsEndgameScene(current.name))
            TryAutoSpawn(current.name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsEndgameScene(scene.name))
            TryAutoSpawn(scene.name);
    }

    private static bool IsEndgameScene(string name) => name == YouWinSceneName || name == GameOverSceneName;

    private static void TryAutoSpawn(string sceneName)
    {
        if (FindObjectOfType<RunSummaryUI>() != null) return; // already wired in scene
        var go = new GameObject("RunSummaryUI_Auto");
        go.AddComponent<RunSummaryUI>();
    }

    // ---- Lifecycle ----
    private void Start()
    {
        // Always derive outcome from scene name so we don't depend on the Inspector toggle.
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == YouWinSceneName) isVictory = true;
        else if (sceneName == GameOverSceneName) isVictory = false;

        // Build any UI parts that weren't pre-wired.
        EnsureUIBuilt();

        var ps = PlayerStats.Instance;
        var gt = FindObjectOfType<GameTime>();
        int currentDay = gt != null ? gt.Day : 0;

        if (headlineText != null)
        {
            headlineText.text = isVictory ? "DEBT CLEARED!" : "TIME'S UP";
            headlineText.color = isVictory ? victoryColor : defeatColor;
        }

        if (playAgainButton != null)
        {
            playAgainButton.onClick.RemoveListener(PlayAgain);
            playAgainButton.onClick.AddListener(PlayAgain);
        }
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(GoToMainMenu);
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        if (ps == null)
        {
            if (statsBlockText != null) statsBlockText.text = "<i>(No run data found.)</i>";
            return;
        }

        if (playerPortrait != null && ps.PlayerSprite != null)
            playerPortrait.sprite = ps.PlayerSprite;

        int dayDebtCleared = ps.DayDebtCleared > 0 ? ps.DayDebtCleared : currentDay;
        int daysUsed = isVictory ? dayDebtCleared : currentDay;
        int netWorth = ps.NetWorth;

        if (subheadText != null)
        {
            subheadText.text = isVictory
                ? $"You paid off the shark in <b>{dayDebtCleared}</b> days."
                : $"The shark came to collect on Day <b>{currentDay}</b>.";
        }

        if (playerNameText != null) playerNameText.text = ps.PlayerName;
        if (netWorthText != null)   netWorthText.text   = $"Net Worth:  ${netWorth:N0}";
        if (walletText != null)     walletText.text     = $"Cash:  ${ps.PlayerWallet:N0}";
        if (debtText != null)       debtText.text       = ps.IsDebtPaidOff ? "Debt:  CLEARED" : $"Debt:  ${ps.Debt:N0} remaining";
        if (daysText != null)       daysText.text       = $"Days:  {daysUsed} / {ps.DayLimit}";
        if (copsText != null)       copsText.text       = $"Cops:  {ps.TimesCaughtByCops} busted / {ps.TotalCopEncounters} encountered";
        if (citiesText != null)     citiesText.text     = $"Cities visited:  {ps.UniqueCitiesVisited}";

        if (statsBlockText != null)
            statsBlockText.text = BuildStatsBlock(ps, daysUsed);

        SubmitLeaderboardEntry(ps, daysUsed, netWorth);

        if (highScoreText != null)
            UpdateLegacyHighScoreLine(ps.PlayerName, daysUsed);
    }

    // ---- Stats text ----
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
        AppendRow(sb, "Debt remaining", ps.IsDebtPaidOff ? "CLEARED" : $"${ps.Debt:N0}", ps.IsDebtPaidOff ? positiveValueColor : negativeValueColor);
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

    // ============================================================
    //  AUTO-BUILT UI
    // ============================================================
    // If statsBlockText hasn't been wired in the Inspector, we build the entire endgame UI
    // from code so the YouWin / GameOver scenes need zero setup. The layout is two columns:
    // stats on the left, leaderboard on the right, with a headline above and buttons below.

    private void EnsureUIBuilt()
    {
        if (statsBlockText != null) return; // assume scene is hand-wired

        // Canvas
        var canvasGo = new GameObject("RunSummaryCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Background panel (full screen)
        var bg = MakeRect(canvasGo.transform, "Background");
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.06f, 1f);
        StretchFull(bg);

        // Main vertical layout container with margins
        var main = MakeRect(bg, "Main");
        StretchFull(main, new Vector4(60, 40, 60, 40));
        var mainV = main.gameObject.AddComponent<VerticalLayoutGroup>();
        mainV.spacing = 18;
        mainV.childAlignment = TextAnchor.UpperCenter;
        mainV.childControlWidth = true;
        mainV.childControlHeight = false;
        mainV.childForceExpandWidth = true;
        mainV.childForceExpandHeight = false;

        // Headline
        headlineText = MakeText(main, "Headline", "", 72, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, 92);
        // Subhead
        subheadText  = MakeText(main, "Subhead", "", 28, FontStyles.Italic, new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.Center, 44);
        // Leaderboard rank flash
        leaderboardRankText = MakeText(main, "RankFlash", "", 32, FontStyles.Bold, victoryColor, TextAlignmentOptions.Center, 44);
        leaderboardRankText.gameObject.SetActive(false);

        // Two-column content row
        var content = MakeRect(main, "Content");
        var contentLE = content.gameObject.AddComponent<LayoutElement>();
        contentLE.flexibleHeight = 1f;
        contentLE.minHeight = 480;
        var contentH = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        contentH.spacing = 24;
        contentH.childAlignment = TextAnchor.UpperCenter;
        contentH.childControlWidth = true;
        contentH.childControlHeight = true;
        contentH.childForceExpandWidth = true;
        contentH.childForceExpandHeight = true;

        // Left: stats column
        var statsCol = MakeRect(content, "StatsColumn");
        var statsBg = statsCol.gameObject.AddComponent<Image>();
        statsBg.color = new Color(0.10f, 0.10f, 0.13f, 1f);
        var statsColLE = statsCol.gameObject.AddComponent<LayoutElement>();
        statsColLE.flexibleWidth = 1f;
        statsColLE.flexibleHeight = 1f;
        BuildStatsScrollView(statsCol);

        // Right: leaderboard column
        var lbCol = MakeRect(content, "LeaderboardColumn");
        var lbBg = lbCol.gameObject.AddComponent<Image>();
        lbBg.color = new Color(0.10f, 0.10f, 0.13f, 1f);
        var lbColLE = lbCol.gameObject.AddComponent<LayoutElement>();
        lbColLE.flexibleWidth = 1f;
        lbColLE.flexibleHeight = 1f;
        BuildLeaderboardColumn(lbCol);

        // Buttons row
        var btnRow = MakeRect(main, "Buttons");
        var btnRowLE = btnRow.gameObject.AddComponent<LayoutElement>();
        btnRowLE.minHeight = 72;
        btnRowLE.preferredHeight = 72;
        var btnH = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        btnH.spacing = 24;
        btnH.childAlignment = TextAnchor.MiddleCenter;
        btnH.childControlWidth = true;
        btnH.childControlHeight = true;
        btnH.childForceExpandWidth = false;
        btnH.childForceExpandHeight = true;
        playAgainButton = MakeButton(btnRow, "PlayAgainBtn", "PLAY AGAIN", new Color(0.25f, 0.55f, 0.25f), 320);
        mainMenuButton  = MakeButton(btnRow, "MainMenuBtn",  "MAIN MENU",  new Color(0.30f, 0.30f, 0.36f), 320);
    }

    private void BuildStatsScrollView(RectTransform parent)
    {
        statsBlockText = BuildScrollableTextBlock(
            parent,
            fontSize: 22,
            color: new Color(0.92f, 0.92f, 0.92f),
            wordWrap: true,
            padding: new Vector4(20, 20, 20, 20));
    }

    private void BuildLeaderboardColumn(RectTransform parent)
    {
        var inner = MakeRect(parent, "Inner");
        StretchFull(inner, new Vector4(20, 20, 20, 20));
        var innerV = inner.gameObject.AddComponent<VerticalLayoutGroup>();
        innerV.spacing = 8;
        innerV.childAlignment = TextAnchor.UpperLeft;
        innerV.childControlWidth = true;
        innerV.childControlHeight = false;
        innerV.childForceExpandWidth = true;
        innerV.childForceExpandHeight = false;

        // Header
        MakeText(inner, "LBHeader", "TOP RUNS", 30, FontStyles.Bold, sectionHeaderColor, TextAlignmentOptions.Center, 40);
        MakeText(inner, "LBSub", "fewest days wins", 16, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center, 22);

        // Scroll host
        var sv = MakeRect(inner, "ScrollHost");
        var svLE = sv.gameObject.AddComponent<LayoutElement>();
        svLE.flexibleHeight = 1f;
        svLE.minHeight = 360;
        var lbText = BuildScrollableTextBlock(
            sv,
            fontSize: 18,
            color: new Color(0.92f, 0.92f, 0.92f),
            wordWrap: false,
            padding: Vector4.zero);

        // Spawn a LeaderboardUI on this same GameObject and hand it the text reference.
        leaderboardUI = inner.gameObject.AddComponent<LeaderboardUI>();
        leaderboardUI.SetSingleBlockText(lbText);
    }

    // Constructs: ScrollRect + Viewport + Content (with VerticalLayoutGroup + ContentSizeFitter)
    // and a TMP_Text child that holds the rich-text body. Returns the text component.
    private TMP_Text BuildScrollableTextBlock(RectTransform host, int fontSize, Color color, bool wordWrap, Vector4 padding)
    {
        StretchFull(host); // ensure host fills its parent slot

        var inner = MakeRect(host, "Inner");
        StretchFull(inner, padding);

        var sr = inner.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = MakeRect(inner, "Viewport");
        StretchFull(viewport);
        var viewportImg = viewport.gameObject.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        sr.viewport = viewport;

        var content = MakeRect(viewport, "Content");
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0, 0);
        var contentVLG = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentVLG.padding = new RectOffset(8, 8, 8, 8);
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = true;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        var contentFit = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = content;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(content, false);
        var t = textGo.AddComponent<TextMeshProUGUI>();
        t.text = "";
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.enableWordWrapping = wordWrap;
        t.richText = true;

        return t;
    }

    // ---- UI factory helpers ----
    private static RectTransform MakeRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void StretchFull(RectTransform rt, Vector4 padding = default)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding.x, padding.w);
        rt.offsetMax = new Vector2(-padding.z, -padding.y);
    }

    private static TMP_Text MakeText(Transform parent, string name, string content, int size, FontStyles style, Color color, TextAlignmentOptions align, float preferredHeight)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.richText = true;
        if (preferredHeight > 0)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = preferredHeight;
            le.preferredHeight = preferredHeight;
        }
        return t;
    }

    private static Button MakeButton(Transform parent, string name, string label, Color bg, float minWidth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.20f);
        cb.pressedColor    = Color.Lerp(bg, Color.black, 0.20f);
        btn.colors = cb;

        var le = go.AddComponent<LayoutElement>();
        le.minWidth = minWidth;
        le.preferredWidth = minWidth;

        var lblGo = new GameObject("Label");
        lblGo.transform.SetParent(go.transform, false);
        var t = lblGo.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.fontSize = 24;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return btn;
    }
}
