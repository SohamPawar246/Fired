using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// F12 saves a timestamped PNG — for devlogs, itch page art, and marketing.
/// Lives on the persistent GameManager so it works in every scene.
/// Disabled on WebGL (browsers can't write to disk; use the browser's own
/// screenshot instead).
///
/// Screenshots land in:  %USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Screenshots
/// (the exact path is logged to the console each time).
/// </summary>
public class ScreenshotTaker : MonoBehaviour
{
#if !UNITY_WEBGL || UNITY_EDITOR
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.f12Key.wasPressedThisFrame) return;

        string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"shot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

        ScreenCapture.CaptureScreenshot(file);
        Debug.Log($"[ScreenshotTaker] Saved {file}");
    }
#endif
}
