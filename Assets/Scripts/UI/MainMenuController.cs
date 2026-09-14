using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Main menu logic. Buttons are wired to the public On* methods by the scaffold
/// builder. Handles the two conditional buttons:
///  - Continue: only visible when SaveSystem.HasSaveData
///  - Quit:     hidden on WebGL builds (quitting a browser tab is not a thing)
/// Also gives the EventSystem an initial selection so gamepad/keyboard
/// navigation works without touching the mouse first.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private GameObject continueButton;
    [SerializeField] private GameObject quitButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject howToPlayPanel;
    [SerializeField] private GameObject firstSelected; // usually the Play button
    [SerializeField] private GameObject gunshotFx;     // MenuGunshot flourish, fired on PLAY

    private bool _firing;

    private void Start()
    {
        // Runs are one-shot — there is nothing to continue into, so the button
        // is permanently hidden (kept wired in case a save system lands later).
        if (continueButton != null) continueButton.SetActive(false);

#if UNITY_WEBGL && !UNITY_EDITOR
        if (quitButton != null) quitButton.SetActive(false);
#endif

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (howToPlayPanel != null) howToPlayPanel.SetActive(false);

        SelectForGamepad(firstSelected);
    }

    private void Update()
    {
        // If a gamepad user closes a panel, make sure something is selected again.
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null
            && !AnyPanelOpen() && JamInput.AnyPressThisFrame())
        {
            SelectForGamepad(firstSelected);
        }
    }

    private bool AnyPanelOpen() =>
        (settingsPanel != null && settingsPanel.activeSelf) ||
        (howToPlayPanel != null && howToPlayPanel.activeSelf);

    public static void SelectForGamepad(GameObject go)
    {
        if (go != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    // ---- Button handlers -----------------------------------------------------

    public void OnPlay()
    {
        if (_firing) return;
        StartCoroutine(FireAndLoad());
    }

    /// <summary>PLAY literally fires the run into existence: gunshot flourish
    /// (BANG! burst + flash + bang SFX via MenuGunshot), then the scene load.</summary>
    private IEnumerator FireAndLoad()
    {
        _firing = true;
        // NOTE for jam gameplay code: starting a NEW game is where you'd wipe/init
        // real save data. The flag below just makes Continue appear next visit.
        SaveSystem.HasSaveData = true;
        SaveSystem.Save();

        if (gunshotFx != null)
        {
            gunshotFx.SetActive(true); // MenuGunshot plays the bang + animates itself
            yield return new WaitForSecondsRealtime(MenuGunshot.Duration * 0.85f);
        }
        else
        {
            AudioManager.Instance.PlayUIConfirm();
        }
        GameManager.Instance.SceneFlow.LoadScene("Game");
        _firing = false;
    }

    public void OnContinue()
    {
        AudioManager.Instance.PlayUIConfirm();
        // Placeholder: loads the Game scene. Point this at real save-restore later.
        GameManager.Instance.SceneFlow.LoadScene("Game");
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

    public void OnCredits()
    {
        AudioManager.Instance.PlayUIClick();
        GameManager.Instance.SceneFlow.LoadScene("Credits");
    }

    public void OnQuit()
    {
        AudioManager.Instance.PlayUIBack();
        GameManager.Instance.QuitGame();
    }
}
