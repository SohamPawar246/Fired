using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated menu backdrop, generated 100% in code — no textures, shaders, or
/// particles, so it behaves identically on WebGL:
///   - vertical indigo gradient
///   - 2–3 huge, slowly drifting "nebula" blobs (soft radial sprites, low alpha)
///   - two parallax layers of twinkling stars (far = small/slow, near = big/fast)
///   - the occasional shooting star streaking across
///
/// This is the "clearly marked slot for real art": when key art exists, replace
/// this object with a full-screen Image and delete/disable this component.
/// </summary>
public class StarfieldBackdrop : MonoBehaviour
{
    [Header("Stars")]
    [SerializeField] private int farStarCount = 60;
    [SerializeField] private int nearStarCount = 26;
    [SerializeField] private float driftSpeed = 12f;
    [SerializeField] private float twinklePeriod = 3f;

    [Header("Shooting stars")]
    [SerializeField] private Vector2 shootIntervalRange = new(5f, 13f);

    private struct Star
    {
        public RectTransform Rt;
        public Image Img;
        public float Speed;
        public float Phase;
        public float BaseAlpha;
    }

    private struct Blob
    {
        public RectTransform Rt;
        public Vector2 BasePos;
        public Vector2 Orbit;      // how far it wanders
        public float Period;
        public float Phase;
        public float SpinSpeed;
    }

    private readonly List<Star> _stars = new();
    private readonly List<Blob> _blobs = new();
    private RectTransform _area;
    private RectTransform _shootingStar;
    private Image _shootingImg;
    private float _nextShootAt;
    private float _shootT = 1f; // >=1 means idle
    private Vector2 _shootFrom, _shootTo;

    private static Sprite _dotSprite;
    private static Sprite _glowSprite;

    private void Awake()
    {
        _area = (RectTransform)transform;
        BuildGradient();
        BuildNebula();
        BuildStars();
        BuildShootingStar();
        _nextShootAt = Time.unscaledTime + Random.Range(2f, shootIntervalRange.y);
    }

    private void Update()
    {
        Rect r = _area.rect;
        float t = Time.unscaledTime;

        // Stars: drift upward, wrap, twinkle.
        for (int i = 0; i < _stars.Count; i++)
        {
            var s = _stars[i];
            var p = s.Rt.anchoredPosition;
            p.y += driftSpeed * s.Speed * Time.unscaledDeltaTime;
            if (p.y > r.height * 0.5f + 12f)
            {
                p.y = -r.height * 0.5f - 12f;
                p.x = Random.Range(-r.width * 0.5f, r.width * 0.5f);
            }
            s.Rt.anchoredPosition = p;

            float k = 0.5f + 0.5f * Mathf.Sin((t + s.Phase) * 2f * Mathf.PI / twinklePeriod);
            var c = s.Img.color;
            c.a = s.BaseAlpha * Mathf.Lerp(0.35f, 1f, k);
            s.Img.color = c;
        }

        // Nebula blobs: slow orbital wander + spin + breathe.
        for (int i = 0; i < _blobs.Count; i++)
        {
            var b = _blobs[i];
            float w = t * 2f * Mathf.PI / b.Period + b.Phase;
            b.Rt.anchoredPosition = b.BasePos + new Vector2(Mathf.Sin(w) * b.Orbit.x, Mathf.Cos(w * 0.8f) * b.Orbit.y);
            b.Rt.localRotation = Quaternion.Euler(0, 0, t * b.SpinSpeed);
            b.Rt.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(w * 0.5f));
        }

        UpdateShootingStar(r, t);
    }

    // ---- Shooting star ---------------------------------------------------------

    private void UpdateShootingStar(Rect r, float t)
    {
        if (_shootT >= 1f)
        {
            if (t < _nextShootAt) return;
            // Launch: pick a diagonal path across the upper half of the screen.
            float y0 = Random.Range(r.height * 0.05f, r.height * 0.45f);
            bool leftToRight = Random.value < 0.5f;
            float x0 = leftToRight ? -r.width * 0.55f : r.width * 0.55f;
            float x1 = -x0;
            _shootFrom = new Vector2(x0, y0);
            _shootTo = new Vector2(x1, y0 - Random.Range(80f, 220f));
            _shootT = 0f;
            var dir = (_shootTo - _shootFrom).normalized;
            _shootingStar.localRotation = Quaternion.FromToRotation(Vector3.right, dir);
            _shootingStar.gameObject.SetActive(true);
        }

        _shootT += Time.unscaledDeltaTime / 0.9f; // ~0.9s crossing
        _shootingStar.anchoredPosition = Vector2.Lerp(_shootFrom, _shootTo, _shootT);
        // Bright in the middle of the flight, faded at both ends.
        float a = Mathf.Sin(Mathf.Clamp01(_shootT) * Mathf.PI);
        var c = _shootingImg.color; c.a = a * 0.85f; _shootingImg.color = c;

        if (_shootT >= 1f)
        {
            _shootingStar.gameObject.SetActive(false);
            _nextShootAt = Time.unscaledTime + Random.Range(shootIntervalRange.x, shootIntervalRange.y);
        }
    }

    // ---- Construction ------------------------------------------------------------

    private void BuildGradient()
    {
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            tex.SetPixel(0, y, Color.Lerp(Theme.Background, Theme.BackgroundHi, y / 63f));
        tex.Apply();

        var go = new GameObject("Gradient", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f));
        img.raycastTarget = false;
        go.transform.SetAsFirstSibling();
    }

    private void BuildNebula()
    {
        Rect r = _area.rect;
        var colors = new[] { Theme.NebulaPurple, Theme.NebulaMagenta, Theme.NebulaTeal };
        for (int i = 0; i < colors.Length; i++)
        {
            var go = new GameObject($"Nebula{i}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            float size = Random.Range(900f, 1500f);
            rt.sizeDelta = new Vector2(size, size * Random.Range(0.6f, 0.9f));
            var basePos = new Vector2(
                Random.Range(-r.width * 0.35f, r.width * 0.35f),
                Random.Range(-r.height * 0.3f, r.height * 0.35f));
            rt.anchoredPosition = basePos;

            var img = go.GetComponent<Image>();
            img.sprite = GetGlowSprite();
            img.color = colors[i];
            img.raycastTarget = false;

            _blobs.Add(new Blob
            {
                Rt = rt,
                BasePos = basePos,
                Orbit = new Vector2(Random.Range(40f, 120f), Random.Range(30f, 80f)),
                Period = Random.Range(20f, 40f),
                Phase = Random.value * Mathf.PI * 2f,
                SpinSpeed = Random.Range(-1.5f, 1.5f)
            });
        }
    }

    private void BuildStars()
    {
        BuildStarLayer(farStarCount, 1.5f, 3f, 0.2f, 0.55f, 0.35f);  // far: small, dim, slow
        BuildStarLayer(nearStarCount, 3f, 6f, 0.35f, 0.9f, 1.1f);    // near: bigger, brighter, faster
    }

    private void BuildStarLayer(int count, float minSize, float maxSize,
        float minAlpha, float maxAlpha, float speedScale)
    {
        Rect r = _area.rect;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);

            float size = Random.Range(minSize, maxSize);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(
                Random.Range(-r.width * 0.5f, r.width * 0.5f),
                Random.Range(-r.height * 0.5f, r.height * 0.5f));

            var img = go.GetComponent<Image>();
            img.sprite = GetDotSprite();
            img.raycastTarget = false;
            float baseAlpha = Random.Range(minAlpha, maxAlpha);
            var col = Random.value < 0.16f ? Theme.Accent : Theme.Text;
            col.a = baseAlpha;
            img.color = col;

            _stars.Add(new Star
            {
                Rt = rt,
                Img = img,
                Speed = Random.Range(0.5f, 1.4f) * speedScale,
                Phase = Random.Range(0f, twinklePeriod),
                BaseAlpha = baseAlpha
            });
        }
    }

    private void BuildShootingStar()
    {
        var go = new GameObject("ShootingStar", typeof(RectTransform), typeof(Image));
        _shootingStar = (RectTransform)go.transform;
        _shootingStar.SetParent(transform, false);
        _shootingStar.sizeDelta = new Vector2(140f, 3.5f); // stretched dot = streak
        _shootingImg = go.GetComponent<Image>();
        _shootingImg.sprite = GetDotSprite();
        _shootingImg.color = Theme.Text;
        _shootingImg.raycastTarget = false;
        go.SetActive(false);
    }

    // ---- Shared procedural sprites -------------------------------------------------

    private static Sprite GetDotSprite()
    {
        if (_dotSprite != null) return _dotSprite;
        _dotSprite = MakeRadialSprite(16, 2f);
        return _dotSprite;
    }

    private static Sprite GetGlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;
        _glowSprite = MakeRadialSprite(128, 2.6f); // bigger + softer falloff
        return _glowSprite;
    }

    private static Sprite MakeRadialSprite(int size, float falloffPower)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), falloffPower);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
