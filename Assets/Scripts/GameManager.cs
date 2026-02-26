using UnityEngine;
using System;
using TMPro;

public class GameManager : MonoBehaviour
{

    public static GameManager S;
    
    public TMP_Text informationText;    
    
    public int availableHour = 18;
    public int availableMinute = 0;
    public int durationMinutes = 5;

    public bool enforceTimeWindow = true;
    public bool IsGameAvailable => !enforceTimeWindow || IsWithinAvailabilityWindow();

    void Awake(){
        S = this;
    }
    
    void Start()
    {
        if (enforceTimeWindow && !IsGameAvailable)
        {
            OnOutsideAvailabilityWindow();
        } else {
            // Let the Player Enter a word
            GetTextInput();
        }
    }

    void Update()
    {
        // Optional: re-check each frame if you need to disable mid-session when window closes
        // if (enforceTimeWindow && !IsGameAvailable) { ... }
    }

    bool IsWithinAvailabilityWindow()
    {
        var now = DateTime.Now.TimeOfDay;
        var start = new TimeSpan(availableHour, availableMinute, 0);
        var end = new TimeSpan(availableHour, availableMinute + durationMinutes, 0);
        return now >= start && now < end;
    }

    /// <summary>Override or call from UI: show message, block input, or load a "come back later" screen.</summary>
    protected virtual void OnOutsideAvailabilityWindow()
    {
        informationText.text = $"only available between {availableHour:D2}:{availableMinute:D2} and {availableHour:D2}:{availableMinute + durationMinutes:D2}. \n please come back later.";
        // TODO: e.g. show UI panel, disable player input, or load a "come back later" scene
    }

    /// <summary>Minutes until the game becomes available (0 if already available).</summary>
    public double MinutesUntilAvailable()
    {
        if (IsGameAvailable) return 0;
        var now = DateTime.Now;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0);
        if (now < todayStart)
            return (todayStart - now).TotalMinutes;
        var tomorrowStart = todayStart.AddDays(1);
        return (tomorrowStart - now).TotalMinutes;
    }

    /// <summary>Minutes since the availability window ended (0 if currently available). Use for move-away ramp from home.</summary>
    public double MinutesSinceAvailableEnded()
    {
        if (IsGameAvailable) return 0;
        var now = DateTime.Now;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, availableHour, availableMinute, 0);
        var windowEnd = todayStart.AddMinutes(durationMinutes);
        if (now >= windowEnd)
            return (now - windowEnd).TotalMinutes;
        var yesterdayEnd = todayStart.AddDays(-1).AddMinutes(durationMinutes);
        return (now - yesterdayEnd).TotalMinutes;
    }

    private void GetTextInput(){
        informationText.text = "enter a six letter word";
    }
}
