using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The comic-book style popup (GDD §3.4): a starburst that pops in at a screen
/// position with a label and a number counting up 0 → score, then pops out.
///
/// This is a PREFAB — Assets/Prefabs/UI/ComicPopup.prefab. Open it and restyle
/// the burst sprite, fonts, sizes and colours in the Inspector; nothing about its
/// look lives in this file any more. FiredGameController instantiates it onto the
/// HUD canvas and calls Play().
///
/// Runs on unscaled time so it stays readable through the score-hit slow-mo.
/// </summary>
public class ComicPopup : MonoBehaviour
{
    [Header("Parts (wired in the prefab)")]
    [SerializeField] private Image burst;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text num;

    [Header("Animation")]
    [Tooltip("Seconds the popup lives for, on unscaled time.")]
    [SerializeField] private float life = 0.9f;
    [Tooltip("Scale at the peak of the pop-in.")]
    [SerializeField] private float overshoot = 1.15f;
    [Tooltip("Random tilt applied once, in degrees.")]
    [SerializeField] private float maxTilt = 8f;

    private int _value;

    /// <summary>Show a score popup. A negative value means "label only" (warnings).</summary>
    public void Play(string labelText, int value, Color color)
    {
        _value = value;

        if (label != null)
        {
            label.text = labelText;
            label.color = color;
        }
        if (num != null) num.gameObject.SetActive(value >= 0);
        if (burst != null) burst.gameObject.SetActive(true);

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float t = 0f;
        float rotZ = Random.Range(-maxTilt, maxTilt);
        float span = Mathf.Max(0.01f, life);

        while (t < span)
        {
            t += Time.unscaledDeltaTime;
            float k = t / span;

            float pop = k < 0.25f
                ? Mathf.Lerp(0f, overshoot, k / 0.25f)
                : Mathf.Lerp(overshoot, k > 0.8f ? 0.7f : 1f, (k - 0.25f) / 0.75f);
            transform.localScale = Vector3.one * pop;
            transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);

            if (_value >= 0 && num != null)
                num.text = "+" + Mathf.FloorToInt(Mathf.Lerp(0f, _value, Mathf.Clamp01(k / 0.5f)));

            yield return null;
        }
        Destroy(gameObject);
    }
}
