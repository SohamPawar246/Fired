using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cartoon-cyberpunk menu backdrop, generated 100% in code:
///   - a striped synthwave sun (yellow→pink) low on the sky
///   - three parallax layers of neon city skyline, windows lit in the theme neons,
///     scrolling endlessly (seamless textures)
///   - hover-traffic light streaks sliding across between the layers
///
/// Pairs with StarfieldBackdrop (stars/nebula/shooting stars) rendered behind it.
/// This object is the marked slot for real key art later.
/// </summary>
public class CyberCityBackdrop : MonoBehaviour
{
    [SerializeField] private float parallaxSpeed = 0.005f; // uv offset/sec, nearest layer
    [SerializeField] private int trafficCount = 9;

    private struct Streak
    {
        public RectTransform Rt;
        public Image Img;
        public float Speed;   // px/sec, sign = direction
        public float Y;
    }

    private readonly List<RawImage> _skylines = new();
    private readonly List<float> _skylineSpeeds = new();
    private readonly List<Streak> _traffic = new();
    private RectTransform _area;

    private void Awake()
    {
        _area = (RectTransform)transform;
        BuildSun();
        BuildSkylines();
        BuildTraffic();
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        for (int i = 0; i < _skylines.Count; i++)
        {
            var r = _skylines[i].uvRect;
            r.x = (t * _skylineSpeeds[i]) % 1f;
            _skylines[i].uvRect = r;
        }

        Rect area = _area.rect;
        float half = area.width * 0.5f + 60f;
        for (int i = 0; i < _traffic.Count; i++)
        {
            var s = _traffic[i];
            var p = s.Rt.anchoredPosition;
            p.x += s.Speed * Time.unscaledDeltaTime;
            if (p.x > half) p.x = -half;
            if (p.x < -half) p.x = half;
            s.Rt.anchoredPosition = new Vector2(p.x, s.Y);
        }
    }

    // ---- Synthwave sun -----------------------------------------------------------

    private void BuildSun()
    {
        var go = new GameObject("SynthSun", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchoredPosition = new Vector2(430, 60);
        rt.sizeDelta = new Vector2(440, 440);
        var img = go.GetComponent<Image>();
        img.sprite = MakeSunSprite();
        img.raycastTarget = false;

        // Soft glow halo behind it.
        var halo = new GameObject("SunHalo", typeof(RectTransform), typeof(Image));
        var hrt = (RectTransform)halo.transform;
        hrt.SetParent(transform, false);
        hrt.anchoredPosition = rt.anchoredPosition;
        hrt.sizeDelta = new Vector2(760, 760);
        var himg = halo.GetComponent<Image>();
        himg.sprite = MakeRadial(96, 2.4f);
        himg.color = new Color(1f, 0.3f, 0.55f, 0.18f);
        himg.raycastTarget = false;
        halo.transform.SetSiblingIndex(go.transform.GetSiblingIndex()); // halo behind disc
    }

    /// <summary>Classic synthwave disc: yellow→pink vertical gradient with horizontal
    /// gaps that widen toward the bottom. Colors baked in (Image color stays white).</summary>
    private static Sprite MakeSunSprite()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 c = new(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
        float radius = size * 0.5f - 2f;

        for (int y = 0; y < size; y++)
        {
            float k = y / (float)size; // 0 bottom → 1 top
            Color rowColor = Color.Lerp(Theme.NeonPink, Theme.NeonYellow, k);

            // Stripe gaps in the lower half: thicker gaps nearer the bottom.
            bool inGap = false;
            if (k < 0.55f)
            {
                float band = Mathf.Lerp(18f, 6f, k / 0.55f); // band period shrinks upward
                float gap = Mathf.Lerp(9f, 2f, k / 0.55f);   // gap thickness shrinks upward
                inGap = (y % (int)band) < gap;
            }

            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float alpha = Mathf.Clamp01(radius - d);
                if (inGap) alpha = 0f;
                tex.SetPixel(x, y, new Color(rowColor.r, rowColor.g, rowColor.b, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ---- Skylines ---------------------------------------------------------------

    private void BuildSkylines()
    {
        // Far → near: shorter/dimmer behind, taller/louder in front. Silhouettes
        // sit clearly above the sky color so buildings read as SHAPES; windows are
        // sparse accents, not confetti.
        AddSkyline("SkylineFar", seed: 11, height: 300, maxBuildingH: 0.62f,
            silhouette: Theme.FromHex("#2B1656"), windowAlpha: 0.35f, speedScale: 0.25f);
        AddSkyline("SkylineMid", seed: 23, height: 250, maxBuildingH: 0.75f,
            silhouette: Theme.FromHex("#1E0E42"), windowAlpha: 0.6f, speedScale: 0.55f);
        AddSkyline("SkylineNear", seed: 37, height: 195, maxBuildingH: 0.9f,
            silhouette: Theme.FromHex("#130931"), windowAlpha: 0.9f, speedScale: 1f);
    }

    private void AddSkyline(string name, int seed, float height, float maxBuildingH,
        Color silhouette, float windowAlpha, float speedScale)
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
        raw.texture = MakeSkylineTexture(seed, maxBuildingH, silhouette, windowAlpha);
        raw.color = Color.white; // colors are baked into the texture (windows differ in hue)
        raw.raycastTarget = false;

        _skylines.Add(raw);
        _skylineSpeeds.Add(parallaxSpeed * speedScale);
    }

    /// <summary>1024x256 seamless skyline: random building blocks with lit windows
    /// in the theme neons. Baked colors so windows keep their hue.</summary>
    private static Texture2D MakeSkylineTexture(int seed, float maxBuildingH,
        Color silhouette, float windowAlpha)
    {
        const int w = 1024, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        var rng = new System.Random(seed);
        var px = new Color32[w * h];
        var clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < px.Length; i++) px[i] = clear;

        Color32 sil = silhouette;
        Color[] windowColors = { Theme.NeonCyan, Theme.NeonPink, Theme.NeonYellow };

        int x = 0;
        while (x < w)
        {
            int bw = rng.Next(28, 86);
            if (x + bw > w) bw = w - x; // last building flush to the edge (seamless enough)
            int bh = (int)(h * (0.18 + rng.NextDouble() * (maxBuildingH - 0.18)));

            // Silhouette block.
            for (int bx = x; bx < x + bw; bx++)
                for (int by = 0; by < bh; by++)
                    px[by * w + bx] = sil;

            // Antenna on some tall buildings.
            if (bh > h * 0.55f && rng.NextDouble() < 0.4)
            {
                int ax = x + bw / 2;
                for (int ay = bh; ay < Mathf.Min(h, bh + 22); ay++) px[ay * w + ax] = sil;
            }

            // Windows: grid inside the block, sparse random lights. Ground floors
            // (wy < 26) stay dark so the footer UI area keeps contrast.
            for (int wy = 26; wy < bh - 6; wy += 12)
                for (int wx = x + 5; wx < x + bw - 5; wx += 10)
                {
                    if (rng.NextDouble() > 0.13) continue;
                    var wc = windowColors[rng.Next(windowColors.Length)];
                    var col = new Color(wc.r, wc.g, wc.b, windowAlpha);
                    for (int dy = 0; dy < 4; dy++)
                        for (int dx = 0; dx < 4; dx++)
                            px[(wy + dy) * w + (wx + dx)] = col;
                }

            x += bw;
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ---- Traffic streaks -----------------------------------------------------------

    private void BuildTraffic()
    {
        Rect r = _area.rect;
        for (int i = 0; i < trafficCount; i++)
        {
            var go = new GameObject("Traffic", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(Random.Range(20f, 42f), 3f);

            float y = Random.Range(-r.height * 0.5f + 210f, -r.height * 0.5f + 430f);
            bool leftToRight = Random.value < 0.5f;
            rt.anchoredPosition = new Vector2(Random.Range(-r.width * 0.5f, r.width * 0.5f), y);

            var img = go.GetComponent<Image>();
            img.sprite = MakeRadial(16, 1.2f);
            var col = leftToRight ? Theme.NeonCyan : Theme.NeonPink; // like tail/head lights
            col.a = Random.Range(0.5f, 0.9f);
            img.color = col;
            img.raycastTarget = false;

            _traffic.Add(new Streak
            {
                Rt = rt,
                Img = img,
                Speed = (leftToRight ? 1f : -1f) * Random.Range(50f, 150f),
                Y = y
            });
        }
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
