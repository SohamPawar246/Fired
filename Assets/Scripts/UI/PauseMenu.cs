using UnityEngine;

/// <summary>
/// In-game pause overlay (lives inside the Game scene, NOT a separate scene).
/// Escape / gamepad Start / P toggles it. Pauses via Time.timeScale = 0 — all
/// shell UI runs on unscaled time so it keeps animating while paused.
///
/// Gameplay code can react to pausing by subscribing to GameEvents.PauseChanged.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private GameObject overlayRoot;    // dim + panel, inactive by default
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject quitButton;
    [SerializeField] private GameObject firstSelected;  // Resume button

    public static bool IsPaused { get; private set; }

    private void Start()
    {
        overlayRoot.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
#if UNITY_WEBGL && !UNITY_EDITOR
        if (quitButton != null) quitButton.SetActive(false);
#endif
    }

    private void Update()
    {
        if (!JamInput.PausePressedThisFrame()) return;
        if (GameOverScreen.IsShowing) return;               // game-over owns the screen
        if (GameManager.Instance.SceneFlow.IsLoading) return;

        // If a sub-panel is open, Escape closes it instead of unpausing.
        if (IsPaused && settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSubPanel(settingsPanel);
            return;
        }
        if (IsPaused && howToPlayPanel != null && howToPlayPanel.activeSelf)
        {
            CloseSubPanel(howToPlayPanel);
            return;
        }

        if (IsPaused) Resume(); else Pause();
    }

    private void CloseSubPanel(GameObject panel)
    {
        panel.SetActive(false);
        AudioManager.Instance.PlayUIBack();
        MainMenuController.SelectForGamepad(firstSelected);
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        overlayRoot.SetActive(true);
        AudioManager.Instance.PlayUIClick();
        MainMenuController.SelectForGamepad(firstSelected);
        GameEvents.RaisePauseChanged(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        overlayRoot.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);
        AudioManager.Instance.PlayUIBack();
        GameEvents.RaisePauseChanged(false);
    }

    // ---- Button handlers -------------------------------------------------------

    public void OnResume() => Resume();

    public void OnRestart()
    {
        ResetPauseState();
        AudioManager.Instance.PlayUIConfirm();
        GameManager.Instance.SceneFlow.ReloadCurrentScene();
    }

    public void OnSettings()
    {
        AudioManager.Instance.PlayUIClick();
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnHowToPlay()
    {
        AudioManager.Instance.PlayUIClick();
        if (howToPlayPanel != null) howToPlayPanel.SetActive(true);
    }

    public void OnMainMenu()
    {
        ResetPauseState();
        AudioManager.Instance.PlayUIBack();
        GameManager.Instance.SceneFlow.LoadScene("MainMenu");
    }

    public void OnQuit()
    {
        ResetPauseState();
        GameManager.Instance.QuitGame();
    }

    private void ResetPauseState()
    {
        IsPaused = false;
        Time.timeScale = 1f; // SceneFlow also resets this, but belt-and-braces
    }

    private void OnDestroy()
    {
        // Scene unloaded while paused (e.g. dev hotkey) — never leak timeScale 0.
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
