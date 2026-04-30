using TMPro;
using UnityEngine;

// Single leaderboard row. Attach to a row prefab with 4 TMP_Texts wired in the Inspector.
// LeaderboardUI calls Bind() to populate.
public class LeaderboardRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text daysText;
    [SerializeField] private TMP_Text netWorthText;
    [Tooltip("Optional date column, e.g. '2026-04-30'. Leave null to skip.")]
    [SerializeField] private TMP_Text dateText;
    [Tooltip("Optional WIN/LOSS column.")]
    [SerializeField] private TMP_Text resultText;

    public void Bind(int rank, Leaderboard.Entry entry, Color tint, bool bold)
    {
        if (rankText != null)     rankText.text     = $"#{rank}";
        if (nameText != null)     nameText.text     = entry.playerName ?? "—";
        if (daysText != null)     daysText.text     = $"{entry.days}d";
        if (netWorthText != null) netWorthText.text = $"${entry.netWorth:N0}";
        if (dateText != null)     dateText.text     = FormatDate(entry.isoDate);
        if (resultText != null)   resultText.text   = entry.wasVictory ? "WIN" : "LOSS";

        ApplyTint(rankText, tint, bold);
        ApplyTint(nameText, tint, bold);
        ApplyTint(daysText, tint, bold);
        ApplyTint(netWorthText, tint, bold);
        ApplyTint(dateText, tint, bold);
        ApplyTint(resultText, tint, bold);
    }

    private static void ApplyTint(TMP_Text label, Color color, bool bold)
    {
        if (label == null) return;
        label.color = color;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
    }

    private static string FormatDate(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        if (System.DateTime.TryParse(iso, out var dt))
            return dt.ToLocalTime().ToString("yyyy-MM-dd");
        return "";
    }
}
