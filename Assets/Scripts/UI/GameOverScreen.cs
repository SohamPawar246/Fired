using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// End-of-run result overlay (lives inside the Game scene). Gameplay code never
/// touches this directly — it fires ONE event and the UI takes over:
///
///     GameEvents.RaiseGameOver(won: true);   // or false
///
/// Shows "You Win" / "Game Over" (placeholder text, restyle freely), freezes the
/// game, and offers Retry / Return to Menu.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private GameObject overlayRoot;   // inactive by default
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsText;       // run summary line (optional)
    [SerializeField] private GameObject firstSelected; // Retry button

    [Header("Best-shot polaroid (baked scene objects — restyle in the Inspector)")]
    [SerializeField] private GameObject bestSnapRoot;  // inactive by default
    [SerializeField] private RawImage bestSnapPhoto;
    [SerializeField] private TMP_Text bestSnapCaption;

    public static bool IsShowing { get; private set; }

    private void OnEnable() => GameEvents.GameOver += Show;
    private void OnDisable()
    {
        GameEvents.GameOver -= Show;
        IsShowing = false; // scene unloading — clear the static flag
    }

    private void Start()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        else Debug.LogError("[GameOverScreen] overlayRoot is not assigned.", this);
        if (bestSnapRoot != null) bestSnapRoot.SetActive(false);
    }

    private void Show(bool won)
    {
        if (IsShowing) return;
        IsShowing = true;

        Time.timeScale = 0f;
        if (titleText != null)
        {
            titleText.text = won ? "YOU WIN" : "GAME OVER";
            titleText.color = won ? Theme.Positive : Theme.Negative;
        }
        if (statsText != null) statsText.text = GameEvents.LastRunSummary;
        if (overlayRoot != null) overlayRoot.SetActive(true);

        if (won) AudioManager.Instance.PlayUIConfirm(); else AudioManager.Instance.PlayUIBack();
        MainMenuController.SelectForGamepad(firstSelected);

        ShowBestSnap();
    }

    /// <summary>The run's best slow-mo moment, shown in the polaroid that already
    /// lives in the scene. Only the photo texture and the caption are set here —
    /// position, tilt, frame colour and fonts are authored in the Inspector.
    /// The texture lives in memory only and is cleared when the next run starts.</summary>
    private void ShowBestSnap()
    {
        if (bestSnapRoot == null) return;

        bool has = GameEvents.BestSnapTexture != null;
        bestSnapRoot.SetActive(has);
        if (!has) return;

        if (bestSnapPhoto != null) bestSnapPhoto.texture = GameEvents.BestSnapTexture;
        if (bestSnapCaption != null)
            bestSnapCaption.text = $"BEST SHOT   ·   {GameEvents.BestSnapLabel}   +{GameEvents.BestSnapScore}";
    }

    /// <summary>
    /// R restarts straight from the results screen. Enter/Space already work through
    /// the EventSystem because Retry is the selected button, but a runner wants a
    /// dedicated key that needs no reading and no mouse — the retry loop should cost
    /// one input, not a click plus a second click to fire.
    /// </summary>
    private void Update()
    {
        if (!IsShowing) return;
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame) OnRetry();
    }

    // ---- Button handlers -------------------------------------------------------

    public void OnRetry()
    {
        IsShowing = false;
        Time.timeScale = 1f;
        AudioManager.Instance.PlayUIConfirm();
        GameManager.Instance.SceneFlow.ReloadCurrentScene();
    }

    public void OnMainMenu()
    {
        IsShowing = false;
        Time.timeScale = 1f;
        AudioManager.Instance.PlayUIBack();
        GameManager.Instance.SceneFlow.LoadScene("MainMenu");
    }
}
