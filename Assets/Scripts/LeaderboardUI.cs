using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Renders the persistent top-N leaderboard.
// Two render modes (Inspector-driven):
//   - Single text: assign `singleBlockText` and we render the whole board into one TMP_Text with rich-text alignment.
//   - Row prefabs: assign `rowPrefab` + `rowContainer` and we instantiate one row per entry. Row prefab must have
//     a LeaderboardRowUI component (or 4 TMP_Texts named Rank/Name/Days/NetWorth — see LeaderboardRowUI).
public class LeaderboardUI : MonoBehaviour
{
    [Header("Mode A — single TMP block (simplest)")]
    [SerializeField] private TMP_Text singleBlockText;

    [Header("Mode B — instanced row prefabs")]
    [SerializeField] private LeaderboardRowUI rowPrefab;
    [SerializeField] private Transform rowContainer;

    [Header("Header row")]
    [Tooltip("Optional fixed header label above the rows. Leave null to skip.")]
    [SerializeField] private TMP_Text headerLabel;
    [SerializeField] private string headerText = "TOP RUNS — fewest days wins";

    [Header("Empty state")]
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private string emptyStateMessage = "No runs on the board yet — be the first!";

    [Header("Highlight")]
    [SerializeField] private Color normalColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.3f);
    [Tooltip("If true, also bolds the highlighted row.")]
    [SerializeField] private bool boldHighlight = true;

    private readonly List<LeaderboardRowUI> _spawnedRows = new List<LeaderboardRowUI>();

    private void Start()
    {
        if (headerLabel != null) headerLabel.text = headerText;
        Refresh(highlightEntry: null);
    }

    public void Refresh(Leaderboard.Entry highlightEntry = null)
    {
        var sorted = Leaderboard.GetSorted();

        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(sorted.Count == 0);
        if (sorted.Count == 0 && emptyStateText != null)
            emptyStateText.text = emptyStateMessage;

        if (singleBlockText != null)
            singleBlockText.text = BuildSingleBlock(sorted, highlightEntry);

        if (rowPrefab != null && rowContainer != null)
            BuildRows(sorted, highlightEntry);
    }

    // Convenience for RunSummaryUI: refresh + highlight the entry it just inserted.
    public void RefreshAndHighlight(Leaderboard.Entry justInserted)
    {
        Refresh(justInserted);
    }

    private string BuildSingleBlock(List<Leaderboard.Entry> sorted, Leaderboard.Entry highlightEntry)
    {
        var sb = new System.Text.StringBuilder();
        if (sorted.Count == 0)
            return $"<i>{emptyStateMessage}</i>";

        sb.Append("<mspace=0.55em>");
        sb.AppendLine("<b> #   NAME              DAYS    NET WORTH    RESULT</b>");
        for (int i = 0; i < sorted.Count; i++)
        {
            var e = sorted[i];
            string result = e.wasVictory ? "WIN" : "LOSS";
            string row = $"{i + 1,2}.  {Truncate(e.playerName, 14),-14}  {e.days,4}    ${e.netWorth,11:N0}    {result,-4}";
            bool isHighlight = IsSameEntry(e, highlightEntry);
            if (isHighlight)
            {
                string colorHex = ColorUtility.ToHtmlStringRGB(highlightColor);
                if (boldHighlight)
                    sb.Append("<color=#").Append(colorHex).Append("><b>").Append(row).AppendLine("</b></color>");
                else
                    sb.Append("<color=#").Append(colorHex).Append('>').Append(row).AppendLine("</color>");
            }
            else
            {
                sb.AppendLine(row);
            }
        }
        sb.Append("</mspace>");
        return sb.ToString();
    }

    private void BuildRows(List<Leaderboard.Entry> sorted, Leaderboard.Entry highlightEntry)
    {
        // Pool-style: reuse existing rows, append more if needed, deactivate extras.
        for (int i = 0; i < sorted.Count; i++)
        {
            LeaderboardRowUI row;
            if (i < _spawnedRows.Count)
            {
                row = _spawnedRows[i];
                row.gameObject.SetActive(true);
            }
            else
            {
                row = Instantiate(rowPrefab, rowContainer);
                _spawnedRows.Add(row);
            }
            bool isHighlight = IsSameEntry(sorted[i], highlightEntry);
            row.Bind(i + 1, sorted[i], isHighlight ? highlightColor : normalColor, isHighlight && boldHighlight);
        }
        for (int i = sorted.Count; i < _spawnedRows.Count; i++)
            _spawnedRows[i].gameObject.SetActive(false);
    }

    private static bool IsSameEntry(Leaderboard.Entry a, Leaderboard.Entry b)
    {
        if (a == null || b == null) return false;
        if (ReferenceEquals(a, b)) return true;
        return a.playerName == b.playerName
            && a.days == b.days
            && a.netWorth == b.netWorth
            && a.isoDate == b.isoDate;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max);
    }
}
