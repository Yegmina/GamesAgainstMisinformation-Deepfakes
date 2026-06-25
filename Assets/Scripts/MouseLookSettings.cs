using System;
using UnityEngine;

public static class MouseLookSettings
{
    public const float MinSensitivity = 0.05f;
    public const float MaxSensitivity = 1.5f;
    public const float DefaultSensitivity = 0.45f;

    private const string SensitivityKey = "Settings.MouseLook.Sensitivity";
    private const string InvertHorizontalKey = "Settings.MouseLook.InvertHorizontal";
    private const string InvertVerticalKey = "Settings.MouseLook.InvertVertical";

    public static event Action Changed;

    public static float Sensitivity => ClampSensitivity(PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity));
    public static bool InvertHorizontal => PlayerPrefs.GetInt(InvertHorizontalKey, 0) == 1;
    public static bool InvertVertical => PlayerPrefs.GetInt(InvertVerticalKey, 0) == 1;

    public static void SetSensitivity(float value)
    {
        value = ClampSensitivity(value);
        if (Mathf.Approximately(Sensitivity, value))
        {
            Debug.Log($"[MouseLookSettings] Sensitivity unchanged at {FormatSensitivity(value)} ({value:0.00}).");
            return;
        }

        PlayerPrefs.SetFloat(SensitivityKey, value);
        PlayerPrefs.Save();
        NotifyChanged($"Sensitivity saved as {FormatSensitivity(value)} ({value:0.00}).");
    }

    public static void SetInvertHorizontal(bool value)
    {
        if (InvertHorizontal == value)
        {
            Debug.Log($"[MouseLookSettings] Invert horizontal unchanged at {value}.");
            return;
        }

        PlayerPrefs.SetInt(InvertHorizontalKey, value ? 1 : 0);
        PlayerPrefs.Save();
        NotifyChanged($"Invert horizontal saved as {value}.");
    }

    public static void SetInvertVertical(bool value)
    {
        if (InvertVertical == value)
        {
            Debug.Log($"[MouseLookSettings] Invert vertical unchanged at {value}.");
            return;
        }

        PlayerPrefs.SetInt(InvertVerticalKey, value ? 1 : 0);
        PlayerPrefs.Save();
        NotifyChanged($"Invert vertical saved as {value}.");
    }

    public static string FormatSensitivity(float value)
    {
        return Mathf.RoundToInt(ClampSensitivity(value) * 100f) + "%";
    }

    private static float ClampSensitivity(float value)
    {
        return Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
    }

    private static void NotifyChanged(string message)
    {
        Debug.Log("[MouseLookSettings] " + message);
        Changed?.Invoke();
    }
}
