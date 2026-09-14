using UnityEngine;

/// <summary>
/// Persistent root singleton. Lives for the whole app lifetime and owns:
///  - SceneFlowController (all scene loads + fade transitions)
///  - AudioManager (music + SFX)
///  - Loaded settings/save data (loaded once, here)
///
/// HOW IT BOOTSTRAPS: you never place this in a scene by hand. The
/// [RuntimeInitializeOnLoadMethod] below instantiates Resources/GameManager.prefab
/// before the *first* scene loads — no matter which scene you hit Play in.
/// That is what makes every scene independently playable from the editor.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Epilepsy warning is shown once per launch; this flag tracks it.</summary>
    public static bool EpilepsyWarningShown;

    public SceneFlowController SceneFlow { get; private set; }
    public AudioManager Audio { get; private set; }

    // Runs before the first scene loads, regardless of which scene that is.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var prefab = Resources.Load<GameObject>("GameManager");
        if (prefab == null)
        {
            Debug.LogError("[GameManager] Resources/GameManager.prefab is missing. " +
                           "Run Tools > Jam Scaffold > Build All Scenes to regenerate it.");
            return;
        }
        Instantiate(prefab).name = "GameManager (persistent)";
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load settings/save data ONCE. Every later scene just reads from memory.
        SaveSystem.Load();

        SceneFlow = GetComponentInChildren<SceneFlowController>();
        Audio = GetComponentInChildren<AudioManager>();
    }

    /// <summary>
    /// Platform-aware quit. On WebGL there is nothing to quit, so menu code should
    /// hide Quit buttons entirely (see MainMenuController / PauseMenu) — this is
    /// just a safe backstop.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        Debug.Log("[GameManager] Quit ignored on WebGL.");
#else
        Application.Quit();
#endif
    }
}
