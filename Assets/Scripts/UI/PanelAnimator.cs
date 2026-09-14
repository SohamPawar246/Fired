using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pop-in animation for panels and overlays: the whole panel fades in, while the
/// plate (and everything else except the full-screen "Dim") scales up slightly.
/// The dim must NOT scale — shrinking a full-screen black overlay flashes an
/// undimmed border for a frame or two.
/// Unscaled time, so it works while the game is paused. Added automatically by
/// the scaffold builder to every modal panel.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PanelAnimator : MonoBehaviour
{
    [SerializeField] private float duration = 0.16f;
    [SerializeField] private float startScale = 0.92f;

    private CanvasGroup _group;
    private readonly List<Transform> _scaleTargets = new();

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        foreach (Transform child in transform)
            if (child.name != "Dim") _scaleTargets.Add(child);
    }

    private void OnEnable() => StartCoroutine(PopIn());

    private void OnDisable()
    {
        // Leave everything reset so re-enabling starts clean.
        SetScale(1f);
        if (_group != null) _group.alpha = 1f;
    }

    private void SetScale(float s)
    {
        for (int i = 0; i < _scaleTargets.Count; i++)
            _scaleTargets[i].localScale = Vector3.one * s;
    }

    private IEnumerator PopIn()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = 1f - (1f - k) * (1f - k); // ease-out
            _group.alpha = k;
            SetScale(Mathf.Lerp(startScale, 1f, k));
            yield return null;
        }
        _group.alpha = 1f;
        SetScale(1f);
    }
}
