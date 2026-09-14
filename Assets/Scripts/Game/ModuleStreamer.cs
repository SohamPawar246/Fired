using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// THE INFINITE STATION. Chains room/corridor module prefabs (built from the
/// user's Kenney Modular Space Kit) seamlessly until death: each module's entry
/// snaps onto the previous module's chosen exit socket, turns included.
///
/// Per spawned module it also places the gameplay: target rings on the flight
/// spine, frozen X-ray enemies in roomy modules, closed doors on unchosen exits,
/// and a light. Modules behind the bullet are unloaded.
///
/// WRONG WAY rule: flying against the current module's flow for more than a
/// moment triggers a warning and bleeds speed — turn around or stall out.
/// </summary>
public class ModuleStreamer : MonoBehaviour
{
    [System.Serializable]
    public class Entry { public GameObject prefab; public int weight = 1; }

    /// <summary>
    /// One environment stretch of the run. Zones are how difficulty and scenery
    /// escalate WITHIN a single flight (Temple-Run style), instead of the run being
    /// one flat difficulty from the muzzle to the stall.
    ///
    /// All three Kenney kits share the same socket grid — verified by measuring the
    /// meshes: corridor-wide is 8x8 in every kit — so a cave module chains onto a
    /// space module with no gap, and switching kits mid-run is seamless.
    /// </summary>
    [System.Serializable]
    public class Zone
    {
        public string name = "ZONE";
        [Tooltip("Flight distance (m) at which this zone takes over. The first zone must start at 0.")]
        public float startDistance;
        [Tooltip("Modules drawn from while inside this zone.")]
        public Entry[] modules;
        [Tooltip("Lamp colour — the cheapest way to make a zone read as a different place.")]
        public Color lampTint = Color.cyan;
        [Tooltip("Second lamp colour; lamps alternate between the two down the corridor.")]
        public Color lampTintAlt = Color.magenta;
    }

    [Header("Zones (escalation by distance). Leave empty to use the flat list below.")]
    public Zone[] zones;

    [Header("Modules (wired by ExampleLevelBuilder)")]
    public Entry[] modules;
    public GameObject firstModule;    // safe straight start, used twice

    [Header("Gameplay content")]
    public GameObject enemyBody, skeletonInside;
    public GameObject targetA, targetB;
    public Texture2D bodyTex;

    [Header("Flow")]
    public Vector3 startPos = new(0, 0, 2);
    public float startYaw = 0f;
    public int aheadCount = 4;
    public int keepBehind = 2;
    [Tooltip("Seconds the bullet may spend outside the level before the run is force-ended. Backstop against flying off into empty space through any gap.")]
    public float outsideKillSeconds = 4f;

    private Vector3 _cursorPos;
    private Quaternion _cursorRot;
    private int _spawned;

    // Anti-overlap: space turns out and alternate their direction so the chain
    // can't curl back into geometry it already placed.
    private int _sinceTurn = 99;
    private float _lastTurnDir; // -1 left, +1 right, 0 none yet

    private class Live
    {
        public GameObject go;
        public Vector3 entryWorld;
        public Vector3 flowDir;     // horizontal direction entry→exit
        public Vector3 exitWorld;
        public Bounds bounds;       // world, generous
    }
    private readonly List<Live> _live = new();

    private BulletController _bullet;
    private FiredGameController _game;
    private Material _body;
    private float _enemyHeight = 1.85f;
    private float _wrongTimer;
    private float _outsideTimer;

    // Entrance seal: placed once the bullet is inside the track (the opening
    // shot has to fly IN through this doorway first).
    private GameObject _entryBlockerPrefab;
    private Vector3 _entryPos;
    private Quaternion _entryRot;
    private Transform _entryParent;
    private bool _entrySealed;

    private void Awake()
    {
        _body = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _body.SetColor("_BaseColor", Theme.FromHex("#8891B0"));
        if (bodyTex != null) _body.SetTexture("_BaseMap", bodyTex);

        _cursorPos = transform.TransformPoint(startPos);
        _cursorRot = transform.rotation * Quaternion.Euler(0f, startYaw, 0f);

        var level = FindFirstObjectByType<LevelInfo>();
        if (level != null && level.shooterAnimator != null)
        {
            var shooterBounds = Measure(level.shooterAnimator.gameObject);
            if (shooterBounds.size.y > 0.01f)
                _enemyHeight = shooterBounds.size.y;
        }
    }

    private void Update()
    {
        if (_game == null) _game = FindFirstObjectByType<FiredGameController>();
        if (_bullet == null) _bullet = FindFirstObjectByType<BulletController>();

        int at = BulletModuleIndex();

        // Keep enough track ahead of wherever the bullet is.
        while (_live.Count - Mathf.Max(at, 0) < aheadCount + 1)
            SpawnNext();

        // Unload far behind.
        while (at > keepBehind && _live.Count > 0)
        {
            Destroy(_live[0].go);
            _live.RemoveAt(0);
            at--;
        }

        if (!_entrySealed && _entryBlockerPrefab != null && _entryParent != null
            && _bullet != null && at >= 0 && _bullet.Distance > 6f)
        {
            Instantiate(_entryBlockerPrefab, _entryPos, _entryRot, _entryParent);
            _entrySealed = true;
        }

        WrongWayCheck(at);
    }

    private int BulletModuleIndex()
    {
        if (_bullet == null) return -1;
        Vector3 p = _bullet.transform.position;
        for (int i = _live.Count - 1; i >= 0; i--)
            if (_live[i].bounds.Contains(new Vector3(p.x, _live[i].bounds.center.y, p.z)))
                return i;
        return -1;
    }

    private void WrongWayCheck(int at)
    {
        if (_bullet == null || _bullet.IsDead || !_bullet.Launched) { _wrongTimer = 0f; return; }

        // Left the track completely (out the entrance, through a doorway hole…):
        // same treatment as flying against the flow — warn, then bleed out.
        if (at < 0)
        {
            _wrongTimer += Time.deltaTime;
            _outsideTimer += Time.deltaTime;
            if (_wrongTimer > 1.0f)
            {
                _bullet.Bleed(24f * Time.deltaTime);
                _game?.OnWrongWay(_bullet.transform.position + _bullet.transform.forward * 6f);
            }
            // Hard backstop. Bleeding out normally ends the run, but a bullet that
            // slips through a hole while still being refuelled by pickups could
            // otherwise fly through empty space forever, racking up distance with
            // no level around it. Past this point the run is simply over.
            if (_outsideTimer > outsideKillSeconds)
                _game?.OnBulletDied("LEFT THE STATION", _bullet.Distance);
            return;
        }
        _outsideTimer = 0f;

        float dot = Vector3.Dot(_bullet.transform.forward, _live[at].flowDir);
        if (dot < -0.35f)
        {
            _wrongTimer += Time.deltaTime;
            if (_wrongTimer > 1.0f)
            {
                _bullet.Bleed(16f * Time.deltaTime); // fly against the flow → stall out
                _game?.OnWrongWay(_bullet.transform.position + _bullet.transform.forward * 6f);
            }
        }
        else _wrongTimer = Mathf.Max(0f, _wrongTimer - Time.deltaTime * 2f);
    }

    // =====================================================================

    private void SpawnNext()
    {
        var prefab = _spawned < 2 && firstModule != null ? firstModule : Pick();
        if (prefab == null) return;

        var go = Instantiate(prefab, _cursorPos, _cursorRot, transform);
        go.name = $"{prefab.name}_{_spawned}";
        var info = go.GetComponent<ModuleInfo>();
        if (info == null || info.exits == null || info.exits.Length == 0)
        {
            Debug.LogError($"[ModuleStreamer] {prefab.name} has no ModuleInfo/exits");
            return;
        }

        // Choose an exit: straight preferred, turns keep it interesting.
        int exitIdx = ChooseExit(info);
        var exit = info.exits[exitIdx];

        Vector3 exitWorld = go.transform.TransformPoint(exit.localPos);
        Vector3 entryWorld = _cursorPos;
        Vector3 flow = exitWorld - entryWorld; flow.y = 0f;

        // Seal the exits we did not take (closed doors, so no holes into the void).
        if (info.doorBlocker != null)
            for (int i = 0; i < info.exits.Length; i++)
            {
                if (i == exitIdx) continue;
                Vector3 pos = go.transform.TransformPoint(info.exits[i].localPos);
                Quaternion rot = go.transform.rotation * Quaternion.Euler(0f, info.exits[i].yawDelta, 0f);
                Instantiate(info.doorBlocker, pos, rot, go.transform);
            }

        // Remember the level entrance so it can be sealed once the bullet is
        // inside — otherwise a straight-back ricochet flies out past the shooter.
        if (_spawned == 0 && info.doorBlocker != null)
        {
            _entryBlockerPrefab = info.doorBlocker;
            _entryPos = entryWorld;
            _entryRot = _cursorRot * Quaternion.Euler(0f, 180f, 0f);
            _entryParent = go.transform;
        }

        PlaceGameplay(go, info, entryWorld, exitWorld);

        // Generous world bounds for occupancy tests.
        var b = new Bounds(entryWorld, Vector3.one);
        b.Encapsulate(exitWorld);
        b.Encapsulate(go.transform.TransformPoint(new Vector3(-info.footprint.x * 0.5f, 0, 0)));
        b.Encapsulate(go.transform.TransformPoint(new Vector3(info.footprint.x * 0.5f, 0, info.footprint.y)));
        b.Expand(new Vector3(4f, 50f, 4f));

        _live.Add(new Live { go = go, entryWorld = entryWorld, flowDir = flow.normalized, exitWorld = exitWorld, bounds = b });

        _cursorPos = exitWorld;
        _cursorRot = _cursorRot * Quaternion.Euler(0f, exit.yawDelta, 0f);
        if (Mathf.Abs(exit.yawDelta) > 1f)
        {
            _sinceTurn = 0;
            _lastTurnDir = Mathf.Sign(exit.yawDelta);
        }
        else _sinceTurn++;
        _spawned++;
    }

    private GameObject Pick()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var p = PickRaw();
            if (p == null) return null;
            bool isTurn = p.name.Contains("Turn");
            if (isTurn && _sinceTurn < 2) continue;                       // space turns out
            if (isTurn && _lastTurnDir > 0 && p.name.Contains("Right")) continue; // alternate
            if (isTurn && _lastTurnDir < 0 && p.name.Contains("Left")) continue;
            return p;
        }
        return firstModule; // safe straight fallback
    }

    /// <summary>The zone the bullet is currently flying through (null = no zones configured).</summary>
    public Zone CurrentZone
    {
        get
        {
            if (zones == null || zones.Length == 0) return null;
            float d = _bullet != null ? _bullet.Distance : 0f;
            Zone best = zones[0];
            foreach (var z in zones)
                if (z != null && d >= z.startDistance && z.startDistance >= best.startDistance) best = z;
            return best;
        }
    }

    /// <summary>Modules available right now: the current zone's set, else the flat list.</summary>
    private Entry[] ActiveModules()
    {
        var z = CurrentZone;
        return (z != null && z.modules != null && z.modules.Length > 0) ? z.modules : modules;
    }

    private GameObject PickRaw()
    {
        var pool = ActiveModules();
        if (pool == null || pool.Length == 0) return null;
        int total = 0;
        foreach (var e in pool) if (e.prefab != null) total += Mathf.Max(1, e.weight);
        if (total <= 0) return null;
        int roll = Random.Range(0, total);
        foreach (var e in pool)
        {
            if (e.prefab == null) continue;
            roll -= Mathf.Max(1, e.weight);
            if (roll < 0) return e.prefab;
        }
        return null;
    }

    private int ChooseExit(ModuleInfo info)
    {
        if (info.exits.Length == 1) return 0;
        int straight = -1;
        for (int i = 0; i < info.exits.Length; i++)
            if (Mathf.Abs(info.exits[i].yawDelta) < 1f) straight = i;

        // Room side-exits are turns too: respect spacing + alternation.
        if (_sinceTurn < 2 && straight >= 0) return straight;
        if (straight >= 0 && Random.value < 0.55f) return straight;

        for (int attempt = 0; attempt < 6; attempt++)
        {
            int i = Random.Range(0, info.exits.Length);
            float yaw = info.exits[i].yawDelta;
            if (Mathf.Abs(yaw) < 1f) return i;
            if (_lastTurnDir != 0f && Mathf.Sign(yaw) == _lastTurnDir) continue; // alternate
            return i;
        }
        return straight >= 0 ? straight : 0;
    }

    // ---- Gameplay dressing ----------------------------------------------------

    private void PlaceGameplay(GameObject module, ModuleInfo info, Vector3 entry, Vector3 exit)
    {
        Vector3 mid = (entry + exit) * 0.5f;
        Vector3 flow = (exit - entry).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, flow);

        // Target rings along the spine at flyable heights.
        int rings = info.roomy ? 2 : (Random.value < 0.7f ? 1 : 0);
        for (int i = 0; i < rings; i++)
        {
            var src = Random.value < 0.5f ? targetA : targetB;
            if (src == null) break;
            float t = 0.35f + 0.3f * i + Random.value * 0.15f;
            Vector3 pos = Vector3.Lerp(entry, exit, t)
                          + side * ((Random.value * 2f - 1f) * (info.roomy ? 5f : 2f))
                          + Vector3.up * (1.4f + Random.value * 2.2f);
            BuildRing(module.transform, src, pos, flow);
        }

        // Frozen enemies in roomy modules.
        if (info.roomy && enemyBody != null)
        {
            int enemies = 1 + (Random.value < 0.5f ? 1 : 0);
            for (int i = 0; i < enemies; i++)
            {
                Vector3 pos = Vector3.Lerp(entry, exit, 0.35f + 0.35f * i)
                              + side * ((Random.value * 2f - 1f) * 4f);
                pos.y = 0f;
                BuildEnemy(module.transform, pos, flow);
            }
        }

        // A light per module so the interior reads (alternating neon).
        var lamp = new GameObject("ModuleLamp").AddComponent<Light>();
        lamp.transform.SetParent(module.transform, false);
        lamp.transform.position = mid + Vector3.up * 4.5f;
        lamp.type = LightType.Point;
        // Lamp colour comes from the zone, so the corridor visibly changes character
        // as the run escalates. Falls back to the original cyan/pink alternation.
        var zone = CurrentZone;
        bool even = _spawned % 2 == 0;
        lamp.color = zone != null
            ? (even ? zone.lampTint : zone.lampTintAlt)
            : (even ? Theme.NeonCyan : Theme.NeonPink);
        lamp.intensity = 5.5f;
        lamp.range = info.roomy ? 26f : 14f;
    }

    private void BuildRing(Transform parent, GameObject src, Vector3 pos, Vector3 face)
    {
        var go = Instantiate(src, parent);
        go.name = "Ring";
        // Kenney target boards are flat along local X (thickness axis), so after
        // aiming local Z down the flow, yaw 90° so the face greets the bullet.
        go.transform.rotation = Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
        var b = Measure(go);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.01f) go.transform.localScale *= 1.7f / longest;
        b = Measure(go);
        go.transform.position += pos - b.center;

        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.05f / Mathf.Max(0.0001f, go.transform.lossyScale.x);
        var t = go.AddComponent<FiredTarget>();
        t.points = 10; t.speedRefund = 18f; t.label = "RING"; t.color = Theme.NeonCyan;
        t.spin = false;
    }

    private void BuildEnemy(Transform parent, Vector3 groundPos, Vector3 face)
    {
        var body = Instantiate(enemyBody, parent);
        body.name = "Enemy";
        var anim = body.GetComponent<Animator>();
        if (anim != null) Destroy(anim);

        var xray = body.GetComponent<EnemyXRay>();
        if (xray == null) xray = body.AddComponent<EnemyXRay>();

        var b = Measure(body);
        if (b.size.y > 0.01f) body.transform.localScale *= _enemyHeight / b.size.y;
        body.transform.rotation = Quaternion.LookRotation(-face); // face the incoming bullet
        b = Measure(body);
        body.transform.position += groundPos - new Vector3(b.center.x, b.min.y, b.center.z);
        b = Measure(body);

        foreach (var r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _body;

        if (xray.bodyRenderers == null || xray.bodyRenderers.Length == 0)
            xray.bodyRenderers = body.GetComponentsInChildren<Renderer>(true);

        GameObject skel = null;
        if (xray.skeleton != null)
        {
            skel = xray.skeleton;
        }
        else if (skeletonInside != null)
        {
            skel = Instantiate(skeletonInside, body.transform);
            skel.name = "SkeletonInside";
            var sb = Measure(skel);
            if (sb.size.y > 0.01f) skel.transform.localScale *= (b.size.y * 0.96f) / sb.size.y;
            skel.transform.rotation = body.transform.rotation;
            sb = Measure(skel);
            skel.transform.position += b.center - sb.center;
            skel.SetActive(false);
        }
        xray.skeleton = skel;

        float inv = 1f / Mathf.Max(0.0001f, body.transform.lossyScale.y);
        Bone("Target_Skull", 50, 30f, "SKULL!", new Vector3(b.center.x, b.max.y - b.size.y * 0.07f, b.center.z), 0.24f * inv);
        Bone("Target_Ribs", 15, 18f, "RIBS!", new Vector3(b.center.x, b.min.y + b.size.y * 0.68f, b.center.z), 0.34f * inv);
        Bone("Target_Pelvis", 20, 20f, "PELVIS!", new Vector3(b.center.x, b.min.y + b.size.y * 0.5f, b.center.z), 0.26f * inv);

        var lamp = new GameObject("EnemyLamp").AddComponent<Light>();
        lamp.transform.SetParent(body.transform, false);
        lamp.transform.position = b.center + Vector3.up * 2.4f;
        lamp.type = LightType.Spot;
        lamp.spotAngle = 65f;
        lamp.color = Theme.NeonCyan;
        lamp.intensity = 13f;
        lamp.range = 6f;
        lamp.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        void Bone(string name, int pts, float refund, string label, Vector3 worldPos, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(body.transform, false);
            go.transform.position = worldPos;
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = radius;
            var t = go.AddComponent<FiredTarget>();
            t.points = pts; t.speedRefund = refund; t.label = label; t.color = Theme.NeonPink;
            t.xrayOwner = xray;
        }
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
