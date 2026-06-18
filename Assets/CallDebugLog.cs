using System;
using System.IO;
using UnityEngine;

/// <summary>Session debug logger for call-system verification (debug-336aa1).</summary>
public static class CallDebugLog
{
    const string SessionId = "336aa1";
    const string LogFileName = "debug-336aa1.log";

    // #region agent log
    public static void Write(string hypothesisId, string location, string message, string dataJson, string runId = "pre-fix")
    {
        try
        {
            string path = Path.Combine(Application.dataPath, "..", LogFileName);
            string line =
                "{\"sessionId\":\"" + SessionId + "\",\"runId\":\"" + runId +
                "\",\"hypothesisId\":\"" + hypothesisId + "\",\"location\":\"" + location +
                "\",\"message\":\"" + message + "\",\"data\":" + dataJson +
                ",\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}\n";
            File.AppendAllText(path, line);
        }
        catch { /* ignore logging failures */ }
    }
    // #endregion
}
