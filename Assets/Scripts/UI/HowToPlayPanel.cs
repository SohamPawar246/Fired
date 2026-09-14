using TMPro;
using UnityEngine;

/// <summary>
/// "How to Play / Controls" panel, reachable from both the Main Menu and the
/// Pause Menu. The text lives in the constant below so it's one edit when the
/// real controls exist — or assign the contentText in the inspector and type
/// straight into the scene.
/// </summary>
public class HowToPlayPanel : MonoBehaviour
{
    /// <summary>PLACEHOLDER — replace with the real controls during the jam.
    /// Supports TMP rich text.</summary>
    public const string PlaceholderText =
        "<color=#00F0FF>You are the bullet. You never stop flying.</color>\n\n" +
        "<b>MOUSE</b>          steer\n" +
        "<b>RIGHT MOUSE</b>    deadeye — slow time to line up a shot\n" +
        "<b>ESC / P</b>        pause\n\n" +
        "Fly through glowing <color=#00F0FF>cores</color> to refuel your speed.\n" +
        "Graze a wall at a shallow angle to <color=#FFE600>ricochet</color>.\n" +
        "Hit a wall head-on and you're done.\n" +
        "Slow to a crawl and you fall out of the air.\n\n" +
        "<color=#FF2A6D>Stay fast. Stay flying.</color>";

    [SerializeField] private TMP_Text contentText;

    private void OnEnable()
    {
        // Only overwrite if nobody customized the scene text.
        if (contentText != null && string.IsNullOrWhiteSpace(contentText.text))
            contentText.text = PlaceholderText;
    }

    public void OnBack()
    {
        AudioManager.Instance.PlayUIBack();
        gameObject.SetActive(false);
    }
}
