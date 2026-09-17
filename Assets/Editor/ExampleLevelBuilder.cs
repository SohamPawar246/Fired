using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds "Level_Example" in the open Game scene: the shooter (user's Ch44 +
/// Gunplay anim + a blaster-kit gun in hand), the LevelInfo, and a CityStreamer
/// wired to the user's assets — the streamer then generates the infinite city
/// at runtime. Running this again replaces only "Level_Example".
///
///   Tools > Jam Scaffold > Build Example Level (into Game scene)
/// </summary>
public static class ExampleLevelBuilder
{
    // ---- User asset paths (update here if files move) -------------------------
    private const string ShooterFbx = "Assets/Gunplay.fbx";
    private const string ShooterController = "Assets/Gunplay.controller";
    private const string EnemyFbx = "Assets/Ch44_nonPBR.fbx";
    private const string DwarfEnemyPrefab = "Assets/Prefabs/Enemies/DwarfEnemy.prefab";
    private const string EnemyPrefab = "Assets/Prefabs/Enemies/EnemyXRay.prefab";
    private const string SkeletonFbx = "Assets/Skelton.fbx";
    private const string BamPrefab = "Assets/Prefabs/Bam VFX.prefab";
    private const string CyberDir = "Assets/Level/Cyberpunk Game Kit-zip";
    private const string KenneyBuildDir = "Assets/Level/kenney_modular-buildings/Models/FBX format";
    private const string GunDir = "Assets/Gun/kenney_blasterKit/Models/FBX format";
    private const string ShooterDiffuse = "Assets/Textures/Ch44_1001_Diffuse.png";

    private static Transform _root;

    [UnityEditor.MenuItem("Tools/FIRED/Build Example Level (into Game scene)")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Game")
            scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");

        var old = GameObject.Find("Level_Example");
        if (old != null) Object.DestroyImmediate(old);

        // Remove the loose kit pieces that were hand-dropped at the scene root
        // (superseded by the module prefabs).
        string[] stray = { "room-small", "room-wide", "room-corner", "template-floor-detail",
                           "template-floor-layer-hole", "corridor-wide-end", "corridor-wide-junction",
                           "template-corner" };
        foreach (var name in stray)
        {
            var go = GameObject.Find(name);
            if (go != null && go.transform.parent == null) Object.DestroyImmediate(go);
        }

        _root = new GameObject("Level_Example").transform;

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = Theme.FromHex("#9AA6FF");
        sun.intensity = 0.75f;
        sun.transform.SetParent(_root, false);
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        BuildStreamer();
        BuildShooterAndInfo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ExampleLevel] Built (streamer-driven infinite city).");
    }

    // =====================================================================

    private static void BuildStreamer()
    {
        var go = new GameObject("ModuleStreamer");
        go.transform.SetParent(_root, false);
        var s = go.AddComponent<ModuleStreamer>();

        const string P = "Assets/Prefabs/Levels";
        s.firstModule = Load($"{P}/M_Corridor.prefab");

        // ---- ZONES ----------------------------------------------------------
        // Escalation happens INSIDE a run, Temple-Run style: the world changes
        // character as you get deeper, so newcomers are taught by the easy opening
        // and veterans get new scenery for surviving.
        //
        // The distance bands are set from real playtest data (first runs 5-10s,
        // learning 10-20s, experienced ~50s) against MEASURED flight speed of
        // roughly 11-20 m/s. Zone 2 at 150 m lands inside the learning band and
        // Zone 3 at 400 m inside a good run: an earlier 250/600 split put both
        // zones past where almost everyone dies, so the new scenery never showed up.
        s.zones = new[]
        {
            new ModuleStreamer.Zone
            {
                name = "STATION",
                startDistance = 0f,
                lampTint = Theme.NeonCyan,
                lampTintAlt = Theme.NeonPink,
                modules = KitSet(P, "M", corridor: 4, corridorLong: 3, turn: 1, roomLarge: 2, roomWide: 2),
            },
            new ModuleStreamer.Zone
            {
                name = "THE CAVES",
                startDistance = 150f,
                lampTint = Theme.FromHex("#FF9A3C"),
                lampTintAlt = Theme.FromHex("#FFE600"),
                modules = KitSet(P, "M_Cave", corridor: 3, corridorLong: 2, turn: 2, roomLarge: 3, roomWide: 2),
            },
            new ModuleStreamer.Zone
            {
                name = "THE DEPTHS",
                startDistance = 400f,
                lampTint = Theme.NeonPurple,
                lampTintAlt = Theme.NeonPink,
                modules = KitSet(P, "M_Dungeon", corridor: 2, corridorLong: 1, turn: 3, roomLarge: 3, roomWide: 2),
            },
        };

        // Keep the flat list populated as a fallback for anything that ignores zones.
        s.modules = s.zones[0].modules;

        s.targetA = Load($"{GunDir}/targetA.fbx");
        s.targetB = Load($"{GunDir}/targetB.fbx");
        s.enemyBody = Load(DwarfEnemyPrefab) ?? Load(EnemyPrefab) ?? Load(EnemyFbx);
        s.skeletonInside = Load(SkeletonFbx);
        s.bodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ShooterDiffuse);
        s.startPos = new Vector3(0f, 0f, 1.5f);
        s.startYaw = 0f;
    }

    /// <summary>
    /// One zone's module pool. Turn weight rises and corridor weight falls with
    /// depth, so later zones are twistier without needing any new geometry.
    /// Missing prefabs are skipped, so a kit that has not been built yet simply
    /// contributes nothing instead of throwing.
    /// </summary>
    private static ModuleStreamer.Entry[] KitSet(string dir, string prefix,
        int corridor, int corridorLong, int turn, int roomLarge, int roomWide)
    {
        var list = new System.Collections.Generic.List<ModuleStreamer.Entry>();
        void Add(string name, int weight)
        {
            var prefab = Load($"{dir}/{prefix}_{name}.prefab");
            if (prefab != null) list.Add(new ModuleStreamer.Entry { prefab = prefab, weight = weight });
            else Debug.LogWarning($"[ExampleLevel] missing module {prefix}_{name} — run Tools > Jam Scaffold > Build Level Module Prefabs (all kits)");
        }
        Add("Corridor", corridor);
        Add("CorridorLong", corridorLong);
        Add("TurnRight", turn);
        Add("TurnLeft", turn);
        Add("RoomLarge", roomLarge);
        Add("RoomWide", roomWide);
        return list.ToArray();
    }

    private static void BuildShooterAndInfo()
    {
        var info = _root.gameObject.AddComponent<LevelInfo>();

        var shooterAsset = Load(ShooterFbx);
        Transform muzzle;
        if (shooterAsset != null)
        {
            var shooter = (GameObject)PrefabUtility.InstantiatePrefab(shooterAsset);
            shooter.name = "Shooter";
            shooter.transform.SetParent(_root, false);

            var b = MeasureBounds(shooter);
            float scale = b.size.y > 0.01f ? 1.85f / b.size.y : 1f;
            shooter.transform.localScale = Vector3.one * scale;
            shooter.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2f), Quaternion.identity);

            var animator = shooter.GetComponent<Animator>();
            if (animator == null) animator = shooter.AddComponent<Animator>();
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ShooterController);
            info.shooterAnimator = animator;

            FixCharacterMaterials(shooter);
            AttachGunToHand(shooter);

            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(shooter.transform, false);
            muzzle.position = shooter.transform.position + new Vector3(0.15f, 1.42f, 0.9f);
            muzzle.rotation = Quaternion.identity;

            var fill = new GameObject("ShooterFill").AddComponent<Light>();
            fill.transform.SetParent(_root, false);
            fill.transform.position = muzzle.position + new Vector3(1.4f, 0.8f, -0.6f);
            fill.type = LightType.Point;
            fill.color = Theme.FromHex("#FFD9A8");
            fill.intensity = 5f;
            fill.range = 7f;
        }
        else
        {
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(_root, false);
            muzzle.position = new Vector3(0f, 1.5f, -1f);
        }

        info.muzzle = muzzle;
        info.fireDelaySeconds = 0.4f;
        info.bulletStartSpeed = 36f;
        info.bamVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BamPrefab);
        info.bulletModel = Load($"{GunDir}/foamBulletA.fbx");
    }

    /// <summary>Blaster from the user's Gun kit, clipped into the right hand with
    /// its measured longest axis aligned to the shooter's forward (no guessing).</summary>
    private static void AttachGunToHand(GameObject shooter)
    {
        Transform hand = null;
        foreach (var tr in shooter.GetComponentsInChildren<Transform>())
            if (tr.name.EndsWith("RightHand")) { hand = tr; break; }
        if (hand == null) return;

        var asset = Load($"{GunDir}/blasterG.fbx") ?? LoadFirst(GunDir);
        if (asset == null) return;

        // Measure the asset UNPARENTED at identity: world axes == local axes.
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var pb = MeasureBounds(probe);
        Vector3 longAxis =
            (pb.size.x >= pb.size.y && pb.size.x >= pb.size.z) ? Vector3.right :
            (pb.size.y >= pb.size.x && pb.size.y >= pb.size.z) ? Vector3.up : Vector3.forward;
        float longest = Mathf.Max(pb.size.x, Mathf.Max(pb.size.y, pb.size.z));
        Object.DestroyImmediate(probe);

        var gun = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        gun.name = "HandGun";
        gun.transform.SetParent(hand, true);

        // Barrel along the shooter's aim (world +Z), grip near the palm.
        gun.transform.rotation = shooter.transform.rotation *
                                 Quaternion.FromToRotation(longAxis, Vector3.forward);
        float worldScale = longest > 0.0001f ? 0.5f / longest : 1f;
        float parentScale = hand.lossyScale.x > 0.0001f ? hand.lossyScale.x : 1f;
        gun.transform.localScale = Vector3.one * (worldScale / parentScale);
        gun.transform.position = hand.position + shooter.transform.forward * 0.16f
                                               + shooter.transform.up * 0.01f;
    }

    private static void FixCharacterMaterials(GameObject character)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ShooterDiffuse);
        if (tex == null) return;
        foreach (var r in character.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", tex);
                mat.SetFloat("_Smoothness", 0.25f);
                mats[m] = mat;
            }
            r.sharedMaterials = mats;
        }
    }

    // ---- helpers -------------------------------------------------------------

    private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    private static GameObject LoadFirst(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return null;
        foreach (var f in Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories))
            return Load(f.Replace('\\', '/'));
        return null;
    }

    private static Bounds MeasureBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}
