using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System;

[DefaultExecutionOrder(-50)]
public class GameTime : MonoBehaviour
{
    [Header("Time Settings")]
    [Tooltip("How many real-time seconds equal one in-game second. 1 = real-time, 0.5 = twice as fast.")]
    [Min(0.0001f)]
    public float timeScale = 1f;

    [Tooltip("Start on this day/hour/minute/second.")]
    public int startDay = 1;
    public int startHour = 0;
    public int startMinute = 0;
    public int startSecond = 0;

    [Header("UI (Optional)")]
    [SerializeField] private TMP_Text timeText;

    [Header("Inspector Events (optional)")]
    public UnityEvent OnSecondChanged;
    public UnityEvent OnMinuteChanged;
    public UnityEvent OnHourChanged;
    public UnityEvent OnDayChanged;

    [Serializable]
    public struct GameDateTime
    {
        public int day, hour, minute, second;
        public GameDateTime(int d, int h, int m, int s)
        {
            day = d; hour = h; minute = m; second = s;
        }
        public override string ToString() => $"Day {day}, {hour:D2}:{minute:D2}:{second:D2}";
    }

    // ==== Public, read-only current time ====
    public int Day { get; private set; }
    public int Hour { get; private set; }
    public int Minute { get; private set; }
    public int Second { get; private set; }

    // ==== C# Events for code subscriptions ====
    public event Action<GameDateTime> SecondChanged;
    public event Action<GameDateTime> MinuteChanged;
    public event Action<GameDateTime> HourChanged;
    public event Action<GameDateTime> DayChanged;

    public event Action<bool> PausedChanged;

    private float timeAccumulator = 0f;
    private bool isPaused = false;

    // ==== Lifecycle ====
    private void Awake()
    {
        SetTime(new GameDateTime(startDay, startHour, startMinute, startSecond), invokeEvents: false);
        PriceService.InGameDay = Day;
    }

    private void Update()
    {
        if (isPaused) return;

        timeAccumulator += Time.deltaTime * (1f / timeScale);
        while (timeAccumulator >= 1f)
        {
            timeAccumulator -= 1f;
            AdvanceSecond();
        }

        if (timeText != null)
            timeText.text = $"{DayLabel()}, {Hour:D2}:{Minute:D2}:{Second:D2}";
    }

    // ==== Public API ====

    public void SetPaused(bool paused)
    {
        if (isPaused == paused) return;
        isPaused = paused;
        PausedChanged?.Invoke(isPaused);
    }

    public void TogglePaused() => SetPaused(!isPaused);

    public void SetTimeScale(float newScale)
    {
        timeScale = Mathf.Max(0.0001f, newScale);
    }

    public void SetTime(GameDateTime t, bool invokeEvents = true)
    {
        Day = Mathf.Max(1, t.day);
        Hour = Mathf.Clamp(t.hour, 0, 23);
        Minute = Mathf.Clamp(t.minute, 0, 59);
        Second = Mathf.Clamp(t.second, 0, 59);

        if (invokeEvents)
        {
            // fire all in increasing granularity so listeners can react properly
            DayChanged?.Invoke(Current);
            OnDayChanged?.Invoke();

            HourChanged?.Invoke(Current);
            OnHourChanged?.Invoke();

            MinuteChanged?.Invoke(Current);
            OnMinuteChanged?.Invoke();

            SecondChanged?.Invoke(Current);
            OnSecondChanged?.Invoke();
        }
    }

    public GameDateTime Current => new GameDateTime(Day, Hour, Minute, Second);

    public string DayLabel() => $"Day {Day}";

    /// <summary>Fast-forward any number of in-game seconds (can be negative if you want to go back).</summary>
    public void AddSeconds(int seconds)
    {
        if (seconds == 0) return;

        int total = Second + seconds;
        Second = Mod(total, 60);
        int carryMinutes = FloorDiv(total, 60);

        if (carryMinutes != 0) AddMinutes(carryMinutes);
        // We’ve already emitted events in AddMinutes / AddHours / AddDays
        SecondChanged?.Invoke(Current);
        OnSecondChanged?.Invoke();
    }

    public void AddMinutes(int minutes)
    {
        if (minutes == 0) return;

        int total = Minute + minutes;
        Minute = Mod(total, 60);
        int carryHours = FloorDiv(total, 60);

        if (carryHours != 0) AddHours(carryHours);

        MinuteChanged?.Invoke(Current);
        OnMinuteChanged?.Invoke();
    }

    public void AddHours(int hours)
    {
        if (hours == 0) return;

        int total = Hour + hours;
        Hour = Mod(total, 24);
        int carryDays = FloorDiv(total, 24);

        if (carryDays != 0) AddDays(carryDays);

        HourChanged?.Invoke(Current);
        OnHourChanged?.Invoke();
    }

    public void AddDays(int days)
    {
        if (days == 0) return;
        Day = Mathf.Max(1, Day + days);
        DayChanged?.Invoke(Current);
        OnDayChanged?.Invoke();
    }

    public void SkipToNextHour()
    {
        AddMinutes(60 - Minute - (Second > 0 ? 1 : 0));
        if (Second > 0) { Second = 0; SecondChanged?.Invoke(Current); OnSecondChanged?.Invoke(); }
    }

    public void SkipToNextDayAtHour(int hour = 0)
    {
        hour = Mathf.Clamp(hour, 0, 23);
        if (Hour >= hour) AddDays(1);
        Hour = hour; Minute = 0; Second = 0;

        DayChanged?.Invoke(Current); OnDayChanged?.Invoke();
        HourChanged?.Invoke(Current); OnHourChanged?.Invoke();
        MinuteChanged?.Invoke(Current); OnMinuteChanged?.Invoke();
        SecondChanged?.Invoke(Current); OnSecondChanged?.Invoke();
    }

    // ==== Internals ====

    private void AdvanceSecond()
    {
        int prevMinute = Minute;
        int prevHour = Hour;
        int prevDay = Day;

        Second++;
        if (Second >= 60)
        {
            Second = 0;
            Minute++;
            if (Minute >= 60)
            {
                Minute = 0;
                Hour++;
                if (Hour >= 24)
                {
                    Hour = 0;
                    Day++;
                    // Day changed
                    DayChanged?.Invoke(Current);
                    OnDayChanged?.Invoke();
                    PriceService.InGameDay = Day;
                }
                // Hour changed
                HourChanged?.Invoke(Current);
                OnHourChanged?.Invoke();
            }
            // Minute changed
            MinuteChanged?.Invoke(Current);
            OnMinuteChanged?.Invoke();
        }
        // Second changed always fires
        SecondChanged?.Invoke(Current);
        OnSecondChanged?.Invoke();
    }

    // Safe integer math for negative fast-forwards
    private static int FloorDiv(int a, int b)
    {
        int q = a / b;
        int r = a % b;
        if ((r != 0) && ((r > 0) != (b > 0))) q--;
        return q;
    }
    private static int Mod(int a, int b)
    {
        int m = a % b;
        if (m < 0) m += Mathf.Abs(b);
        return m;
    }
}
