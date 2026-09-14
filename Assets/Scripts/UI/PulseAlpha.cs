using UnityEngine;

/// <summary>
/// Gentle alpha pulse for "Press any key" style prompts. Unscaled time.
/// (Deliberately NOT used on the epilepsy warning screen, which must stay static.)
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PulseAlpha : MonoBehaviour
{
    [SerializeField] private float minAlpha = 0.35f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private float period = 1.6f;

    private CanvasGroup _group;

    private void Awake() => _group = GetComponent<CanvasGroup>();

    private void Update()
    {
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / period);
        _group.alpha = Mathf.Lerp(minAlpha, maxAlpha, k);
    }
}
