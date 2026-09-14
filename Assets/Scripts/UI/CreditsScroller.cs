using TMPro;
using UnityEngine;

/// <summary>
/// Classic vertical credits crawl. THE CONTENT IS NOT IN THE SCENE — it is read
/// from Assets/Resources/Credits.txt at runtime, so teammates add their name by
/// editing one text file (TMP rich text like <b> and <size=...> works there).
///
/// Skippable (any key, after a short debounce) and returns to MainMenu when the
/// text has fully scrolled past the top.
/// </summary>
public class CreditsScroller : MonoBehaviour
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private RectTransform content;   // moving container
    [SerializeField] private TMP_Text creditsText;    // single rich-text block

    [Header("Tuning")]
    [SerializeField] private float scrollSpeed = 80f; // units (≈px at 1080p) per second
    [SerializeField] private float inputDebounceSeconds = 0.5f;

    private float _elapsed;
    private bool _leaving;
    private float _endY; // content y position at which the crawl is finished

    private void Start()
    {
        var textAsset = Resources.Load<TextAsset>("Credits");
        creditsText.text = textAsset != null
            ? textAsset.text
            : "Credits file missing!\n(Assets/Resources/Credits.txt)";

        // Let TMP measure the text, then start the crawl just below the screen.
        creditsText.ForceMeshUpdate();
        float textHeight = creditsText.preferredHeight;
        content.sizeDelta = new Vector2(content.sizeDelta.x, textHeight);

        float screenHalf = ((RectTransform)content.parent).rect.height * 0.5f;
        content.anchoredPosition = new Vector2(0f, -(screenHalf + textHeight * 0.5f));
        _endY = screenHalf + textHeight * 0.5f;
    }

    private void Update()
    {
        if (_leaving) return;
        _elapsed += Time.unscaledDeltaTime;

        content.anchoredPosition += Vector2.up * (scrollSpeed * Time.unscaledDeltaTime);

        bool finished = content.anchoredPosition.y >= _endY;
        bool skipped = _elapsed > inputDebounceSeconds && JamInput.AnyPressThisFrame();
        if (finished || skipped)
        {
            _leaving = true;
            GameManager.Instance.SceneFlow.LoadScene("MainMenu");
        }
    }
}
