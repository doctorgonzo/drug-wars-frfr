using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Persistent top-10 list of best runs, ranked by fewest days to clear debt.
// Tiebreak: higher net worth wins. Stored as JSON in PlayerPrefs.
public static class Leaderboard
{
    public const string PrefsKey = "Leaderboard_v1";
    public const int MaxEntries = 10;

    [Serializable]
    public class Entry
    {
        public string playerName;
        public int days;
        public int netWorth;
        public string isoDate;       // ISO 8601 timestamp
        public int totalClicks;
        public int citiesVisited;
        public bool wasVictory;      // true = paid off debt; false = ran out of days

        // Runtime-only flag set on a freshly-inserted entry so the UI can highlight it.
        // Not serialized — re-evaluated each session.
        [NonSerialized] public bool isNew;
    }

    [Serializable]
    private class EntryListWrapper
    {
        public List<Entry> entries = new List<Entry>();
    }

    public static List<Entry> Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();

        try
        {
            var wrapper = JsonUtility.FromJson<EntryListWrapper>(json);
            return wrapper?.entries ?? new List<Entry>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Leaderboard] Failed to parse stored entries — resetting. {e.Message}");
            return new List<Entry>();
        }
    }

    public static void Save(List<Entry> entries)
    {
        var wrapper = new EntryListWrapper { entries = entries ?? new List<Entry>() };
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    // Returns the 0-based rank if the entry made the top N, else -1.
    public static int TryInsert(Entry candidate)
    {
        if (candidate == null) return -1;
        var entries = Load();
        candidate.isNew = true;
        entries.Add(candidate);
        var sorted = SortDescendingQuality(entries).Take(MaxEntries).ToList();
        int rank = sorted.IndexOf(candidate);
        Save(sorted);
        return rank;
    }

    public static List<Entry> GetSorted()
    {
        return SortDescendingQuality(Load()).ToList();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    private static IEnumerable<Entry> SortDescendingQuality(IEnumerable<Entry> entries)
    {
        // Wins always rank above losses. Within wins: fewest days, tiebreak highest net worth.
        // Within losses: highest net worth (held out longest financially).
        return entries
            .Where(e => e != null && e.days > 0)
            .OrderByDescending(e => e.wasVictory)
            .ThenBy(e => e.wasVictory ? e.days : int.MaxValue)
            .ThenByDescending(e => e.netWorth);
    }
}
