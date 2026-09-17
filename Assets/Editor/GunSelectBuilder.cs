using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the ARSENAL (gun select) feature into both scenes:
///  - MainMenu: an ARSENAL menu button + the GunCarousel panel (preview image,
///    name label, arrows, EQUIP/BACK), all wired with persistent listeners.
///  - Game: a ShooterGunEquip on the Shooter so the chosen gun is in hand.
/// Re-running replaces what it previously built.
///
///   Tools > Jam Scaffold > Build Gun Select (both scenes)
/// </summary>
public static class GunSelectBuilder
{
    private const string GunDir = "Assets/Gun/kenney_blasterKit/Models/FBX format";

    [UnityEditor.MenuItem("Tools/FIRED/Build Gun Select (both scenes)")]
    public static void Build()
    {
        var guns = LoadGuns();
        if (guns.Length == 0) { Debug.LogError("[GunSelect] No blaster*.fbx found in " + GunDir); return; }

        BuildMenu(guns);
        BuildGame(guns);
        Debug.Log($"[GunSelect] Built with {guns.Length} guns.");
    }

    private static GameObject[] LoadGuns()
    {
        var list = new List<GameObject>();
        foreach (var f in Directory.GetFiles(GunDir, "blaster*.fbx"))
        {
            string path = f.Replace('\\', '/');

            // Barrel-direction detection (GunSelection.BarrelSign) reads mesh
            // vertices at runtime — needs Read/Write enabled to work in builds.
            if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
            }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) list.Add(go);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list.ToArray();
    }

    // ---- MainMenu ------------------------------------------------------------

    private static void BuildMenu(GameObject[] guns)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");

        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("[GunSelect] No Canvas in MainMenu"); return; }

        // Replace a previous build.
        var oldPanel = FindDeep(canvas.transform, "ArsenalPanel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);
        var oldBtn = FindDeep(canvas.transform, "Btn_Arsenal");
        if (oldBtn != null) Object.DestroyImmediate(oldBtn.gameObject);

        // ---- Panel root (inactive until the button opens it) ----
        var panel = new GameObject("ArsenalPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)panel.transform);
        panel.GetComponent<Image>().color = new Color(0.02f, 0.008f, 0.07f, 0.97f);
        var carousel = panel.AddComponent<GunCarousel>();
        carousel.guns = guns;

        var font = FindAnyFont(canvas);

        // Title
        var title = MakeText(panel.transform, "Title", "ARSENAL", 64, Theme.NeonYellow, font);
        Anchor(title, new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(800, 90));

        // 3-D preview
        var prevGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
        prevGo.transform.SetParent(panel.transform, false);
        var prt = (RectTransform)prevGo.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, 40);
        prt.sizeDelta = new Vector2(960, 480);
        carousel.preview = prevGo.GetComponent<RawImage>();

        // Gun name under the preview
        var name = MakeText(panel.transform, "GunName", "BLASTER G", 44, Theme.NeonCyan, font);
        Anchor(name, new Vector2(0.5f, 0.5f), new Vector2(0, -240), new Vector2(700, 60));
        carousel.nameText = name.GetComponent<TMP_Text>();

        // Arrows
        var left = MakeButton(panel.transform, "Btn_Left", "<", font, out var leftBtn);
        Anchor(left, new Vector2(0.5f, 0.5f), new Vector2(-560, 40), new Vector2(90, 90));
        UnityEventTools.AddPersistentListener(leftBtn.onClick, carousel.OnLeft);

        var right = MakeButton(panel.transform, "Btn_Right", ">", font, out var rightBtn);
        Anchor(right, new Vector2(0.5f, 0.5f), new Vector2(560, 40), new Vector2(90, 90));
        UnityEventTools.AddPersistentListener(rightBtn.onClick, carousel.OnRight);

        // Equip / Back
        var equip = MakeButton(panel.transform, "Btn_Equip", "EQUIP", font, out var equipBtn);
        Anchor(equip, new Vector2(0.5f, 0f), new Vector2(-130, 90), new Vector2(220, 64));
        UnityEventTools.AddPersistentListener(equipBtn.onClick, carousel.OnEquip);

        var back = MakeButton(panel.transform, "Btn_Back", "BACK", font, out var backBtn);
        Anchor(back, new Vector2(0.5f, 0f), new Vector2(130, 90), new Vector2(220, 64));
        UnityEventTools.AddPersistentListener(backBtn.onClick, carousel.OnBack);

        panel.SetActive(false);

        // ---- ARSENAL button on the main menu, cloned from an existing one ----
        Button template = null;
        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
        {
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.ToUpperInvariant().Contains("SETTINGS")) { template = b; break; }
        }
        if (template == null)
            foreach (var b in canvas.GetComponentsInChildren<Button>(true)) { template = b; break; }

        if (template != null)
        {
            var btnGo = Object.Instantiate(template.gameObject, template.transform.parent);
            btnGo.name = "Btn_Arsenal";
            btnGo.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
            var label = btnGo.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "ARSENAL";
            var btn = btnGo.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddBoolPersistentListener(btn.onClick, panel.SetActive, true);
        }
        else Debug.LogWarning("[GunSelect] No menu button found to clone — open the panel manually.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---- Game ------------------------------------------------------------------

    private static void BuildGame(GameObject[] guns)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");

        var shooter = GameObject.Find("Shooter");
        if (shooter == null) { Debug.LogWarning("[GunSelect] No 'Shooter' in Game scene"); return; }

        var equip = shooter.GetComponent<ShooterGunEquip>();
        if (equip == null) equip = shooter.AddComponent<ShooterGunEquip>();
        equip.guns = guns;
        EditorUtility.SetDirty(shooter);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---- UI helpers --------------------------------------------------------------

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y == 0f ? 0f : anchor.y == 1f ? 1f : 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static GameObject MakeText(Transform parent, string name, string text, float size, Color color, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        return go;
    }

    private static GameObject MakeButton(Transform parent, string name, string label, TMP_FontAsset font, out Button button)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = Theme.FromHex("#2A1B52");
        button = go.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.4f, 1f);
        button.colors = colors;

        var txt = MakeText(go.transform, "Label", label, 34, Theme.NeonYellow, font);
        Stretch((RectTransform)txt.transform);
        return go;
    }

    private static TMP_FontAsset FindAnyFont(Canvas canvas)
    {
        var t = canvas.GetComponentInChildren<TMP_Text>(true);
        return t != null ? t.font : null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
