using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editable gun poses:
///
///  - One prefab per blaster in Assets/Resources/Guns/Held_<name>.prefab. The
///    ROOT's local transform is the exact local transform the gun gets under
///    the shooter's RightHand at runtime (ShooterGunEquip instantiates it with
///    worldPositionStays=false). Tweak position/rotation/scale freely.
///  - A GunPreview scene (NOT in the build) with one posed shooter per gun so
///    every grip can be judged side by side. The guns there are prefab
///    INSTANCES: move/rotate/scale them, then Overrides ▸ Apply All on the
///    Held_* root to save the pose back to the prefab the game uses.
///
///   Tools > Jam Scaffold > Build Gun Pose Prefabs + Preview
///     (existing Held_* prefabs are NOT overwritten — your edits are safe)
///   Tools > Jam Scaffold > Reset Gun Pose Prefabs to Auto Defaults
/// </summary>
public static class GunPoseBuilder
{
    private const string GunDir = "Assets/Gun/kenney_blasterKit/Models/FBX format";
    private const string HeldDir = "Assets/Resources/Guns";
    private const string PreviewScenePath = "Assets/Scenes/GunPreview.unity";
    private const string ShooterFbx = "Assets/Gunplay.fbx";

    [UnityEditor.MenuItem("Tools/Jam Scaffold/Build Gun Pose Prefabs + Preview")]
    public static void Build() => BuildInternal(overwrite: false);

    [UnityEditor.MenuItem("Tools/Jam Scaffold/Reset Gun Pose Prefabs to Auto Defaults")]
    public static void Reset()
    {
        if (EditorUtility.DisplayDialog("Reset gun poses?",
            "This overwrites ALL Held_* prefabs (including manual tweaks) with auto-computed defaults.",
            "Reset", "Cancel"))
            BuildInternal(overwrite: true);
    }

    private static void BuildInternal(bool overwrite)
    {
        // The reference hand comes from the REAL Game-scene shooter, so the
        // authored local transforms mean exactly what the runtime will apply.
        var gameScene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        var shooter = GameObject.Find("Shooter");
        if (shooter == null) { Debug.LogError("[GunPose] No 'Shooter' in Game scene"); return; }
        Transform hand = FindHand(shooter.transform);
        if (hand == null) { Debug.LogError("[GunPose] No RightHand bone"); return; }

        if (!AssetDatabase.IsValidFolder(HeldDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Guns");
        }

        // ---- 1. Held_* prefabs -------------------------------------------------
        int created = 0, kept = 0;
        var gunPaths = Directory.GetFiles(GunDir, "blaster*.fbx");
        System.Array.Sort(gunPaths, string.CompareOrdinal);

        foreach (var f in gunPaths)
        {
            string path = f.Replace('\\', '/');
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) continue;
            string heldPath = $"{HeldDir}/Held_{model.name}.prefab";
            if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(heldPath) != null) { kept++; continue; }

            var root = new GameObject($"Held_{model.name}");
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(model);
            vis.transform.SetParent(root.transform, false);
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = Vector3.one;

            ApplyAutoPose(root.transform, vis, shooter.transform, hand);

            // Bake the world pose into hand-local space on the ROOT.
            root.transform.SetParent(hand, true);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, heldPath);
            Object.DestroyImmediate(root.gameObject);
            created++;
        }

        // ---- 2. Preview scene ---------------------------------------------------
        // NewScene destroys the Game-scene objects — capture what we still need.
        Vector3 shooterScale = shooter.transform.localScale;
        string gameScenePath = gameScene.path;
        var preview = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var note = new GameObject("README — tweak gun, then Overrides ▸ Apply on Held_* root");
        var noteText = note.AddComponent<TextMesh>();
        noteText.text = "Each shooter holds a Held_<gun> PREFAB INSTANCE under its RightHand.\n" +
                        "Move / rotate / scale the Held_* root, then in the Inspector use\n" +
                        "Overrides ▸ Apply All to save the pose — the game uses it immediately.";
        noteText.characterSize = 0.12f;
        noteText.fontSize = 48;
        note.transform.position = new Vector3(0f, 3.4f, 0f);

        var shooterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ShooterFbx);
        var clip = FirstClip(ShooterFbx);

        int i = 0;
        foreach (var f in gunPaths)
        {
            string gunName = Path.GetFileNameWithoutExtension(f);
            var held = AssetDatabase.LoadAssetAtPath<GameObject>($"{HeldDir}/Held_{gunName}.prefab");
            if (held == null) continue;

            var s = (GameObject)PrefabUtility.InstantiatePrefab(shooterAsset);
            s.name = $"Shooter_{gunName}";
            s.transform.localScale = shooterScale;
            s.transform.position = new Vector3((i % 7) * 2.2f, 0f, (i / 7) * -4f);
            if (clip != null) clip.SampleAnimation(s, 0f); // freeze on the aim pose

            var h = FindHand(s.transform);
            if (h != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(held);
                inst.transform.SetParent(h, false); // adopt authored local pose
            }

            var label = new GameObject($"Label_{gunName}").AddComponent<TextMesh>();
            label.text = gunName;
            label.characterSize = 0.16f;
            label.fontSize = 40;
            label.anchor = TextAnchor.MiddleCenter;
            label.transform.position = s.transform.position + new Vector3(0f, 2.6f, 0f);
            i++;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(6.6f, 1.6f, 5.5f);
            cam.transform.rotation = Quaternion.Euler(6f, 180f, 0f);
        }

        EditorSceneManager.SaveScene(preview, PreviewScenePath);
        EditorSceneManager.OpenScene(gameScenePath); // leave the editor back on Game
        Debug.Log($"[GunPose] prefabs: {created} created, {kept} kept (edits preserved). Preview: {PreviewScenePath}");
    }

    /// <summary>Same auto-alignment the runtime fallback uses (height-based
    /// scale, barrel -Z to shooter forward, centred ahead of the palm).</summary>
    private static void ApplyAutoPose(Transform root, GameObject vis, Transform shooter, Transform hand)
    {
        var b = Measure(vis);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float height = Mathf.Max(0.0001f, b.size.y);

        // Barrel -Z → shooter forward via pure yaw; FromToRotation's arbitrary
        // 180° axis choice flipped every gun upside down.
        root.rotation = shooter.rotation * Quaternion.Euler(0f, 180f, 0f);

        float worldScale = 0.34f / height;
        float len = longest * worldScale;
        if (len < 0.9f) worldScale = 0.9f / longest;
        else if (len > 1.5f) worldScale = 1.5f / longest;
        root.localScale = Vector3.one * worldScale;

        var gb = Measure(vis);
        Vector3 wanted = hand.position + shooter.forward * (longest * worldScale * 0.28f) + shooter.up * 0.015f;
        root.position += wanted - gb.center;
    }

    private static Transform FindHand(Transform root)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>())
            if (tr.name.EndsWith("RightHand")) return tr;
        return null;
    }

    private static AnimationClip FirstClip(string fbxPath)
    {
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview")) return c;
        return null;
    }

    private static Bounds Measure(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }
}
