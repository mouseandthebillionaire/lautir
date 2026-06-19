using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// WebGL P/Invoke into <c>RNBOBridge.jslib</c>. One RNBO JS device per <paramref name="instanceIndex"/>
/// (matches Unity mixer "Instance Index"). Shares AudioContext and caches patch JSON across instances.
/// </summary>
public static class RnboWebBridge
{
    public const string PadBufferId = "pad";

    public static string LautirSongPatcherUrl =>
        Application.streamingAssetsPath + "/LautirSong/lautirSong.export.json";

    public static string LautirSongDepsUrl =>
        Application.streamingAssetsPath + "/LautirSong/dependencies.json";

    public enum DataBufferLoadState
    {
        Idle = 0,
        Loading = 1,
        Succeeded = 2,
        Failed = 3
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void RNBO_ResumeAudioOnGesture();
    [DllImport("__Internal")] static extern void RNBO_Init(int instanceIndex, string patcherUrl, string depsUrl);
    [DllImport("__Internal")] static extern int RNBO_IsReady(int instanceIndex);
    [DllImport("__Internal")] static extern IntPtr RNBO_LastError(int instanceIndex);
    [DllImport("__Internal")] static extern int RNBO_SetParamById(int instanceIndex, string paramId, double value);
    [DllImport("__Internal")] static extern int RNBO_SendMessage(int instanceIndex, string tag, double value);
    [DllImport("__Internal")] static extern int RNBO_LoadDataBufferFromUrl(int instanceIndex, string bufferId, string url);
    [DllImport("__Internal")] static extern int RNBO_GetDataBufferLoadState(int instanceIndex);
    [DllImport("__Internal")] static extern void RNBO_ResetDataBufferLoadState(int instanceIndex);

    /// <summary>Must run in the same frame as the user's tap/key (before any yield).</summary>
    public static void ResumeAudioOnUserGesture() => RNBO_ResumeAudioOnGesture();

    public static void Init(int instanceIndex, string patcherUrl, string depsUrl) =>
        RNBO_Init(instanceIndex, patcherUrl, depsUrl);

    public static bool IsReady(int instanceIndex) => RNBO_IsReady(instanceIndex) != 0;

    public static string GetLastError(int instanceIndex)
    {
        var ptr = RNBO_LastError(instanceIndex);
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr);
    }

    public static bool SetParamById(int instanceIndex, string paramId, double value) =>
        RNBO_SetParamById(instanceIndex, paramId, value) != 0;

    public static bool SendMessage(int instanceIndex, string tag, double value) =>
        RNBO_SendMessage(instanceIndex, tag, value) != 0;

    /// <summary>Fetch + decode a WAV (or other browser-decodable audio) into an RNBO buffer by id.</summary>
    public static bool LoadDataBufferFromUrl(int instanceIndex, string bufferId, string url) =>
        RNBO_LoadDataBufferFromUrl(instanceIndex, bufferId, url) != 0;

    public static DataBufferLoadState GetDataBufferLoadState(int instanceIndex) =>
        (DataBufferLoadState)RNBO_GetDataBufferLoadState(instanceIndex);

    public static void ResetDataBufferLoadState(int instanceIndex) =>
        RNBO_ResetDataBufferLoadState(instanceIndex);

    public static bool LoadPadFromStreamingAssets(int instanceIndex, string relativePath) =>
        LoadDataBufferFromUrl(instanceIndex, PadBufferId, StreamingAssetsUrl(relativePath));
#else
    public static void ResumeAudioOnUserGesture() { }
    public static void Init(int instanceIndex, string patcherUrl, string depsUrl) { }
    public static bool IsReady(int instanceIndex) => false;

    public static string GetLastError(int instanceIndex) =>
        "RNBO web bridge is only available in WebGL builds.";

    public static bool SetParamById(int instanceIndex, string paramId, double value) => false;
    public static bool SendMessage(int instanceIndex, string tag, double value) => false;
    public static bool LoadDataBufferFromUrl(int instanceIndex, string bufferId, string url) => false;
    public static DataBufferLoadState GetDataBufferLoadState(int instanceIndex) => DataBufferLoadState.Idle;
    public static void ResetDataBufferLoadState(int instanceIndex) { }
    public static bool LoadPadFromStreamingAssets(int instanceIndex, string relativePath) => false;
#endif

    /// <summary>Build a fetchable URL for a file under StreamingAssets (WebGL-safe).</summary>
    public static string StreamingAssetsUrl(string relativePath)
    {
        var path = (relativePath ?? "").TrimStart('/');
        return Application.streamingAssetsPath + "/" + path;
    }

    /// <summary>Wait until the in-flight buffer load finishes, fails, or times out.</summary>
    public static IEnumerator WaitForDataBufferLoad(int instanceIndex, float timeoutSeconds = 60f)
    {
        float t0 = Time.realtimeSinceStartup;
        while (GetDataBufferLoadState(instanceIndex) == DataBufferLoadState.Loading
               && Time.realtimeSinceStartup - t0 < timeoutSeconds)
            yield return null;

        var state = GetDataBufferLoadState(instanceIndex);
        if (state == DataBufferLoadState.Loading)
            Debug.LogError($"[LAUTIR] Data buffer load timed out (instance {instanceIndex})");
        else if (state == DataBufferLoadState.Failed)
            Debug.LogError($"[LAUTIR] Data buffer load failed (instance {instanceIndex}): {GetLastError(instanceIndex)}");
    }
}
