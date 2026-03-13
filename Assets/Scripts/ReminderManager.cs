using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class ReminderManager : MonoBehaviour
{
    public string gameUrl = "https://mouseandthebillionaire.github.io/lautir/0.2/";

    public int availableHour = 12;
    public int availableDuration = 5;    

    /// <summary>
    /// Creates an .ics (iCalendar) event starting at the next available hour,
    /// repeating daily for 5 occurrences, and triggers a download/open of the file.
    /// </summary>
    /// 
    public void DownloadReminder()
    {
        var now = DateTime.Now;
        // Next occurrence of availableHour (today or tomorrow if that hour has passed)
        var start = new DateTime(now.Year, now.Month, now.Day, availableHour, 0, 0, DateTimeKind.Local);
        if (now >= start)
            start = start.AddDays(1);

        var end = start.AddMinutes(availableDuration);

        string ics = BuildIcal(start, end, gameUrl);

#if UNITY_WEBGL && !UNITY_EDITOR
        // In WebGL, trigger a browser download using the JS plugin (Blob + anchor click).
        WebGLDownloadIcs("lautirReminder.ics", ics);
#else
        // In editor / standalone, write to disk and reveal/open.
        string path = Path.Combine(Application.persistentDataPath, "lautirReminder.ics");
        File.WriteAllText(path, ics);

    #if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(path);
    #else
        Application.OpenURL("file://" + path);
    #endif
#endif
    }

    static string BuildIcal(DateTime startLocal, DateTime endLocal, string url)
    {
        var startUtc = startLocal.ToUniversalTime();
        var endUtc = endLocal.ToUniversalTime();
        var stampUtc = DateTime.UtcNow;

        string format = "yyyyMMddTHHmmssZ";
        string dtStart = startUtc.ToString(format);
        string dtEnd = endUtc.ToString(format);
        string dtStamp = stampUtc.ToString(format);
        string uid = $"reminder-{Guid.NewGuid():N}@unity";

        return
            "BEGIN:VCALENDAR\r\n" +
            "VERSION:2.0\r\n" +
            "PRODID:-//ReminderManager//EN\r\n" +
            "BEGIN:VEVENT\r\n" +
            "UID:" + uid + "\r\n" +
            "DTSTAMP:" + dtStamp + "\r\n" +
            "DTSTART:" + dtStart + "\r\n" +
            "DTEND:" + dtEnd + "\r\n" +
            "RRULE:FREQ=DAILY;COUNT=6\r\n" +
            "SUMMARY:LAUTIR\r\n" +
            (string.IsNullOrEmpty(url) ? "" : "DESCRIPTION:LAUTIR - " + url + "\r\n") +
            "BEGIN:VALARM\r\n" +
            "ACTION:DISPLAY\r\n" +
            "DESCRIPTION:LAUTIR\r\n" +
            "TRIGGER:-PT5M\r\n" +
            "END:VALARM\r\n" +
            "END:VEVENT\r\n" +
            "END:VCALENDAR\r\n";
    }
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void WebGLDownloadIcs(string filename, string content);
    #endif
}
