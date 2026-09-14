using UnityEngine;

/// <summary>
/// Photosensitive-seizure warning. Deliberately static and high-contrast —
/// nothing on this screen animates except a gentle prompt fade-in AFTER the
/// minimum display time.
///
/// Shown once per launch (tracked via GameManager.EpilepsyWarningShown; if the
/// flow somehow revisits this scene in the same session, it passes through
/// instantly). Cannot be dismissed before minDisplaySeconds.
/// </summary>
public class EpilepsyWarning : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "MainMenu";
    [Tooltip("The warning cannot be dismissed before this many seconds.")]
    [SerializeField] private float minDisplaySeconds = 4.5f;
    [SerializeField] private CanvasGroup continuePrompt; // "press any key" line, hidden until dismissible

    private float _elapsed;
    private bool _advancing;

    private void Start()
    {
        if (GameManager.EpilepsyWarningShown)
        {
            // Already shown this launch — pass straight through.
            Advance();
            return;
        }
        if (continuePrompt != null) continuePrompt.alpha = 0f;
    }

    private void Update()
    {
        if (_advancing) return;
        _elapsed += Time.unscaledDeltaTime;

        bool dismissible = _elapsed >= minDisplaySeconds;

        // Fade the prompt in once the warning becomes dismissible.
        if (continuePrompt != null && dismissible)
            continuePrompt.alpha = Mathf.MoveTowards(continuePrompt.alpha, 1f, Time.unscaledDeltaTime * 2f);

        if (dismissible && JamInput.AnyPressThisFrame())
        {
            GameManager.EpilepsyWarningShown = true;
            Advance();
        }
    }

    private void Advance()
    {
        if (_advancing) return;
        _advancing = true;
        GameManager.Instance.SceneFlow.LoadScene(nextSceneName);
    }
}
