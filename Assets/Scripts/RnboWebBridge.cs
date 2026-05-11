using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>WebGL P/Invoke into <c>RNBOBridge.jslib</c> (RNBO.js + exported patch JSON).</summary>
public static class RnboWebBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void RNBO_Init(string patcherUrl, string depsUrl);
    [DllImport("__Internal")] static extern int RNBO_IsReady();
    [DllImport("__Internal")] static extern IntPtr RNBO_LastError();
    [DllImport("__Internal")] static extern int RNBO_SetParamById(string paramId, double value);
    [DllImport("__Internal")] static extern int RNBO_SendMessage(string tag, double value);

    public static void Init(string patcherUrl, string depsUrl) => RNBO_Init(patcherUrl, depsUrl);
    public static bool IsReady() => RNBO_IsReady() != 0;

    public static string GetLastError()
    {
        var ptr = RNBO_LastError();
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr);
    }

    public static bool SetParamById(string paramId, double value) => RNBO_SetParamById(paramId, value) != 0;
    public static bool SendMessage(string tag, double value) => RNBO_SendMessage(tag, value) != 0;
#else
    public static void Init(string patcherUrl, string depsUrl) { }
    public static bool IsReady() => false;
    public static string GetLastError() => "RNBO web bridge is only available in WebGL builds.";
    public static bool SetParamById(string paramId, double value) => false;
    public static bool SendMessage(string tag, double value) => false;
#endif
}

