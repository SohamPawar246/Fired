using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ONE-TIME MIGRATION: turns the HUD that used to be built with `new GameObject(...)`
/// at runtime into real, serialized scene objects you can select, restyle and move
/// in the Inspector.
///
/// What it creates (all pixel-identical to what the code used to build):
///   Game.unity  Canvas/FirePrompt, Canvas/Crosshair, Canvas/DeadeyeBar,
///               Canvas/ComboText, Canvas/LetterboxTop, Canvas/LetterboxBottom,
///               GameOverScreen/Overlay/BestSnap (polaroid + photo + caption)
///   Prefab      Assets/Prefabs/UI/ComicPopup.prefab (the score starburst)
///
/// It then wires every one of them into FiredGameController / GameOverScreen, so the
/// runtime code only READS these references — it never constructs UI again.
///
/// Safe to re-run: existing baked objects are replaced in place and re-wired.
/// Anything you added as a CHILD of a baked object is destroyed on a re-bake, so
/// restyle the baked objects themselves rather than nesting new art under them.
/// </summary>
public static class RuntimeUiBaker
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";
    private const string PopupPrefabPath = "Assets/Prefabs/UI/ComicPopup.prefab";
    private const string ComicFontPath = "Assets/Art/Fonts/Bangers SDF.asset";
    private const string BurstSpritePath = "Assets/Art/UI/ComicBurst.png";
    private const string GlowSpritePath = "Assets/Art/UI/SoftGlow.png";
    private const string RoundedSpritePath = "Assets/Art/UI/RoundedRect.png";

    // Fully qualified: the project's runtime MenuItem UI class shadows the attribute.
    [UnityEditor.MenuItem("Tools/FIRED/Bake Runtime UI into Scenes")]
    public static void Bake()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[UiBaker] No 'Canvas' in Game.unity — aborting."); return; }

        var game = Object.FindFirstObjectByType<FiredGameController>();
        if (game == null) { Debug.LogError("[UiBaker] No FiredGameController in Game.unity — aborting."); return; }

        var over = Object.FindFirstObjectByType<GameOverScreen>(FindObjectsInactive.Include);

        BakeInto(canvas.transform, game, over);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[UiBaker] Baked the runtime HUD into Game.unity. Every HUD element is now a normal " +
                  "scene object — select it in the Hierarchy and restyle it in the Inspector.");
    }

    /// <summary>Bakes the flight HUD and the best-shot polaroid into an already-open
    /// scene and wires every reference. Shared by the menu item above and by
    /// JamScaffoldBuilder's Game-scene rebuild so the two can never drift apart.</summary>
    public static void BakeInto(Transform canvas, FiredGameController game, GameOverScreen over)
    {
        var comicFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ComicFontPath);
        if (comicFont == null)
            Debug.LogWarning($"[UiBaker] Comic font missing at {ComicFontPath} — baked text will use the TMP default.");

        var popupPrefab = BuildPopupPrefab(comicFont);

        var prompt = BakeFirePrompt(canvas, comicFont);
        var crosshair = BakeCrosshair(canvas);
        var deadeye = BakeDeadeyeBar(canvas, out var deadeyeFill);
        var combo = BakeComboText(canvas, comicFont);
        var letterTop = BakeLetterbox(canvas, "LetterboxTop", 1f);
        var letterBottom = BakeLetterbox(canvas, "LetterboxBottom", 0f);

        SetRef(game, "firePrompt", prompt);
        SetRef(game, "crosshair", crosshair.gameObject);
        SetRef(game, "deadeyeBar", deadeye.gameObject);
        SetRef(game, "deadeyeFill", deadeyeFill);
        SetRef(game, "comboText", combo);
        SetRef(game, "letterboxTop", letterTop);
        SetRef(game, "letterboxBottom", letterBottom);
        SetRef(game, "popupPrefab", popupPrefab.GetComponent<ComicPopup>());

        var best = BakeBestText(canvas);
        SetRef(game, "bestText", best);

        BakeSpeedGauge(canvas, game);
        FixLetterboxDrawOrder(canvas, letterTop, letterBottom);

        if (over == null) return;

        var overlay = over.transform.Find("Overlay");
        if (overlay == null)
        {
            Debug.LogWarning("[UiBaker] GameOverScreen has no 'Overlay' child — best-shot polaroid skipped.");
            return;
        }

        var title = overlay.Find("Title");
        var titleFont = title != null ? title.GetComponent<TMP_Text>()?.font : null;

        var snap = BakeBestSnap(overlay, titleFont, out var photo, out var caption);
        SetRef(over, "bestSnapRoot", snap.gameObject);
        SetRef(over, "bestSnapPhoto", photo);
        SetRef(over, "bestSnapCaption", caption);
    }

    // =====================================================================
    // HUD pieces
    // =====================================================================

    private static TMP_Text BakeFirePrompt(Transform parent, TMP_FontAsset font)
    {
        var go = Replace(parent, "FirePrompt");
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = "CLICK TO FIRE!";
        text.fontSize = 64;
        text.color = Theme.NeonYellow;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 200f);
        rt.sizeDelta = new Vector2(900f, 90f);

        go.AddComponent<CanvasGroup>();
        go.AddComponent<PulseAlpha>();
        go.SetActive(false);
        return text;
    }

    private static RectTransform BakeCrosshair(Transform parent)
    {
        var go = Replace(parent, "Crosshair");
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(64f, 64f);

        Bar(rt, "BarTop", new Vector2(2.5f, 26f), new Vector2(0f, 24f), Theme.NeonCyan);
        Bar(rt, "BarBottom", new Vector2(2.5f, 26f), new Vector2(0f, -24f), Theme.NeonCyan);
        Bar(rt, "BarRight", new Vector2(26f, 2.5f), new Vector2(24f, 0f), Theme.NeonCyan);
        Bar(rt, "BarLeft", new Vector2(26f, 2.5f), new Vector2(-24f, 0f), Theme.NeonCyan);
        Bar(rt, "Dot", new Vector2(4f, 4f), Vector2.zero, Theme.NeonPink);

        go.SetActive(false);
        return rt;
    }

    private static Image Bar(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform BakeDeadeyeBar(Transform parent, out Image fill)
    {
        var go = Replace(parent, "DeadeyeBar", typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 30f);
        rt.sizeDelta = new Vector2(700f, 10f);
        var bg = go.GetComponent<Image>();
        bg.color = Theme.FromHex("#20153E");
        bg.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(go.transform, false);
        var frt = (RectTransform)fillGo.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;

        fill = fillGo.GetComponent<Image>();
        // Purple, not yellow: the speed gauge above now passes THROUGH yellow in its
        // caution band, and two adjacent yellow bars read as one muddy block. Deadeye
        // is a resource, not a health bar, so it gets its own hue.
        fill.color = Theme.NeonPurple;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.raycastTarget = false;
        return rt;
    }

    private static TMP_Text BakeComboText(Transform parent, TMP_FontAsset font)
    {
        var go = Replace(parent, "ComboText");
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = "COMBO x2";
        text.fontSize = 52;
        text.color = Theme.NeonYellow;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.68f);
        rt.anchoredPosition = new Vector2(230f, 0f);
        rt.sizeDelta = new Vector2(420f, 70f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 6f);

        go.SetActive(false);
        return text;
    }

    /// <summary>Full-width cinematic bar pinned to the top (anchorY 1) or bottom (0).
    /// Height is driven at runtime, so it is baked at zero.</summary>
    private static RectTransform BakeLetterbox(Transform parent, string name, float anchorY)
    {
        var go = Replace(parent, name, typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, anchorY);
        rt.anchorMax = new Vector2(1f, anchorY);
        rt.pivot = new Vector2(0.5f, anchorY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        return rt;
    }

    /// <summary>The run's best slow-mo frame as a tilted polaroid: cream frame,
    /// 16:9 photo, caption strip. Photo texture is assigned at runtime.</summary>
    private static RectTransform BakeBestSnap(Transform parent, TMP_FontAsset font,
        out RawImage photo, out TMP_Text caption)
    {
        const float photoW = 440f, photoH = 247.5f, border = 14f, captionH = 52f;

        var go = Replace(parent, "BestSnap", typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-60f, 40f);
        rt.sizeDelta = new Vector2(photoW + border * 2f, photoH + border * 2f + captionH);
        rt.localRotation = Quaternion.Euler(0f, 0f, 4f);

        var frame = go.GetComponent<Image>();
        frame.color = new Color(0.96f, 0.95f, 0.92f);
        frame.raycastTarget = false;

        var photoGo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
        photoGo.transform.SetParent(go.transform, false);
        var prt = (RectTransform)photoGo.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0f, -border);
        prt.sizeDelta = new Vector2(photoW, photoH);
        photo = photoGo.GetComponent<RawImage>();
        photo.raycastTarget = false;

        var capGo = new GameObject("Caption", typeof(RectTransform));
        capGo.transform.SetParent(go.transform, false);
        caption = capGo.AddComponent<TextMeshProUGUI>();
        if (font != null) caption.font = font;
        caption.text = "BEST SHOT";
        caption.fontSize = 26;
        caption.color = new Color(0.12f, 0.1f, 0.16f);
        caption.alignment = TextAlignmentOptions.Center;
        caption.raycastTarget = false;
        var crt = (RectTransform)capGo.transform;
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 6f);
        crt.sizeDelta = new Vector2(0f, captionH - 10f);

        go.SetActive(false);
        return rt;
    }

    /// <summary>
    /// The record readout, tucked under the score in the top-right corner. Small and
    /// dim by design: it is a bar to clear, not a competing number — it only lights up
    /// (in code) at the moment the player beats it.
    /// </summary>
    private static TMP_Text BakeBestText(Transform parent)
    {
        var uiFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/ChakraPetch SDF.asset");

        var go = Replace(parent, "BestText");
        var text = go.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) text.font = uiFont;
        text.text = "BEST 0 m";
        text.fontSize = 26;
        text.color = Theme.TextDim;
        text.alignment = TextAlignmentOptions.TopRight;
        text.characterSpacing = 2f;
        text.raycastTarget = false;

        // Sits directly under Multiplier, which occupies y -86..-162 at this anchor.
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-44f, -170f);
        rt.sizeDelta = new Vector2(420f, 40f);
        return text;
    }

    // =====================================================================
    // Speed gauge
    // =====================================================================

    /// <summary>
    /// Dresses the existing SpeedBar into a readable instrument WITHOUT moving or
    /// resizing it — the designer's own position and size are left exactly as found.
    /// Adds, under SpeedBar: a danger wash over the low end, segment ticks, a soft
    /// glow and a bright leading cap. DeadeyeBar is widened to match SpeedBar so the
    /// two read as one instrument instead of two stray bars.
    /// </summary>
    private static void BakeSpeedGauge(Transform canvas, FiredGameController game)
    {
        var bar = canvas.Find("SpeedBar") as RectTransform;
        if (bar == null) { Debug.LogWarning("[UiBaker] No SpeedBar — gauge skipped."); return; }

        var fill = bar.Find("Fill")?.GetComponent<Image>();
        if (fill == null) { Debug.LogWarning("[UiBaker] SpeedBar has no Fill — gauge skipped."); return; }

        var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
        var roundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);

        float w = bar.rect.width;
        float h = bar.rect.height;

        // --- danger redline: a MARKING on the track, never a block in the fill's
        // place. It is deliberately a short stripe along the bottom edge: a
        // full-height red panel at the low end is indistinguishable from a red fill,
        // so at zero speed the gauge looked like it still held a quarter tank.
        var wash = Replace(bar, "DangerWash", typeof(Image));
        var wrt = (RectTransform)wash.transform;
        wrt.anchorMin = new Vector2(0f, 0f);
        wrt.anchorMax = new Vector2(0f, 0f);
        wrt.pivot = new Vector2(0f, 0f);
        wrt.anchoredPosition = new Vector2(0f, -3f);
        wrt.sizeDelta = new Vector2(w * 0.28f, 4f);
        var washImg = wash.GetComponent<Image>();
        washImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/ScrimH.png");
        washImg.color = new Color(1f, 0.22f, 0.39f, 0.45f);
        washImg.raycastTarget = false;

        // --- segment ticks: make change perceptible ---------------------------
        var ticks = Replace(bar, "Ticks");
        var trt = (RectTransform)ticks.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        for (int i = 1; i < 5; i++)
        {
            var tk = new GameObject($"Tick{i}", typeof(RectTransform), typeof(Image));
            tk.transform.SetParent(ticks.transform, false);
            var krt = (RectTransform)tk.transform;
            krt.anchorMin = new Vector2(i / 5f, 0f);
            krt.anchorMax = new Vector2(i / 5f, 1f);
            krt.pivot = new Vector2(0.5f, 0.5f);
            krt.offsetMin = new Vector2(-1f, 3f);
            krt.offsetMax = new Vector2(1f, -3f);
            var ti = tk.GetComponent<Image>();
            ti.color = new Color(0f, 0f, 0f, 0.38f);
            ti.raycastTarget = false;
        }

        // --- soft glow behind the leading edge (feeds the URP bloom) ----------
        var glow = Replace(bar, "CapGlow", typeof(Image));
        var grt = (RectTransform)glow.transform;
        grt.anchorMin = grt.anchorMax = new Vector2(0f, 0.5f);
        grt.pivot = new Vector2(0.5f, 0.5f);
        grt.sizeDelta = new Vector2(h * 5.5f, h * 5.5f);
        var glowImg = glow.GetComponent<Image>();
        glowImg.sprite = glowSprite;
        glowImg.color = new Color(0f, 0.94f, 1f, 0.5f);
        glowImg.raycastTarget = false;

        // --- the leading cap --------------------------------------------------
        var cap = Replace(bar, "Cap", typeof(Image));
        var crt = (RectTransform)cap.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(5f, h + 6f);
        var capImg = cap.GetComponent<Image>();
        capImg.sprite = roundSprite;
        capImg.type = Image.Type.Sliced;
        capImg.color = Color.white;
        capImg.raycastTarget = false;

        // Draw order inside the bar: fill, ticks, wash, glow, cap on top.
        fill.transform.SetSiblingIndex(0);
        ticks.transform.SetSiblingIndex(1);
        wash.transform.SetSiblingIndex(2);
        glow.transform.SetSiblingIndex(3);
        cap.transform.SetSiblingIndex(4);

        // --- align the deadeye meter to the speed bar -------------------------
        var de = canvas.Find("DeadeyeBar") as RectTransform;
        if (de != null)
        {
            de.sizeDelta = new Vector2(w, de.rect.height);
            de.anchoredPosition = new Vector2(bar.anchoredPosition.x, de.anchoredPosition.y);
        }

        var gauge = bar.GetComponent<SpeedGauge>();
        if (gauge == null) gauge = bar.gameObject.AddComponent<SpeedGauge>();
        SetRef(gauge, "track", bar);
        SetRef(gauge, "fill", fill);
        SetRef(gauge, "cap", crt);
        SetRef(gauge, "capGlow", glowImg);
        SetRef(gauge, "dangerWash", washImg);
        SetRef(game, "speedGauge", gauge);
    }

    /// <summary>
    /// The cinematic bars were drawn LAST, so at full height (8.5% of a 1080 canvas
    /// = 91.8u) they completely buried DeadeyeBar (30–40u) and SpeedBar (58–80u) —
    /// the speed readout vanished during exactly the beats where the player needs to
    /// see whether a hit paid out. Push them behind the HUD instead of moving the HUD,
    /// which would drag it toward the centre of the screen.
    /// </summary>
    private static void FixLetterboxDrawOrder(Transform canvas, RectTransform top, RectTransform bottom)
    {
        if (top != null) top.SetSiblingIndex(0);
        if (bottom != null) bottom.SetSiblingIndex(1);
    }

    // =====================================================================
    // ComicPopup prefab
    // =====================================================================

    private static GameObject BuildPopupPrefab(TMP_FontAsset font)
    {
        var burst = AssetDatabase.LoadAssetAtPath<Sprite>(BurstSpritePath);
        if (burst == null)
            Debug.LogWarning($"[UiBaker] {BurstSpritePath} is not imported as a Sprite — the popup " +
                             "starburst will render as a plain square. Set Texture Type = Sprite (2D and UI).");

        var root = new GameObject("ComicPopup", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(220f, 220f);

        var burstGo = new GameObject("Burst", typeof(RectTransform), typeof(Image));
        burstGo.transform.SetParent(root.transform, false);
        ((RectTransform)burstGo.transform).sizeDelta = new Vector2(190f, 190f);
        var burstImg = burstGo.GetComponent<Image>();
        burstImg.sprite = burst;
        burstImg.color = Theme.NeonYellow;
        burstImg.raycastTarget = false;

        var label = PopupText(root.transform, "Label", font, 36f, Theme.NeonYellow, new Vector2(0f, 16f));
        var num = PopupText(root.transform, "Num", font, 42f, Color.white, new Vector2(0f, -24f));
        num.text = "+0";

        var popup = root.AddComponent<ComicPopup>();
        SetRef(popup, "burst", burstImg);
        SetRef(popup, "label", label);
        SetRef(popup, "num", num);

        System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PopupPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static TMP_Text PopupText(Transform parent, string name, TMP_FontAsset font,
        float size, Color color, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(240f, 60f);
        rt.anchoredPosition = pos;
        return text;
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    /// <summary>Destroy any previous bake of this object and return a fresh one,
    /// keeping its sibling index so the Hierarchy order stays stable.</summary>
    private static GameObject Replace(Transform parent, string name, params System.Type[] components)
    {
        int index = -1;
        var existing = parent.Find(name);
        if (existing != null)
        {
            index = existing.GetSiblingIndex();
            Object.DestroyImmediate(existing.gameObject);
        }

        var types = new System.Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        components.CopyTo(types, 1);

        var go = new GameObject(name, types);
        go.transform.SetParent(parent, false);
        if (index >= 0) go.transform.SetSiblingIndex(index);
        return go;
    }

    private static void SetRef(Component target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[UiBaker] Field '{fieldName}' not found on {target.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
