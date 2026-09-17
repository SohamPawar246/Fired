using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Builds the entire pre-jam shell — scenes, prefabs, fonts, sprites, post-FX —
/// from code. Idempotent: run again to regenerate (overwrites manual scene edits).
///
///   Tools > Jam Scaffold > Build All Scenes
///
/// ART DIRECTION (based on how shipped games actually compose menus —
/// Hollow Knight / Celeste / Hades conventions):
///  - menus are TEXT, not boxed buttons; selection = amber label + triangle marker
///  - a real display typeface (Cinzel, OFL) + clean UI face (Manrope, OFL)
///  - the background is a place: moon, fog, parallax mountain ridges, fireflies,
///    stars/nebula/shooting stars (StarfieldBackdrop + NightScenery)
///  - footer bar with input hints; understated, standards-shaped epilepsy screen
///  - URP bloom/vignette/film grain over everything (canvases are camera-space)
/// </summary>
public static class JamScaffoldBuilder
{
    private const string ScenesDir = "Assets/Scenes";
    private const string ArtDir = "Assets/Art/UI";
    private const string FontDir = "Assets/Art/Fonts";

    private static Sprite _rounded;
    private static Sprite _softGlow;
    private static Sprite _triangle;     // right-pointing menu marker
    private static Sprite _warnTriangle; // warning icon (equilateral, filled)
    private static Sprite _burst;        // comic-book starburst (BANG!, style popups)
    private static Sprite _scrimH;       // left readability gradient (over menu video)
    private static Sprite _scrimV;       // bottom readability gradient (footer)
    private static VolumeProfile _postFx;
    private static TMP_FontAsset _displayFont; // Bungee — titles, menu items
    private static TMP_FontAsset _uiFont;      // Chakra Petch — body, labels, HUD
    private static TMP_FontAsset _comicFont;   // Bangers — comic bursts / onomatopoeia

    // Discovered video files (copied into StreamingAssets at build time).
    private static string _introVideoFile; // e.g. "StudioIntro.mp4" — null if none found
    private static string _menuVideoFile;  // e.g. "MenuLoop.mp4"   — null if none found

    private static bool _forceGame;

    /// <summary>
    /// Re-creates ONLY the generated UI sprites in Assets/Art/UI (rounded rect, soft
    /// glow, menu marker, warning triangle, scrims, comic burst). Touches no scene and
    /// no prefab, and each PNG keeps its existing .meta — so its GUID survives and every
    /// Image already pointing at it lights up again.
    ///
    /// Run this when the UI looks like plain rectangles: it means the PNGs are Git-LFS
    /// pointer stubs (a "Download ZIP" from GitHub, or a clone without `git lfs pull`)
    /// rather than real images, and Unity imported nothing.
    /// </summary>
    [UnityEditor.MenuItem("Tools/FIRED/Regenerate UI Sprites (safe — no scenes touched)")]
    public static void RegenerateArtOnly()
    {
        EnsureFolders();
        GenerateArtAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[JamScaffold] Regenerated the UI sprites in Assets/Art/UI. "
                  + "GUIDs are unchanged, so every scene and prefab picks them up automatically.");
    }

    [UnityEditor.MenuItem("Tools/FIRED/DANGER — Rebuild Game Scene (WIPES your level!)")]
    public static void ForceRebuildGameScene()
    {
        if (!EditorUtility.DisplayDialog(
                "Wipe Game.unity?",
                "This DELETES the hand-built level, the HUD you have restyled and every "
                + "Inspector tweak in Game.unity, then regenerates them from code.\n\n"
                + "There is no undo.",
                "Wipe and rebuild", "Cancel"))
            return;

        _forceGame = true;
        BuildAll(skipConfirm: true);
    }

    [UnityEditor.MenuItem("Tools/FIRED/DANGER — Regenerate Menu Scenes (overwrites edits)")] // fully qualified: the runtime MenuItem class shadows the attribute
    public static void BuildAll() => BuildAll(skipConfirm: false);

    /// <summary>
    /// Regenerates the shell scenes FROM CODE. The scenes — not this file — are the
    /// source of truth now: anything you change in the Inspector (moved buttons,
    /// recoloured labels, resized HUD, new objects) is DESTROYED by running this.
    /// It exists only to rebuild the shell from scratch, so it asks first.
    /// </summary>
    public static void BuildAll(bool skipConfirm)
    {
        if (!skipConfirm && !EditorUtility.DisplayDialog(
                "Overwrite the menu scenes?",
                "Boot, StudioIntro, EpilepsyWarning, MainMenu and Credits will be regenerated "
                + "from code, discarding every Inspector edit you made to them.\n\n"
                + "Game.unity is preserved. There is no undo.",
                "Overwrite", "Cancel"))
            return;

        EnsureFolders();
        EnableHdrOnPipeline();
        GenerateArtAssets();
        CreateFontAssets();
        DiscoverVideos();
        _postFx = CreatePostFxProfile();

        CreateGameManagerPrefab();
        var settingsPrefab = BuildSettingsPanelPrefab();
        var howToPrefab = BuildHowToPlayPanelPrefab();

        BuildBootScene();
        BuildStudioIntroScene();
        BuildEpilepsyScene();
        BuildMainMenuScene(settingsPrefab, howToPrefab);

        // THE GAME SCENE HOLDS HAND-BUILT LEVELS — never silently regenerate it.
        // It is only built if missing, or via the explicit destructive menu item.
        if (_forceGame || !File.Exists($"{ScenesDir}/Game.unity"))
            BuildGameScene(settingsPrefab, howToPrefab);
        else
            Debug.Log("[JamScaffold] Game.unity preserved (contains hand-built level). " +
                      "Use 'Tools > Jam Scaffold > Rebuild Game Scene' to force-regenerate it.");
        _forceGame = false;

        BuildCreditsScene();

        SetBuildSettings();
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene(ScenesDir + "/MainMenu.unity");
        Debug.Log("[JamScaffold] Build complete. Scenes: Boot > StudioIntro > EpilepsyWarning > MainMenu > Game > Credits");
    }

    // =====================================================================
    // Folders / pipeline / fonts
    // =====================================================================

    private static void EnsureFolders()
    {
        string[] dirs =
        {
            "Assets/Scenes", "Assets/Scripts", "Assets/Art", "Assets/Art/UI", "Assets/Art/Fonts",
            "Assets/Audio", "Assets/Prefabs", "Assets/Prefabs/UI", "Assets/Resources", "Assets/Settings"
        };
        foreach (var dir in dirs)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                var parent = dir.Substring(0, dir.LastIndexOf('/'));
                var leaf = dir.Substring(dir.LastIndexOf('/') + 1);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }

    /// <summary>
    /// Finds video files in the project by name convention and copies them into
    /// StreamingAssets (videos must play via URL — the only method WebGL supports).
    ///   "menu" / "loop" / "bg"        → main-menu looping background
    ///   "intro" / "studio" / "logo"   → studio intro
    ///   a single unclaimed video      → studio intro
    /// Re-run the builder after adding/renaming a video.
    /// </summary>
    private static void DiscoverVideos()
    {
        _introVideoFile = null;
        _menuVideoFile = null;

        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:VideoClip"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (!p.StartsWith("Assets/StreamingAssets")) paths.Add(p); // ignore our own copies
        }

        string introSrc = null, menuSrc = null;
        foreach (var p in paths)
        {
            var n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (menuSrc == null && (n.Contains("menu") || n.Contains("loop") || n.Contains("bg"))) menuSrc = p;
            else if (introSrc == null && (n.Contains("intro") || n.Contains("studio") || n.Contains("logo"))) introSrc = p;
        }
        // A single video with no matching keywords is assumed to be the intro.
        if (introSrc == null)
            introSrc = paths.Find(p => p != menuSrc);

        if (introSrc != null) _introVideoFile = CopyToStreamingAssets(introSrc, "StudioIntro");
        if (menuSrc != null) _menuVideoFile = CopyToStreamingAssets(menuSrc, "MenuLoop");

        Debug.Log($"[JamScaffold] Videos — intro: {(_introVideoFile ?? "none")}, menu loop: {(_menuVideoFile ?? "none")}");
    }

    private static string CopyToStreamingAssets(string assetPath, string baseName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/StreamingAssets"))
            AssetDatabase.CreateFolder("Assets", "StreamingAssets");

        string fileName = baseName + Path.GetExtension(assetPath).ToLowerInvariant();
        string dst = "Assets/StreamingAssets/" + fileName;
        File.Copy(assetPath, dst, overwrite: true);
        AssetDatabase.ImportAsset(dst);
        return fileName;
    }

    private static void EnableHdrOnPipeline()
    {
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            urp.supportsHDR = true;
            EditorUtility.SetDirty(urp);
        }
    }

    /// <summary>Creates TMP font assets from the downloaded TTFs (Bungee, Chakra
    /// Petch, Bangers — all SIL OFL, license files sit next to them). Dynamic
    /// atlases, with LiberationSans as fallback for any missing glyph.</summary>
    private static void CreateFontAssets()
    {
        _displayFont = MakeFontAsset($"{FontDir}/Bungee.ttf", $"{FontDir}/Bungee SDF.asset");
        _uiFont = MakeFontAsset($"{FontDir}/ChakraPetch.ttf", $"{FontDir}/ChakraPetch SDF.asset");
        _comicFont = MakeFontAsset($"{FontDir}/Bangers.ttf", $"{FontDir}/Bangers SDF.asset");

        // Dark cartoon outline (TMP underlay) baked into the shared font materials:
        // every glyph carries its own contrast, so text survives bright video frames.
        // Fits the comic art direction — cartoon lettering has outlines anyway.
        EnableOutline(_displayFont, dilate: 0.55f);
        EnableOutline(_uiFont, dilate: 0.4f);
    }

    private static void EnableOutline(TMP_FontAsset fa, float dilate)
    {
        if (fa == null || fa.material == null) return;
        var m = fa.material;
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.02f, 0f, 0.09f, 0.95f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.12f);
        EditorUtility.SetDirty(m);
    }

    private static TMP_FontAsset MakeFontAsset(string ttfPath, string assetPath)
    {
        // Reuse if it already exists (rebuilding fonts every run would churn refs).
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogWarning($"[JamScaffold] Font missing at {ttfPath} — falling back to default TMP font.");
            return TMP_Settings.defaultFontAsset;
        }

        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        fa.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(fa, assetPath);
        if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
        {
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        }
        if (fa.material != null)
        {
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        fa.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // =====================================================================
    // Generated sprites
    // =====================================================================

    private static void GenerateArtAssets()
    {
        _rounded = MakeSpriteAsset("RoundedRect", 64, 64, (x, y) =>
        {
            float d = RoundRectDist(x, y, 64, 64, 14f);
            return Mathf.Clamp01(0.5f - d);
        }, new Vector4(20, 20, 20, 20));

        _softGlow = MakeSpriteAsset("SoftGlow", 256, 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128)) / 128f;
            return Mathf.Pow(Mathf.Clamp01(1f - d), 2.6f);
        }, Vector4.zero);

        // Right-pointing triangle, AA edges — the menu selection marker.
        _triangle = MakeSpriteAsset("MarkerTriangle", 32, 32, (x, y) =>
        {
            float half = 16f;
            float span = 1f - Mathf.Abs(y + 0.5f - half) / half; // 1 at middle row, 0 at edges
            float edge = span * 26f;                              // triangle depth
            return Mathf.Clamp01(edge - x) * Mathf.Clamp01(x - 2f + 1f);
        }, Vector4.zero);

        // Equilateral warning triangle (filled; an "!" is overlaid as text).
        _warnTriangle = MakeSpriteAsset("WarnTriangle", 96, 96, (x, y) =>
        {
            // Triangle with apex up: baseline y=8, apex y=88.
            float fy = (y - 8f) / 80f; // 0 at base, 1 at apex
            if (fy < 0f || fy > 1f) return 0f;
            float halfWidth = (1f - fy) * 44f;
            float dx = Mathf.Abs(x + 0.5f - 48f);
            return Mathf.Clamp01(halfWidth - dx);
        }, Vector4.zero);

        // Readability scrims: horizontal (left→right fade) and vertical (bottom→up).
        // Tinted via Image color; used to darken the video behind menu text.
        _scrimH = MakeSpriteAsset("ScrimH", 256, 4, (x, y) =>
        {
            float k = x / 255f;
            if (k < 0.12f) return 1f;
            return Mathf.Clamp01(1f - (k - 0.12f) / 0.55f); // fully clear by ~67%
        }, Vector4.zero);

        _scrimV = MakeSpriteAsset("ScrimV", 4, 256, (x, y) =>
        {
            float k = y / 255f;
            if (k < 0.06f) return 1f;
            return Mathf.Clamp01(1f - (k - 0.06f) / 0.20f); // fully clear by ~26%
        }, Vector4.zero);

        // 12-point comic starburst — menu BANG! now, in-game style popups later.
        _burst = MakeSpriteAsset("ComicBurst", 256, 256, (x, y) =>
        {
            float dx = x + 0.5f - 128f, dy = y + 0.5f - 128f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float theta = Mathf.Atan2(dy, dx); // -π..π
            float spikes = 12f;
            float saw = Mathf.Repeat(theta / (2f * Mathf.PI) * spikes, 1f);
            float spike = 1f - Mathf.Abs(saw * 2f - 1f);          // 1 at tip, 0 in valley
            float edge = Mathf.Lerp(74f, 124f, spike);            // valley/tip radii
            return Mathf.Clamp01(edge - r);
        }, Vector4.zero);
    }

    private static float RoundRectDist(int px, int py, float w, float h, float r)
    {
        float x = px + 0.5f, y = py + 0.5f;
        float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
        float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
        float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r + 1f;
    }

    private static Sprite MakeSpriteAsset(string name, int w, int h,
        System.Func<int, int, float> alphaAt, Vector4 border)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alphaAt(x, y)));
        tex.Apply();

        string path = $"{ArtDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // =====================================================================
    // Post-processing
    // =====================================================================

    private static VolumeProfile CreatePostFxProfile()
    {
        const string path = "Assets/Settings/JamPostFX.asset";
        AssetDatabase.DeleteAsset(path);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>();
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.72f);

        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.36f);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(Theme.FromHex("#05040F"));

        var grain = profile.Add<FilmGrain>();
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.22f); // subtle texture kills the "flat digital" look
        grain.response.Override(0.85f);

        var ca = profile.Add<ChromaticAberration>();
        ca.intensity.Override(0.05f);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void AddPostFxVolume()
    {
        var go = new GameObject("PostFX (Global Volume)");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = _postFx;
    }

    // =====================================================================
    // GameManager prefab
    // =====================================================================

    private static void CreateGameManagerPrefab()
    {
        var root = new GameObject("GameManager");
        root.AddComponent<GameManager>();
        root.AddComponent<ScreenshotTaker>();
        root.AddComponent<DebugOverlay>();
        root.AddComponent<DevHotkeys>();

        var flow = new GameObject("SceneFlow");
        flow.transform.SetParent(root.transform, false);
        flow.AddComponent<SceneFlowController>();

        var audio = new GameObject("Audio");
        audio.transform.SetParent(root.transform, false);
        audio.AddComponent<AudioManager>();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/GameManager.prefab");
        Object.DestroyImmediate(root);
    }

    // =====================================================================
    // Scenes
    // =====================================================================

    private static void BuildBootScene()
    {
        var scene = NewScene();
        MakeCamera();
        new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
        SaveScene(scene, "Boot");
    }

    private static void BuildStudioIntroScene()
    {
        var scene = NewScene();
        var cam = MakeCamera(Color.black);
        AddPostFxVolume();
        var canvas = MakeCanvas(cam);

        var bg = MakeImage(canvas.transform, "BlackBG", Color.black);
        Stretch(bg.rectTransform);

        var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)content.transform);
        var contentGroup = content.GetComponent<CanvasGroup>();

        var videoSurfaceGo = new GameObject("VideoSurface", typeof(RectTransform), typeof(RawImage));
        videoSurfaceGo.transform.SetParent(content.transform, false);
        Stretch((RectTransform)videoSurfaceGo.transform);
        var videoSurface = videoSurfaceGo.GetComponent<RawImage>();
        videoSurfaceGo.SetActive(false);

        // Text placeholder is only built when NO intro video exists — with a video,
        // the video IS the card (it carries its own branding and sound).
        if (_introVideoFile == null)
        {
            var glowColor = Theme.GlowAmber; glowColor.a *= 0.3f;
            var glow = MakeImage(content.transform, "Glow", glowColor);
            glow.sprite = _softGlow;
            glow.rectTransform.sizeDelta = new Vector2(1100, 380);
            glow.rectTransform.anchoredPosition = new Vector2(0, 20);

            var title = MakeText(content.transform, "StudioName", "FIRED.", 84, Theme.Text,
                new Vector2(0, 20), new Vector2(1500, 130), _displayFont, spacing: 2f);
            ApplyTitleGradient(title);
        }

        var cardGo = new GameObject("IntroCard");
        cardGo.transform.SetParent(canvas.transform, false);
        var card = cardGo.AddComponent<IntroCard>();

        var videoGo = new GameObject("VideoPlayer (auto-wired by the builder)");
        videoGo.transform.SetParent(cardGo.transform, false);
        var player = videoGo.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.renderMode = VideoRenderMode.RenderTexture;
        videoGo.SetActive(false);

        SetRef(card, "contentGroup", contentGroup);
        SetRef(card, "videoPlayer", player);
        SetRef(card, "videoSurface", videoSurface);
        if (_introVideoFile != null) SetStr(card, "videoFileName", _introVideoFile);

        SaveScene(scene, "StudioIntro");
    }

    /// <summary>Shaped like the industry-standard console warning: black screen,
    /// small icon, restrained heading, one narrow text column. Deliberately static.</summary>
    private static void BuildEpilepsyScene()
    {
        var scene = NewScene();
        var cam = MakeCamera(Color.black);
        AddPostFxVolume();
        var canvas = MakeCanvas(cam);

        var bg = MakeImage(canvas.transform, "BlackBG", Color.black);
        Stretch(bg.rectTransform);

        // Warning icon: amber triangle + overlaid "!".
        var icon = MakeImage(canvas.transform, "WarnIcon", Theme.Accent);
        icon.sprite = _warnTriangle;
        icon.rectTransform.sizeDelta = new Vector2(64, 64);
        icon.rectTransform.anchoredPosition = new Vector2(0, 300);
        var bang = MakeText(icon.transform, "Bang", "!", 30, Color.black,
            new Vector2(0, -6), new Vector2(40, 40), _uiFont);
        bang.fontStyle = FontStyles.Bold;

        MakeText(canvas.transform, "Title", "EPILEPSY WARNING", 32, Theme.Text,
            new Vector2(0, 218), new Vector2(1300, 50), _displayFont, spacing: 3f);

        MakeRule(canvas.transform, new Vector2(0, 178), 320, Theme.Divider);

        var body = MakeText(canvas.transform, "Body",
            "A very small percentage of people may experience seizures when exposed to " +
            "certain lights, patterns or flashing images — even with no prior history of " +
            "epilepsy or seizures.\n\n" +
            "If you or anyone in your family has an epileptic condition, or has had seizures " +
            "of any kind, consult a doctor before playing.\n\n" +
            "Stop playing immediately and consult a doctor if you experience dizziness, " +
            "altered vision, eye or muscle twitching, involuntary movement, loss of " +
            "awareness, or disorientation.",
            24, Theme.FromHex("#C8C5D4"), new Vector2(0, -10), new Vector2(880, 400), _uiFont);
        body.alignment = TextAlignmentOptions.Center;
        body.lineSpacing = 8f;

        var promptGo = new GameObject("ContinuePrompt", typeof(RectTransform), typeof(CanvasGroup));
        promptGo.transform.SetParent(canvas.transform, false);
        var promptRt = (RectTransform)promptGo.transform;
        promptRt.anchoredPosition = new Vector2(0, -380);
        promptRt.sizeDelta = new Vector2(800, 50);
        MakeText(promptGo.transform, "Text", "PRESS ANY KEY", 20, Theme.TextDim,
            Vector2.zero, new Vector2(800, 50), _uiFont, spacing: 6f);

        var warningGo = new GameObject("EpilepsyWarning");
        warningGo.transform.SetParent(canvas.transform, false);
        var warning = warningGo.AddComponent<EpilepsyWarning>();
        SetRef(warning, "continuePrompt", promptGo.GetComponent<CanvasGroup>());

        SaveScene(scene, "EpilepsyWarning");
    }

    private static void BuildMainMenuScene(GameObject settingsPrefab, GameObject howToPrefab)
    {
        var scene = NewScene();
        var cam = MakeCamera();
        AddPostFxVolume();
        MakeEventSystem();
        var canvas = MakeCanvas(cam);

        var backdrop = MakeBackdropReturning(canvas.transform);

        // ---- Optional looping background video (hides the procedural backdrop) ----
        var menuVideoGo = new GameObject("MenuVideo (auto-wired by the builder)",
            typeof(RectTransform), typeof(RawImage), typeof(VideoPlayer));
        menuVideoGo.transform.SetParent(canvas.transform, false);
        menuVideoGo.transform.SetAsFirstSibling(); // behind everything
        Stretch((RectTransform)menuVideoGo.transform);
        var menuSurface = menuVideoGo.GetComponent<RawImage>();
        menuSurface.enabled = false; // enabled by MenuVideoLoop once frames exist
        menuSurface.raycastTarget = false;
        var menuLoop = menuVideoGo.AddComponent<MenuVideoLoop>();
        SetRef(menuLoop, "videoPlayer", menuVideoGo.GetComponent<VideoPlayer>());
        SetRef(menuLoop, "surface", menuSurface);
        SetRef(menuLoop, "backdropToHide", backdrop);
        if (_menuVideoFile != null) SetStr(menuLoop, "videoFileName", _menuVideoFile);

        // ---- Readability scrims: darken the left third (menu column) and the
        // footer strip so text stays legible over any video frame. They sit
        // directly above the video/backdrop, below all UI.
        var leftScrim = MakeImage(canvas.transform, "LeftScrim", new Color(
            Theme.Background.r, Theme.Background.g, Theme.Background.b, 0.93f));
        leftScrim.sprite = _scrimH;
        Stretch(leftScrim.rectTransform);
        leftScrim.raycastTarget = false;
        leftScrim.transform.SetSiblingIndex(1); // right after MenuVideo

        var bottomScrim = MakeImage(canvas.transform, "BottomScrim", new Color(
            Theme.Background.r, Theme.Background.g, Theme.Background.b, 0.9f));
        bottomScrim.sprite = _scrimV;
        Stretch(bottomScrim.rectTransform);
        bottomScrim.raycastTarget = false;
        bottomScrim.transform.SetSiblingIndex(2);

        // ---- Left-anchored title block: the GAME NAME (the scenery owns the right side) ----
        var title = MakeText(canvas.transform, "Title", "FIRED.", 128, Theme.Text,
            Vector2.zero, new Vector2(760, 200), _displayFont, spacing: 0f);
        AnchorLeft(title.rectTransform, 196, 185);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyTitleGradient(title);

        var subtitle = MakeText(canvas.transform, "Subtitle", "YOU ARE THE BULLET",
            20, Theme.NeonCyan, Vector2.zero, new Vector2(700, 30), _uiFont, spacing: 6f);
        AnchorLeft(subtitle.rectTransform, 206, 82);
        subtitle.alignment = TextAlignmentOptions.MidlineLeft;

        MakeRule(canvas.transform, Vector2.zero, 380, Theme.Divider, leftX: 206, leftY: 48);

        // ---- Text menu (no boxes) ----
        var column = MakeVerticalColumn(canvas.transform, "Menu", Vector2.zero, 6, 52, 10);
        AnchorLeft((RectTransform)column, 196, -180);
        ((VerticalLayoutGroup)column.GetComponent<VerticalLayoutGroup>()).childAlignment = TextAnchor.UpperLeft;

        var controllerGo = new GameObject("MainMenu");
        controllerGo.transform.SetParent(canvas.transform, false);
        var controller = controllerGo.AddComponent<MainMenuController>();

        var play = MakeMenuItem(column, "PlayItem", "PLAY");
        var cont = MakeMenuItem(column, "ContinueItem", "CONTINUE");
        var settings = MakeMenuItem(column, "SettingsItem", "SETTINGS");
        var howTo = MakeMenuItem(column, "HowToPlayItem", "HOW TO PLAY");
        var credits = MakeMenuItem(column, "CreditsItem", "CREDITS");
        var quit = MakeMenuItem(column, "QuitItem", "QUIT");

        Wire(play, controller.OnPlay);
        Wire(cont, controller.OnContinue);
        Wire(settings, controller.OnSettings);
        Wire(howTo, controller.OnHowToPlay);
        Wire(credits, controller.OnCredits);
        Wire(quit, controller.OnQuit);

        // ---- Footer: hairline + version + input hints ----
        BuildFooter(canvas.transform, withVersion: true);

        var settingsPanel = InstantiatePanel(settingsPrefab, canvas.transform);
        var howToPanel = InstantiatePanel(howToPrefab, canvas.transform);

        // ---- Gunshot flourish (PLAY fires the run into existence) ----
        var shotGo = new GameObject("ShotFX", typeof(RectTransform));
        shotGo.transform.SetParent(canvas.transform, false); // last sibling = on top
        Stretch((RectTransform)shotGo.transform);

        var flash = MakeImage(shotGo.transform, "Flash", new Color(1f, 1f, 1f, 0f));
        Stretch(flash.rectTransform);
        flash.raycastTarget = false;

        var burstRoot = new GameObject("Burst", typeof(RectTransform));
        burstRoot.transform.SetParent(shotGo.transform, false);
        var burstRt = (RectTransform)burstRoot.transform;
        burstRt.anchorMin = burstRt.anchorMax = new Vector2(0, 0.5f);
        burstRt.pivot = new Vector2(0.5f, 0.5f);
        burstRt.anchoredPosition = new Vector2(545, 10); // on the PLAY line, right of the label
        burstRt.sizeDelta = new Vector2(260, 260);

        var star = MakeImage(burstRoot.transform, "Star", Theme.NeonYellow);
        star.sprite = _burst;
        Stretch(star.rectTransform);
        star.raycastTarget = false;
        var bang = MakeText(burstRoot.transform, "BangText", "BANG!", 58, Theme.Background,
            new Vector2(0, 2), new Vector2(240, 90), _comicFont);
        bang.transform.localRotation = Quaternion.Euler(0, 0, 6f);

        var shot = shotGo.AddComponent<MenuGunshot>();
        SetRef(shot, "flash", flash);
        SetRef(shot, "burst", burstRt);
        shotGo.SetActive(false);

        SetRef(controller, "continueButton", cont.gameObject);
        SetRef(controller, "quitButton", quit.gameObject);
        SetRef(controller, "settingsPanel", settingsPanel);
        SetRef(controller, "howToPlayPanel", howToPanel);
        SetRef(controller, "firstSelected", play.gameObject);
        SetRef(controller, "gunshotFx", shotGo);

        SaveScene(scene, "MainMenu");
    }

    private static void BuildGameScene(GameObject settingsPrefab, GameObject howToPrefab)
    {
        var scene = NewScene();
        // FIRED. is a 3D flight game — perspective camera + a directional light is
        // added at runtime by FiredGameController. The UI canvas is Screen Space -
        // Overlay so the HUD/pause/gameover always render crisp on top of the world.
        MakeGameCamera();
        AddPostFxVolume();
        MakeEventSystem();
        var canvas = MakeCanvasOverlay();

        // ---- HUD ----
        var distance = MakeText(canvas.transform, "Distance", "0 m", 46, Theme.Text,
            Vector2.zero, new Vector2(420, 60), _uiFont, spacing: 2f);
        var drt = distance.rectTransform;
        drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(1, 1);
        drt.anchoredPosition = new Vector2(-44, -32);
        distance.alignment = TextAlignmentOptions.TopRight;

        var mult = MakeText(canvas.transform, "Multiplier", "x1", 60, Theme.NeonYellow,
            Vector2.zero, new Vector2(220, 76), _displayFont, spacing: 0f);
        var mrt = mult.rectTransform;
        mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1, 1);
        mrt.anchoredPosition = new Vector2(-44, -86);
        mult.alignment = TextAlignmentOptions.TopRight;

        var barBg = MakeImage(canvas.transform, "SpeedBar", Theme.FromHex("#20153E"));
        barBg.sprite = _rounded; barBg.type = Image.Type.Sliced;
        var bgrt = barBg.rectTransform;
        bgrt.anchorMin = bgrt.anchorMax = bgrt.pivot = new Vector2(0.5f, 0);
        bgrt.anchoredPosition = new Vector2(0, 54);
        bgrt.sizeDelta = new Vector2(620, 16);
        var fill = MakeImage(barBg.transform, "Fill", Theme.NeonCyan);
        fill.sprite = _rounded; fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        Stretch(fill.rectTransform); fill.fillAmount = 0.7f;
        var speedLabel = MakeText(canvas.transform, "SpeedLabel", "SPEED", 15, Theme.TextDim,
            Vector2.zero, new Vector2(200, 24), _uiFont, spacing: 5f);
        var slrt = speedLabel.rectTransform;
        slrt.anchorMin = slrt.anchorMax = slrt.pivot = new Vector2(0.5f, 0);
        slrt.anchoredPosition = new Vector2(0, 76);

        // FiredGameController builds the whole 3D world at runtime (camera rig,
        // bullet, corridor, materials, lights). See Assets/Scripts/Game and the GDD.
        var gameGo = new GameObject("FiredGame");
        var game = gameGo.AddComponent<FiredGameController>();
        SetRef(game, "distanceText", distance);
        SetRef(game, "multiplierText", mult);
        SetRef(game, "speedFill", fill);
        SetRef(game, "hudCanvas", canvas.GetComponent<RectTransform>());

        // ---- Pause overlay: dim + left-aligned text menu (no plate) ----
        var pauseGo = new GameObject("PauseMenu", typeof(RectTransform));
        pauseGo.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)pauseGo.transform);
        var pause = pauseGo.AddComponent<PauseMenu>();

        var pauseOverlay = MakeTextOverlay(pauseGo.transform, "Overlay");
        var pTitle = MakeText(pauseOverlay.transform, "Title", "PAUSED", 56, Theme.Text,
            Vector2.zero, new Vector2(600, 90), _displayFont, spacing: 2f);
        AnchorLeft(pTitle.rectTransform, 200, 255);
        pTitle.alignment = TextAlignmentOptions.MidlineLeft;
        ApplyTitleGradient(pTitle);
        MakeRule(pauseOverlay.transform, Vector2.zero, 340, Theme.Divider, leftX: 204, leftY: 195);

        var pCol = MakeVerticalColumn(pauseOverlay.transform, "Menu", Vector2.zero, 6, 48, 8);
        AnchorLeft((RectTransform)pCol, 196, -85);
        ((VerticalLayoutGroup)pCol.GetComponent<VerticalLayoutGroup>()).childAlignment = TextAnchor.UpperLeft;

        var resume = MakeMenuItem(pCol, "ResumeItem", "RESUME", fontSize: 26, height: 46);
        var restart = MakeMenuItem(pCol, "RestartItem", "RESTART", fontSize: 26, height: 46);
        var pSettings = MakeMenuItem(pCol, "SettingsItem", "SETTINGS", fontSize: 26, height: 46);
        var pHowTo = MakeMenuItem(pCol, "HowToPlayItem", "HOW TO PLAY", fontSize: 26, height: 46);
        var pMenu = MakeMenuItem(pCol, "MainMenuItem", "MAIN MENU", fontSize: 26, height: 46);
        var pQuit = MakeMenuItem(pCol, "QuitItem", "QUIT", fontSize: 26, height: 46);

        Wire(resume, pause.OnResume);
        Wire(restart, pause.OnRestart);
        Wire(pSettings, pause.OnSettings);
        Wire(pHowTo, pause.OnHowToPlay);
        Wire(pMenu, pause.OnMainMenu);
        Wire(pQuit, pause.OnQuit);

        // ---- Game over overlay: centered big type ----
        var overGo = new GameObject("GameOverScreen", typeof(RectTransform));
        overGo.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)overGo.transform);
        var over = overGo.AddComponent<GameOverScreen>();

        var overOverlay = MakeTextOverlay(overGo.transform, "Overlay");
        var oGlow = MakeImage(overOverlay.transform, "Glow", new Color(1f, 0.7f, 0.4f, 0.10f));
        oGlow.sprite = _softGlow;
        oGlow.rectTransform.sizeDelta = new Vector2(1300, 500);
        oGlow.rectTransform.anchoredPosition = new Vector2(0, 130);

        var oTitle = MakeText(overOverlay.transform, "Title", "GAME OVER", 84, Theme.Negative,
            new Vector2(0, 170), new Vector2(1500, 130), _displayFont, spacing: 3f);
        MakeRule(overOverlay.transform, new Vector2(0, 84), 420, Theme.Divider);

        // Run summary (distance / score / cause), filled from GameEvents.LastRunSummary.
        var oStats = MakeText(overOverlay.transform, "Stats", "", 26, Theme.Text,
            new Vector2(0, 34), new Vector2(1200, 40), _uiFont, spacing: 2f);

        var oCol = MakeVerticalColumn(overOverlay.transform, "Menu", new Vector2(30, -110), 2, 50, 10);
        var retry = MakeMenuItem(oCol, "RetryItem", "RETRY", width: 300);
        var oMenu = MakeMenuItem(oCol, "MainMenuItem", "MAIN MENU", width: 300);
        Wire(retry, over.OnRetry);
        Wire(oMenu, over.OnMainMenu);

        var settingsPanel = InstantiatePanel(settingsPrefab, canvas.transform);
        var howToPanel = InstantiatePanel(howToPrefab, canvas.transform);

        SetRef(pause, "overlayRoot", pauseOverlay);
        SetRef(pause, "settingsPanel", settingsPanel);
        SetRef(pause, "howToPlayPanel", howToPanel);
        SetRef(pause, "quitButton", pQuit.gameObject);
        SetRef(pause, "firstSelected", resume.gameObject);

        SetRef(over, "overlayRoot", overOverlay);
        SetRef(over, "titleText", oTitle);
        SetRef(over, "statsText", oStats);
        SetRef(over, "firstSelected", retry.gameObject);

        // The flight HUD (fire prompt, crosshair, deadeye meter, combo, letterbox)
        // and the best-shot polaroid are baked as real scene objects, not built at
        // runtime — same pass the Bake Runtime UI menu item uses.
        RuntimeUiBaker.BakeInto(canvas.transform, game, over);

        pauseOverlay.SetActive(false);
        overOverlay.SetActive(false);

        SaveScene(scene, "Game");
    }

    private static void BuildCreditsScene()
    {
        var scene = NewScene();
        var cam = MakeCamera();
        AddPostFxVolume();
        var canvas = MakeCanvas(cam);

        MakeBackdrop(canvas.transform);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)viewport.transform);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = (RectTransform)content.transform;
        contentRt.sizeDelta = new Vector2(1200, 2000);

        var text = MakeText(content.transform, "CreditsText", "(loaded from Resources/Credits.txt)",
            32, Theme.Text, Vector2.zero, Vector2.zero, _uiFont);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Top;
        text.lineSpacing = 6f;

        var hintGo = new GameObject("SkipHint", typeof(RectTransform), typeof(CanvasGroup));
        hintGo.transform.SetParent(canvas.transform, false);
        var hintRt = (RectTransform)hintGo.transform;
        hintRt.anchorMin = hintRt.anchorMax = hintRt.pivot = new Vector2(1, 0);
        hintRt.anchoredPosition = new Vector2(-30, 24);
        hintRt.sizeDelta = new Vector2(500, 40);
        var hint = MakeText(hintGo.transform, "Text", "PRESS ANY KEY TO SKIP", 16, Theme.TextDim,
            Vector2.zero, new Vector2(500, 40), _uiFont, spacing: 4f);
        hint.alignment = TextAlignmentOptions.BottomRight;
        hintGo.AddComponent<PulseAlpha>();

        var scrollerGo = new GameObject("CreditsScroller");
        scrollerGo.transform.SetParent(canvas.transform, false);
        var scroller = scrollerGo.AddComponent<CreditsScroller>();
        SetRef(scroller, "content", contentRt);
        SetRef(scroller, "creditsText", text);

        SaveScene(scene, "Credits");
    }

    // =====================================================================
    // Panels (settings / how-to-play) — minimal full-screen sheets, no plates
    // =====================================================================

    private static GameObject BuildSettingsPanelPrefab()
    {
        var root = MakeSheet("SettingsPanel", "SETTINGS", out Transform sheet);
        var menu = root.AddComponent<SettingsMenu>();

        var rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(sheet, false);
        var rowsRt = (RectTransform)rows.transform;
        rowsRt.anchoredPosition = new Vector2(0, -40);
        rowsRt.sizeDelta = new Vector2(820, 420);
        var vlg = rows.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 18;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = false; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;

        var master = MakeSliderRow(rows.transform, "MasterRow", "MASTER VOLUME");
        var music = MakeSliderRow(rows.transform, "MusicRow", "MUSIC VOLUME");
        var sfx = MakeSliderRow(rows.transform, "SfxRow", "SFX VOLUME");
        var (fullscreenRow, fullscreenToggle) = MakeToggleRow(rows.transform, "FullscreenRow", "FULLSCREEN");
        var (resolutionRow, resolutionDropdown) = MakeDropdownRow(rows.transform, "ResolutionRow", "RESOLUTION");

        // Bottom actions as text items.
        var actions = new GameObject("Actions", typeof(RectTransform));
        actions.transform.SetParent(sheet, false);
        var art = (RectTransform)actions.transform;
        art.anchoredPosition = new Vector2(30, -330);
        art.sizeDelta = new Vector2(760, 50);
        var hlg = actions.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 80;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var reset = MakeMenuItem(actions.transform, "ResetItem", "RESET DEFAULTS", width: 320, fontSize: 24, height: 46);
        var back = MakeMenuItem(actions.transform, "BackItem", "BACK", width: 190, fontSize: 24, height: 46);

        UnityEventTools.AddPersistentListener(master.onValueChanged, menu.OnMasterChanged);
        UnityEventTools.AddPersistentListener(music.onValueChanged, menu.OnMusicChanged);
        UnityEventTools.AddPersistentListener(sfx.onValueChanged, menu.OnSfxChanged);
        UnityEventTools.AddPersistentListener(fullscreenToggle.onValueChanged, menu.OnFullscreenChanged);
        UnityEventTools.AddPersistentListener(resolutionDropdown.onValueChanged, menu.OnResolutionChanged);
        Wire(reset, menu.OnResetToDefaults);
        Wire(back, menu.OnBack);

        SetRef(menu, "masterSlider", master);
        SetRef(menu, "musicSlider", music);
        SetRef(menu, "sfxSlider", sfx);
        SetRef(menu, "fullscreenToggle", fullscreenToggle);
        SetRef(menu, "resolutionRow", resolutionRow);
        SetRef(menu, "resolutionDropdown", resolutionDropdown);

        var selector = root.AddComponent<SelectOnEnable>();
        SetRef(selector, "target", master.gameObject);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/SettingsPanel.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildHowToPlayPanelPrefab()
    {
        var root = MakeSheet("HowToPlayPanel", "HOW TO PLAY", out Transform sheet);
        var panel = root.AddComponent<HowToPlayPanel>();

        var content = MakeText(sheet, "Content", HowToPlayPanel.PlaceholderText, 26, Theme.Text,
            new Vector2(0, -30), new Vector2(760, 420), _uiFont);
        content.alignment = TextAlignmentOptions.Center;
        content.lineSpacing = 10f;

        var back = MakeMenuItem(sheet, "BackItem", "BACK", width: 190, fontSize: 24, height: 46);
        var backRt = (RectTransform)back.transform;
        backRt.anchoredPosition = new Vector2(30, -330);

        Wire(back, panel.OnBack);
        SetRef(panel, "contentText", content);

        var selector = root.AddComponent<SelectOnEnable>();
        SetRef(selector, "target", back.gameObject);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/HowToPlayPanel.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Full-screen near-opaque sheet with a display-font header and rule —
    /// the minimal "options screen" idiom. Root has CanvasGroup + PanelAnimator.</summary>
    private static GameObject MakeSheet(string name, string title, out Transform sheet)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        Stretch((RectTransform)root.transform);
        root.AddComponent<PanelAnimator>();

        var dim = MakeImage(root.transform, "Dim", Theme.FromHex("#0D0221F5"));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var content = new GameObject("Sheet", typeof(RectTransform));
        content.transform.SetParent(root.transform, false);
        Stretch((RectTransform)content.transform);
        sheet = content.transform;

        var header = MakeText(sheet, "Header", title, 42, Theme.Text,
            new Vector2(0, 330), new Vector2(1100, 70), _displayFont, spacing: 2f);
        ApplyTitleGradient(header);
        MakeRule(sheet, new Vector2(0, 282), 360, Theme.Divider);
        return root;
    }

    /// <summary>Dim-only overlay root for pause/game-over (contents added by caller).</summary>
    private static GameObject MakeTextOverlay(Transform parent, string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        Stretch((RectTransform)root.transform);
        root.AddComponent<PanelAnimator>();

        var dim = MakeImage(root.transform, "Dim", Theme.FromHex("#070113F2"));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        return root;
    }

    private static void BuildFooter(Transform parent, bool withVersion)
    {
        var line = MakeImage(parent, "FooterLine", Theme.FromHex("#FFFFFF12"));
        var lrt = line.rectTransform;
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 0);
        lrt.pivot = new Vector2(0.5f, 0);
        lrt.anchoredPosition = new Vector2(0, 62);
        lrt.sizeDelta = new Vector2(-160, 1);
        line.raycastTarget = false;

        if (withVersion)
        {
            var version = MakeText(parent, "Version", "v0.0.0", 16, Theme.TextDim,
                Vector2.zero, new Vector2(220, 30), _uiFont, spacing: 2f);
            var vrt = version.rectTransform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0, 0);
            vrt.anchoredPosition = new Vector2(84, 20);
            version.alignment = TextAlignmentOptions.BottomLeft;
            version.gameObject.AddComponent<VersionLabel>();
        }

        var hints = MakeText(parent, "InputHints",
            "W / S  or  UP / DOWN   navigate        ENTER   select", 15, Theme.TextDim,
            Vector2.zero, new Vector2(900, 30), _uiFont, spacing: 2f);
        var hrt = hints.rectTransform;
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(1, 0);
        hrt.anchoredPosition = new Vector2(-84, 20);
        hints.alignment = TextAlignmentOptions.BottomRight;
    }

    private static GameObject InstantiatePanel(GameObject prefab, Transform parent)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        Stretch((RectTransform)instance.transform);
        instance.SetActive(false);
        return instance;
    }

    // =====================================================================
    // Settings rows
    // =====================================================================

    private static RectTransform MakeRow(Transform parent, string name)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.sizeDelta = new Vector2(820, 54);

        // Hairline separator under each row — quiet structure instead of boxes.
        var sep = MakeImage(row.transform, "Separator", Theme.FromHex("#FFFFFF0D"));
        var srt = sep.rectTransform;
        srt.anchorMin = new Vector2(0, 0);
        srt.anchorMax = new Vector2(1, 0);
        srt.pivot = new Vector2(0.5f, 0);
        srt.anchoredPosition = new Vector2(0, -6);
        srt.sizeDelta = new Vector2(0, 1);
        sep.raycastTarget = false;
        return rt;
    }

    private static TextMeshProUGUI MakeRowLabel(RectTransform row, string label)
    {
        var text = MakeText(row, "Label", label, 22, Theme.Text, Vector2.zero,
            new Vector2(360, 54), _uiFont, spacing: 3f);
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(10, 0);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private static void DockRight(RectTransform rt, Vector2 size, float rightPad = 10)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(-rightPad, 0);
    }

    private static Slider MakeSliderRow(Transform parent, string name, string label)
    {
        var row = MakeRow(parent, name);
        MakeRowLabel(row, label);

        var sliderGo = DefaultControls.CreateSlider(UIRes());
        sliderGo.transform.SetParent(row, false);
        DockRight((RectTransform)sliderGo.transform, new Vector2(380, 22));

        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;

        Restyle(sliderGo, "Background", _rounded, Theme.FromHex("#23264A"));
        Restyle(sliderGo, "Fill Area/Fill", _rounded, Theme.Accent);
        Tint(sliderGo, "Handle Slide Area/Handle", Theme.Text);
        return slider;
    }

    private static (GameObject row, Toggle toggle) MakeToggleRow(Transform parent, string name, string label)
    {
        var row = MakeRow(parent, name);
        MakeRowLabel(row, label);

        var toggleGo = DefaultControls.CreateToggle(UIRes());
        toggleGo.transform.SetParent(row, false);
        DockRight((RectTransform)toggleGo.transform, new Vector2(38, 38), 24);

        var oldLabel = toggleGo.transform.Find("Label");
        if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);

        var bg = toggleGo.transform.Find("Background");
        if (bg != null)
        {
            ((RectTransform)bg).sizeDelta = new Vector2(38, 38);
            ((RectTransform)bg).anchoredPosition = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = _rounded;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Theme.FromHex("#23264A");
            var check = bg.Find("Checkmark");
            if (check != null)
            {
                ((RectTransform)check).sizeDelta = new Vector2(28, 28);
                check.GetComponent<Image>().color = Theme.Accent;
            }
        }
        return (row.gameObject, toggleGo.GetComponent<Toggle>());
    }

    private static (GameObject row, TMP_Dropdown dropdown) MakeDropdownRow(Transform parent, string name, string label)
    {
        var row = MakeRow(parent, name);
        MakeRowLabel(row, label);

        var ddGo = TMP_DefaultControls.CreateDropdown(TmpRes());
        ddGo.transform.SetParent(row, false);
        DockRight((RectTransform)ddGo.transform, new Vector2(380, 42));

        var dd = ddGo.GetComponent<TMP_Dropdown>();
        var ddImg = ddGo.GetComponent<Image>();
        ddImg.sprite = _rounded;
        ddImg.type = Image.Type.Sliced;
        ddImg.color = Theme.FromHex("#23264A");
        if (dd.captionText != null) { dd.captionText.color = Theme.Text; dd.captionText.font = _uiFont; }
        if (dd.itemText != null) { dd.itemText.color = Theme.Text; dd.itemText.font = _uiFont; }
        Restyle(ddGo, "Template", _rounded, Theme.FromHex("#191C3A"));
        Tint(ddGo, "Arrow", Theme.Text);
        return (row.gameObject, dd);
    }

    // =====================================================================
    // Low-level helpers
    // =====================================================================

    private static UnityEngine.SceneManagement.Scene NewScene()
        => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    private static void SaveScene(UnityEngine.SceneManagement.Scene scene, string name)
        => EditorSceneManager.SaveScene(scene, $"{ScenesDir}/{name}.unity");

    private static Camera MakeCamera() => MakeCamera(Theme.Background);

    private static Camera MakeCamera(Color bg)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = bg;
        cam.orthographic = true;
        cam.allowHDR = true;
        go.AddComponent<AudioListener>();
        go.transform.position = new Vector3(0, 0, -10);

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        return cam;
    }

    private static void MakeEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    /// <summary>Perspective camera for the FIRED. 3D world (driven at runtime by
    /// the chase cam). Post-processing on so the neon world blooms.</summary>
    private static Camera MakeGameCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Theme.Background;
        cam.orthographic = false;
        cam.fieldOfView = 70f;
        cam.allowHDR = true;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 800f;
        go.AddComponent<AudioListener>();
        go.transform.position = new Vector3(0, 2, -6);

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        return cam;
    }

    /// <summary>Screen Space - Overlay canvas (independent of the flying game
    /// camera) so the HUD and overlays stay crisp and always on top.</summary>
    private static Canvas MakeCanvasOverlay()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static Canvas MakeCanvas(Camera cam)
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 5f;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void MakeBackdrop(Transform parent) => MakeBackdropReturning(parent);

    private static GameObject MakeBackdropReturning(Transform parent)
    {
        var go = new GameObject("Backdrop (replace with real art)", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        go.AddComponent<StarfieldBackdrop>();   // stars, nebula, shooting stars
        go.AddComponent<CyberCityBackdrop>();   // synthwave sun, neon skylines, traffic
        return go;
    }

    private static void ApplyTitleGradient(TextMeshProUGUI text)
    {
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(
            Theme.TitleGradTop, Theme.TitleGradTop,
            Theme.TitleGradBottom, Theme.TitleGradBottom);
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string content,
        float size, Color color, Vector2 pos, Vector2 rectSize,
        TMP_FontAsset font = null, float spacing = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        if (rectSize != Vector2.zero) rt.sizeDelta = rectSize;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        if (font != null) text.font = font;
        text.characterSpacing = spacing;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Image MakeImage(Transform parent, string name, Color color, bool sliced = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (sliced)
        {
            img.sprite = _rounded;
            img.type = Image.Type.Sliced;
        }
        return img;
    }

    /// <summary>Thin horizontal line. Centered by default; pass leftX/leftY to left-anchor.</summary>
    private static void MakeRule(Transform parent, Vector2 pos, float width, Color color,
        float? leftX = null, float? leftY = null)
    {
        var img = MakeImage(parent, "Rule", color);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(width, 2);
        if (leftX.HasValue)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(leftX.Value, leftY ?? 0);
        }
        else rt.anchoredPosition = pos;
        img.raycastTarget = false;
    }

    private static void AnchorLeft(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private static Transform MakeVerticalColumn(Transform parent, string name, Vector2 pos,
        int slots, float itemHeight, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(420, slots * (itemHeight + spacing));

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = false; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        return go.transform;
    }

    /// <summary>Text-only menu entry: invisible hit-rect + Button(no transition) +
    /// MenuItem (amber label + sliding triangle marker on select).</summary>
    private static Button MakeMenuItem(Transform parent, string name, string label,
        float width = 380, float fontSize = 30, float height = 52)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(width, height);

        var hit = go.GetComponent<Image>();
        hit.color = new Color(0, 0, 0, 0); // invisible, still raycastable

        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;

        var marker = MakeImage(go.transform, "Marker", Theme.Accent);
        marker.sprite = _triangle;
        var mrt = marker.rectTransform;
        mrt.anchorMin = mrt.anchorMax = new Vector2(0, 0.5f);
        mrt.pivot = new Vector2(0, 0.5f);
        mrt.sizeDelta = new Vector2(15, 15);
        mrt.anchoredPosition = new Vector2(2, 0);
        marker.raycastTarget = false;

        var text = MakeText(go.transform, "Label", label, fontSize, Theme.Text,
            Vector2.zero, new Vector2(width - 34, height), _displayFont, spacing: 4f);
        var trt = text.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0, 0.5f);
        trt.pivot = new Vector2(0, 0.5f);
        trt.anchoredPosition = new Vector2(30, 0);
        text.alignment = TextAlignmentOptions.MidlineLeft;

        var item = go.AddComponent<MenuItem>();
        SetRef(item, "label", text);
        SetRef(item, "marker", marker);
        return button;
    }

    private static void Wire(Button button, UnityAction handler)
        => UnityEventTools.AddPersistentListener(button.onClick, handler);

    /// <summary>Sets a private [SerializeField] string, like typing in the inspector.</summary>
    private static void SetStr(Component target, string fieldName, string value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[JamScaffold] Field '{fieldName}' not found on {target.GetType().Name}");
            return;
        }
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRef(Component target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[JamScaffold] Field '{fieldName}' not found on {target.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Tint(GameObject root, string childPath, Color color)
    {
        var child = root.transform.Find(childPath);
        if (child == null) return;
        var img = child.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    private static void Restyle(GameObject root, string childPath, Sprite sprite, Color color)
    {
        var child = root.transform.Find(childPath);
        if (child == null) return;
        var img = child.GetComponent<Image>();
        if (img == null) return;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
    }

    private static DefaultControls.Resources UIRes() => new DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
    };

    private static TMP_DefaultControls.Resources TmpRes() => new TMP_DefaultControls.Resources
    {
        standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
        inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
        dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
        mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
    };

    private static void SetBuildSettings()
    {
        string[] order = { "Boot", "StudioIntro", "EpilepsyWarning", "MainMenu", "Game", "Credits" };
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var name in order)
            scenes.Add(new EditorBuildSettingsScene($"{ScenesDir}/{name}.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
