using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor / Development-Build-only iteration shortcuts. Lives on the persistent
/// GameManager. Compiled out of release builds entirely — safe to leave in.
///
///   Ctrl+R = restart current scene (through the normal fade transition)
///
/// (Plain R would clash with gameplay keys; Ctrl+R stays out of the way.)
/// </summary>
public class DevHotkeys : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.ctrlKey.isPressed && kb.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            GameManager.Instance.SceneFlow.ReloadCurrentScene();
        }
    }
#endif
}
