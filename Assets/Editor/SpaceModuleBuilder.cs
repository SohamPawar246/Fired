using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the level-module PREFABS from Kenney's modular kits: corridors, turns
/// and rooms with matching entry/exit sockets so ModuleStreamer can chain them
/// seamlessly (entry always at prefab origin, flight = +Z, exits declared in
/// ModuleInfo). Also bakes mesh colliders (ricochet walls) and a ceiling lid.
///
///   Tools > Jam Scaffold > Build Level Module Prefabs (all kits)
///
/// THREE KITS, ONE GRID. Space, Cave and Dungeon are the same modular system:
/// 36 of 40 model names are identical and — verified by measuring the meshes —
/// the XZ footprints match exactly (corridor-wide 8x8, room-large 20x20,
/// corridor 4x4). Only ceiling height differs (4.25 / 4.40 / 4.23), which does
/// not affect floor-plane chaining, and the cave pieces overhang ~0.23 in Z with
/// rock, which simply overlaps the neighbour and helps hide the seam.
///
/// Because of that, every kit is built from ONE layout description below and the
/// socket positions are identical across kits — so a cave module chains onto a
/// space module with no gap. Only the source folder, the door-blocker model and
/// the tint change per kit.
///
/// The kit is a 4-unit grid, ground-centered pivots. Everything scales x1.5 for
/// flight room. Orientation constants are the only thing to tweak if a piece
/// faces the wrong way (verify with screenshots, adjust once, rebuild).
/// </summary>
public static class SpaceModuleBuilder
{
    private const string Cyber = "Assets/Level/Cyberpunk Game Kit-zip";
    private const string Out = "Assets/Prefabs/Levels";
    private const string MatDir = "Assets/Level/_ZoneMaterials";
    private const float S = 1.5f;        // module scale
    private const float WallTop = 4.3f;  // unscaled ceiling height
    // Wide enough to cover a full 8-unit module opening with margin, so a sealed
    // exit has no gap at the edges for the bullet to slip through.
    private const float SealWidth = 9.0f;

    // ---- Orientation constants (raycast-verified in the editor) ---------------
    private const float CorridorYaw = 90f;   // piece is open ±X at yaw 0 → rotate to ±Z
    private const float CornerYawRight = 0f; // openings on -Z and +X (verified)
    private const float CornerYawLeft = 90f; // openings on -Z and -X (verified)
    private const float RoomWideYaw = 0f;    // open all sides; traverse the 12-unit depth

    /// <summary>One environment: where its models live, what seals a door, how it is lit.</summary>
    private class Kit
    {
        public string Id;          // "space" / "cave" / "dungeon"
        public string Dir;         // FBX folder
        public string Prefix;      // prefab name prefix
        public string Blocker;     // model that seals unused exits
        public Color Tint;         // light tone — the environment's mood
        public bool CyberProps;    // cyberpunk-kit set dressing (space only)
    }

    private static readonly Kit[] Kits =
    {
        // Zone 1 — the familiar clean station. Untinted: this is the look the
        // game already shipped with, and the onboarding zone should feel neutral.
        new Kit { Id = "space",   Prefix = "M",         Blocker = "gate-door",
                  Tint = new Color(1.00f, 1.00f, 1.00f), CyberProps = true },

        // Zone 2 — warm rock. The step out of the station should feel organic.
        new Kit { Id = "cave",    Prefix = "M_Cave",    Blocker = "gate-rock",
                  Tint = new Color(1.00f, 0.72f, 0.48f), CyberProps = false },

        // Zone 3 — cold stone. Oppressive, and the furthest most players get.
        new Kit { Id = "dungeon", Prefix = "M_Dungeon", Blocker = "gate-door",
                  Tint = new Color(0.72f, 0.68f, 1.00f), CyberProps = false },
    };

    private static Kit _k;          // kit currently being built
    private static Material _mat;   // its shared tinted material

    [UnityEditor.MenuItem("Tools/FIRED/Build Level Module Prefabs (all kits)")]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Out)) AssetDatabase.CreateFolder("Assets/Prefabs", "Levels");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Level", "_ZoneMaterials");

        foreach (var kit in Kits)
        {
            _k = kit;
            _k.Dir = $"Assets/Level/kenney_modular-{kit.Id}-kit_1.0/Models/FBX format/";
            if (!AssetDatabase.IsValidFolder(_k.Dir.TrimEnd('/')))
            {
                Debug.LogWarning($"[Modules] kit folder missing, skipped: {_k.Dir}");
                continue;
            }
            _mat = ZoneMaterial(kit);

            var blocker = BuildDoorBlocker();
            BuildCorridor(blocker);
            BuildCorridorLong(blocker);
            BuildTurn(blocker, right: true);
            BuildTurn(blocker, right: false);
            BuildRoomLarge(blocker);
            BuildRoomWide(blocker);
            Debug.Log($"[Modules] built '{kit.Id}' modules as {kit.Prefix}_*");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Modules] All kits built in " + Out);
    }

    /// <summary>
    /// One shared URP/Lit material per environment, using that kit's own atlas with
    /// a colour tint. The tint does ALL the work: Kenney ships these kits as colormap
    /// atlases that average out near-neutral grey, so without a tint the three
    /// environments look nearly identical. The kits ship their colormap embedded INSIDE each FBX, which
    /// cannot be recoloured per-zone — a standalone material can, and it is
    /// editable in the Inspector like everything else in this project.
    /// </summary>
    private static Material ZoneMaterial(Kit kit)
    {
        string path = $"{MatDir}/Zone_{kit.Id}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"Assets/Level/kenney_modular-{kit.Id}-kit_1.0/Models/Textures/variation-a.png");
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        else Debug.LogWarning($"[Modules] no variation-a.png for {kit.Id} — material will be untextured.");

        mat.SetColor("_BaseColor", kit.Tint);
        mat.SetFloat("_Smoothness", 0.12f); // matte rock/metal; the neons do the shine
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // =====================================================================
    // Module layouts — IDENTICAL socket geometry for every kit, which is what
    // lets a cave module chain onto a space module with no gap.
    // =====================================================================

    /// <summary>
    /// Seals an exit the streamer did not take.
    ///
    /// The kit's gate models are only ~4-5 units wide but a module opening is the
    /// full 8-unit footprint (12 world units at S=1.5), so a gate ALONE leaves a
    /// ~3-unit gap down each side — which the bullet flies straight through and out
    /// of the map. So the blocker is a solid panel that spans the whole opening,
    /// with the kit's gate art placed on top of it as dressing.
    /// </summary>
    private static GameObject BuildDoorBlocker()
    {
        var root = new GameObject($"{_k.Prefix}_DoorBlocker");

        var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "SealPanel";
        panel.transform.SetParent(root.transform, false);
        panel.transform.localScale = new Vector3(SealWidth, WallTop + 0.6f, 0.5f);
        panel.transform.localPosition = new Vector3(0f, (WallTop + 0.6f) * 0.5f, 0f);
        var pr = panel.GetComponent<Renderer>();
        if (pr != null) pr.sharedMaterial = SealMaterial(_k);

        // Kit gate art in front of the panel so a sealed exit still reads as a door.
        Piece(root.transform, _k.Blocker, new Vector3(0f, 0f, -0.35f), 0f);

        // NOT scaled by S here: the streamer parents this under a module that is
        // already scaled by S, so setting it again would square the scale and the
        // panel would punch out through the corridor walls.
        root.transform.localScale = Vector3.one;
        return Save(root, $"{_k.Prefix}_DoorBlocker");
    }

    /// <summary>Dark, untextured bulkhead behind the gate art — reads as "no way through".</summary>
    private static Material SealMaterial(Kit kit)
    {
        string path = $"{MatDir}/Seal_{kit.Id}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", kit.Tint * 0.16f);
        mat.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void BuildCorridor(GameObject blocker)
    {
        var root = NewRoot($"{_k.Prefix}_Corridor");
        Piece(root.transform, "corridor-wide", new Vector3(0, 0, 4), CorridorYaw);
        Ceiling(root.transform, new Vector3(0, WallTop, 4), new Vector3(8, 0.5f, 8));
        Info(root, new(8, 8), roomy: false, blocker,
            new ModuleInfo.Exit { localPos = new Vector3(0, 0, 8), yawDelta = 0f });
        Save(root, $"{_k.Prefix}_Corridor");
    }

    private static void BuildCorridorLong(GameObject blocker)
    {
        var root = NewRoot($"{_k.Prefix}_CorridorLong");
        Piece(root.transform, "corridor-wide", new Vector3(0, 0, 4), CorridorYaw);
        Piece(root.transform, "corridor-wide", new Vector3(0, 0, 12), CorridorYaw);
        Ceiling(root.transform, new Vector3(0, WallTop, 8), new Vector3(8, 0.5f, 16));
        Info(root, new(8, 16), roomy: false, blocker,
            new ModuleInfo.Exit { localPos = new Vector3(0, 0, 16), yawDelta = 0f });
        Save(root, $"{_k.Prefix}_CorridorLong");
    }

    private static void BuildTurn(GameObject blocker, bool right)
    {
        string name = $"{_k.Prefix}_{(right ? "TurnRight" : "TurnLeft")}";
        var root = NewRoot(name);
        Piece(root.transform, "corridor-wide-corner", new Vector3(0, 0, 4), right ? CornerYawRight : CornerYawLeft);
        Ceiling(root.transform, new Vector3(0, WallTop, 4), new Vector3(8, 0.5f, 8));
        float x = right ? 4f : -4f;
        Info(root, new(8, 8), roomy: false, blocker,
            new ModuleInfo.Exit { localPos = new Vector3(x, 0, 4), yawDelta = right ? 90f : -90f });
        Save(root, name);
    }

    private static void BuildRoomLarge(GameObject blocker)
    {
        var root = NewRoot($"{_k.Prefix}_RoomLarge");
        Piece(root.transform, "room-large", new Vector3(0, 0, 10), 0f);
        Piece(root.transform, "gate", new Vector3(0, 0, 0.4f), 0f); // arch over the entry
        if (_k.CyberProps)
        {
            Prop(root.transform, $"{Cyber}/Computer Large", new Vector3(-7.5f, 0, 5f), 135f, 2.4f);
            Prop(root.transform, $"{Cyber}/Lootbox", new Vector3(7f, 0, 14.5f), -120f, 1.5f);
            Prop(root.transform, $"{Cyber}/Pickup Tank", new Vector3(-6.5f, 0, 15.5f), 60f, 1.6f);
        }
        Ceiling(root.transform, new Vector3(0, WallTop, 10), new Vector3(20, 0.5f, 20));
        Info(root, new(20, 20), roomy: true, blocker,
            new ModuleInfo.Exit { localPos = new Vector3(0, 0, 20), yawDelta = 0f },
            new ModuleInfo.Exit { localPos = new Vector3(10, 0, 10), yawDelta = 90f },
            new ModuleInfo.Exit { localPos = new Vector3(-10, 0, 10), yawDelta = -90f });
        Save(root, $"{_k.Prefix}_RoomLarge");
    }

    private static void BuildRoomWide(GameObject blocker)
    {
        var root = NewRoot($"{_k.Prefix}_RoomWide");
        Piece(root.transform, "room-wide", new Vector3(0, 0, 6), RoomWideYaw);
        Piece(root.transform, "gate", new Vector3(0, 0, 0.4f), 0f);
        if (_k.CyberProps)
        {
            Prop(root.transform, $"{Cyber}/Computer", new Vector3(-8f, 0, 3.5f), 120f, 1.8f);
            Prop(root.transform, $"{Cyber}/Lootbox", new Vector3(8f, 0, 8.5f), -60f, 1.5f);
        }
        Ceiling(root.transform, new Vector3(0, WallTop, 6), new Vector3(20, 0.5f, 12));
        // Open on all four sides (verified) — declare the side openings as exits
        // too, so the streamer either uses them or seals them with doors.
        Info(root, new(20, 12), roomy: true, blocker,
            new ModuleInfo.Exit { localPos = new Vector3(0, 0, 12), yawDelta = 0f },
            new ModuleInfo.Exit { localPos = new Vector3(10, 0, 6), yawDelta = 90f },
            new ModuleInfo.Exit { localPos = new Vector3(-10, 0, 6), yawDelta = -90f });
        Save(root, $"{_k.Prefix}_RoomWide");
    }

    // =====================================================================

    private static GameObject NewRoot(string name)
    {
        var root = new GameObject(name);
        root.transform.localScale = Vector3.one * S; // exits stay unscaled; TransformPoint applies S
        return root;
    }

    private static GameObject Piece(Transform root, string fbx, Vector3 localPos, float yaw)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(_k.Dir + fbx + ".fbx");
        if (asset == null) { Debug.LogWarning($"[Modules] {_k.Id}: missing {fbx}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();
        Tint(go);
        return go;
    }

    /// <summary>Swap the FBX's embedded colormap for this zone's tinted material.</summary>
    private static void Tint(GameObject go)
    {
        if (_mat == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = _mat;
            r.sharedMaterials = mats;
        }
    }

    /// <summary>Cyberpunk-kit prop, ground-snapped at a local grid position.</summary>
    private static void Prop(Transform root, string folder, Vector3 localPos, float yaw, float height)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;
        string path = null;
        foreach (var f in Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories)) { path = f.Replace('\\', '/'); break; }
        if (path == null) return;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);

        // Scale to height and ground at the requested local position.
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            go.transform.localScale *= height / Mathf.Max(0.001f, b.size.y);
            b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            var worldTarget = root.TransformPoint(localPos);
            go.transform.position += worldTarget - new Vector3(b.center.x, b.min.y, b.center.z);
        }
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();
    }

    private static void Ceiling(Transform root, Vector3 center, Vector3 size)
    {
        var go = new GameObject("CeilingLid");
        go.transform.SetParent(root, false);
        var box = go.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }

    private static void Info(GameObject root, Vector2 footprint, bool roomy, GameObject blocker,
        params ModuleInfo.Exit[] exits)
    {
        var info = root.AddComponent<ModuleInfo>();
        info.exits = exits;
        info.footprint = footprint;
        info.roomy = roomy;
        info.doorBlocker = blocker;
    }

    private static GameObject Save(GameObject root, string name)
    {
        string path = $"{Out}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
