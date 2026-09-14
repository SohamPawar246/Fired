using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The landscape half of the menu backdrop — what turns "dots on a gradient"
/// into an actual place. Generated entirely in code:
///   - a pale moon with baked craters and a soft halo
///   - a warm fog glow sitting on the horizon
///   - three layers of mountain silhouettes, each an endlessly-scrolling
///     seamless ridge texture (parallax: far layers drift slower)
///   - fireflies wandering above the ridgeline
///
/// Pairs with StarfieldBackdrop (stars/nebula/shooting stars) behind it.
/// Replace both with real key art when it exists.
/// </summary>
public class NightScenery : MonoBehaviour
{
    [Header("Mountains")]
    [SerializeField] private float parallaxSpeed = 0.004f; // uv offset per second, nearest layer

    [Header("Fireflies")]
    [SerializeField] private int fireflyCount = 10;

    private struct Firefly
    {
        public RectTransform Rt;
        public Image Img;
        public Vector2 Base;
        public float PhaseX, PhaseY, Speed;
    }

    private readonly List<RawImage> _ridges = new();
    private readonly List<float> _ridgeSpeeds = new();
    private readonly List<Firefly> _flies = new();
    private RectTransform _area;

    private void Awake()
    {
        _area = (RectTransform)transform;
        BuildMoon();
        BuildHorizonGlow();
        BuildMountains();
        BuildFireflies();
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        // Parallax drift: each ridge scrolls its UVs; textures are seamless.
        for (int i = 0; i < _ridges.Count; i++)
        {
            var r = _ridges[i].uvRect;
            r.x = (t * _ridgeSpeeds[i]) % 1f;
            _ridges[i].uvRect = r;
        }

        for (int i = 0; i < _flies.Count; i++)
        {
            var f = _flies[i];
            // Slow Lissajous wander + soft blink.
            f.Rt.anchoredPosition = f.Base + new Vector2(
                Mathf.Sin(t * 0.31f * f.Speed + f.PhaseX) * 46f,
                Mathf.Sin(t * 0.47f * f.Speed + f.PhaseY) * 28f);
            var c = f.Img.color;
            c.a = 0.25f + 0.55f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 1.3f * f.Speed + f.PhaseY), 2f);
            f.Img.color = c;
        }
    }

    // ---- Moon --------------------------------------------------------------------

    private void BuildMoon()
    {
        // Halo first (behind the disc).
        var halo = new GameObject("MoonHalo", typeof(RectTransform), typeof(Image));
        var haloRt = Place(halo, new Vector2(560, 240), new Vector2(340, 340));
        var haloImg = halo.GetComponent<Image>();
        haloImg.sprite = MakeRadial(96, 2.4f);
        haloImg.color = new Color(0.92f, 0.9f, 1f, 0.20f);
        haloImg.raycastTarget = false;

        var moon = new GameObject("Moon", typeof(RectTransform), typeof(Image));
        Place(moon, new Vector2(560, 240), new Vector2(110, 110));
        var img = moon.GetComponent<Image>();
        img.sprite = MakeMoonSprite();
        img.color = Theme.FromHex("#E9E6F2");
        img.raycastTarget = false;
    }

    /// <summary>Anti-aliased disc with a few darker crater blotches baked in.</summary>
    private static Sprite MakeMoonSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 c = new(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
        float radius = size * 0.5f - 2f;

        // A handful of fixed craters (deterministic — same moon every launch).
        Vector3[] craters = {
            new(-18, 14, 11), new(10, -20, 14), new(24, 18, 8),
            new(-30, -8, 7), new(2, 30, 6), new(-6, -2, 5)
        };

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float alpha = Mathf.Clamp01(radius - d); // 1px AA edge
                float shade = 1f;
                foreach (var cr in craters)
                {
                    float cd = Vector2.Distance(new Vector2(x, y), c + new Vector2(cr.x, cr.y));
                    if (cd < cr.z) shade = Mathf.Min(shade, 0.82f + 0.18f * (cd / cr.z));
                }
                // Slight top-left light falloff so the disc isn't flat.
                float lightK = 1f - 0.15f * Mathf.Clamp01((x - y + size) / (2f * size));
                tex.SetPixel(x, y, new Color(shade * lightK, shade * lightK, shade * lightK, alpha));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ---- Horizon fog glow -----------------------------------------------------------

    private void BuildHorizonGlow()
    {
        var glow = new GameObject("HorizonGlow", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)glow.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 190);
        rt.sizeDelta = new Vector2(2600, 620);
        var img = glow.GetComponent<Image>();
        img.sprite = MakeRadial(96, 2.2f);
        img.color = Theme.FromHex("#FFB4541A"); // warm haze where sky meets ridges
        img.raycastTarget = false;
    }

    // ---- Mountains -----------------------------------------------------------------

    private void BuildMountains()
    {
        // Far → near: lighter/smaller ridges first, darker/taller in front.
        AddRidge("RidgeFar", seed: 3, height: 320, baseline: 0.42f, amplitude: 0.30f,
            color: Theme.FromHex("#2A2E5C"), speedScale: 0.25f);
        AddRidge("RidgeMid", seed: 7, height: 260, baseline: 0.40f, amplitude: 0.38f,
            color: Theme.FromHex("#1E2148"), speedScale: 0.55f);
        AddRidge("RidgeNear", seed: 12, height: 200, baseline: 0.36f, amplitude: 0.46f,
            color: Theme.FromHex("#121430"), speedScale: 1f);
    }

    private void AddRidge(string name, int seed, float height, float baseline, float amplitude,
        Color color, float speedScale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, height);

        var raw = go.GetComponent<RawImage>();
        raw.texture = MakeRidgeTexture(seed, baseline, amplitude);
        raw.color = color;
        raw.raycastTarget = false;

        _ridges.Add(raw);
        _ridgeSpeeds.Add(parallaxSpeed * speedScale);
    }

    /// <summary>
    /// 1024x256 silhouette: solid below a ridgeline built from summed sines.
    /// Every sine completes whole cycles across the texture, so it tiles
    /// seamlessly when the UV rect scrolls.
    /// </summary>
    private static Texture2D MakeRidgeTexture(int seed, float baseline, float amplitude)
    {
        const int w = 1024, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat
        };

        var rng = new System.Random(seed);
        // 5 octaves; integer cycle counts keep the left/right edges continuous.
        int[] cycles = { 2, 3, 5, 9, 17 };
        float[] weights = { 0.42f, 0.28f, 0.16f, 0.09f, 0.05f };
        float[] phases = new float[cycles.Length];
        for (int i = 0; i < phases.Length; i++) phases[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);

        var colors = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            float n = 0f;
            for (int i = 0; i < cycles.Length; i++)
                n += Mathf.Sin(2f * Mathf.PI * cycles[i] * x / w + phases[i]) * weights[i];
            // Sharpen valleys into peaks a touch (|n| folds sines into ridges).
            n = Mathf.Abs(n);
            float ridgeY = (baseline + amplitude * n) * h;

            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Clamp01(ridgeY - y); // AA at the ridgeline
                colors[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(colors);
        tex.Apply();
        return tex;
    }

    // ---- Fireflies -----------------------------------------------------------------

    private void BuildFireflies()
    {
        Rect r = _area.rect;
        for (int i = 0; i < fireflyCount; i++)
        {
            var go = new GameObject("Firefly", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            float size = Random.Range(4f, 7f);
            rt.sizeDelta = new Vector2(size, size);

            var basePos = new Vector2(
                Random.Range(-r.width * 0.5f, r.width * 0.5f),
                Random.Range(-r.height * 0.5f + 140f, -r.height * 0.5f + 420f)); // above the ridgeline
            rt.anchoredPosition = basePos;

            var img = go.GetComponent<Image>();
            img.sprite = MakeRadial(16, 2f);
            img.color = Theme.Accent;
            img.raycastTarget = false;

            _flies.Add(new Firefly
            {
                Rt = rt,
                Img = img,
                Base = basePos,
                PhaseX = Random.value * Mathf.PI * 2f,
                PhaseY = Random.value * Mathf.PI * 2f,
                Speed = Random.Range(0.7f, 1.5f)
            });
        }
    }

    // ---- Shared helpers ---------------------------------------------------------------

    private RectTransform Place(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Sprite MakeRadial(int size, float power)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 c = new(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), power)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
