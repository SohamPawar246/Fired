using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Tiny wrapper around the New Input System for the shell's simple needs
/// ("any key to continue", "pause pressed"). Gameplay should use proper
/// InputActions — this exists so menu scripts don't each reinvent device polling.
/// </summary>
public static class JamInput
{
    /// <summary>True on the frame any keyboard key, mouse button, gamepad button,
    /// or touch is pressed. Used by skippable screens.</summary>
    public static bool AnyPressThisFrame()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame ||
                              mouse.rightButton.wasPressedThisFrame ||
                              mouse.middleButton.wasPressedThisFrame)) return true;

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

        var pad = Gamepad.current;
        if (pad != null)
        {
            // Check every button control on the pad (sticks excluded automatically).
            foreach (var control in pad.allControls)
            {
                if (control is ButtonControl b && !b.synthetic && b.wasPressedThisFrame)
                    return true;
            }
        }
        return false;
    }

    /// <summary>Escape / gamepad Start / P. (P included because browsers sometimes
    /// swallow Escape in WebGL builds when the cursor is locked.)</summary>
    public static bool PausePressedThisFrame()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) return true;

        var pad = Gamepad.current;
        if (pad != null && pad.startButton.wasPressedThisFrame) return true;

        return false;
    }
}
