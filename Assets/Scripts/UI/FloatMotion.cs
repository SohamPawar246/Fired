using UnityEngine;

/// <summary>
/// Gentle sinusoidal bob (and optional sway) for decorative elements — the main
/// menu title, glows, etc. Unscaled time; purely cosmetic.
/// </summary>
public class FloatMotion : MonoBehaviour
{
    [SerializeField] private float amplitude = 8f;     // vertical travel in UI units
    [SerializeField] private float period = 5f;        // seconds per full bob
    [SerializeField] private float swayDegrees = 0f;   // optional slow tilt

    private RectTransform _rt;
    private Vector2 _basePos;
    private float _phase;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _basePos = _rt.anchoredPosition;
        _phase = Random.value * Mathf.PI * 2f; // desync multiple floaters
    }

    private void Update()
    {
        float w = Time.unscaledTime * 2f * Mathf.PI / period + _phase;
        _rt.anchoredPosition = _basePos + Vector2.up * (Mathf.Sin(w) * amplitude);
        if (swayDegrees > 0f)
            _rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(w * 0.7f) * swayDegrees);
    }
}
