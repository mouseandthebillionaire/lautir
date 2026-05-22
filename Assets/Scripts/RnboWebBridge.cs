using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// WebGL P/Invoke into <c>RNBOBridge.jslib</c>. One RNBO JS device per <paramref name="instanceIndex"/>
/// (matches Unity mixer "Instance Index"). Shares AudioContext and caches patch JSON across instances.
/// </summary>
public static class RnboWebBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void RNBO_Init(int instanceIndex, string patcherUrl, string depsUrl);
    [DllImport("__Internal")] static extern int RNBO_IsReady(int instanceIndex);
    [DllImport("__Internal")] static extern IntPtr RNBO_LastError(int instanceIndex);
    [DllImport("__Internal")] static extern int RNBO_SetParamById(int instanceIndex, string paramId, double value);
    [DllImport("__Internal")] static extern int RNBO_SendMessage(int instanceIndex, string tag, double value);

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
#else
    public static void Init(int instanceIndex, string patcherUrl, string depsUrl) { }
    public static bool IsReady(int instanceIndex) => false;

    public static string GetLastError(int instanceIndex) =>
        "RNBO web bridge is only available in WebGL builds.";

    public static bool SetParamById(int instanceIndex, string paramId, double value) => false;
    public static bool SendMessage(int instanceIndex, string tag, double value) => false;
#endif
}
