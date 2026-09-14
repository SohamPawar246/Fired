using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// F1 toggles a small FPS/frametime readout (top-left). IMGUI on purpose —
/// zero scene setup, zero dependencies. Lives on the persistent GameManager.
///
/// SUBMISSION NOTE: this compiles into all builds so judges *could* find it.
/// It's harmless, but wrap the class body in #if UNITY_EDITOR || DEVELOPMENT_BUILD
/// before the final build if you want it stripped.
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    private bool _visible;
    private float _smoothedDelta;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame) _visible = !_visible;

        // Exponential smoothing so the number is readable, not a blur.
        _smoothedDelta = Mathf.Lerp(_smoothedDelta, Time.unscaledDeltaTime, 0.06f);
    }

    private void OnGUI()
    {
        if (!_visible) return;

        float fps = _smoothedDelta > 0f ? 1f / _smoothedDelta : 0f;
        string text = $"FPS {fps:0}  ({_smoothedDelta * 1000f:0.0} ms)";

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = fps < 30 ? Color.red : Color.green }
        };
        GUI.Label(new Rect(12, 40, 320, 30), text, style); // below the shadow line
        var shadow = new GUIStyle(style) { normal = { textColor = Color.black } };
        GUI.Label(new Rect(13, 41, 320, 30), text, shadow);
        GUI.Label(new Rect(12, 40, 320, 30), text, style);
    }
}
