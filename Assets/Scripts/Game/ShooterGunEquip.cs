using UnityEngine;

/// <summary>
/// Puts the player's selected blaster (GunSelection) into the shooter's right
/// hand at runtime, replacing whatever gun the scene was authored with. Uses
/// the same measured-longest-axis alignment as the scaffold builder, so every
/// kit gun ends up barrel-forward and correctly sized regardless of how the
/// FBX was modelled.
/// </summary>
public class ShooterGunEquip : MonoBehaviour
{
    [Tooltip("All selectable gun prefabs, wired by Tools > Jam Scaffold > Build Gun Select. Matched to GunSelection by asset name.")]
    public GameObject[] guns;

    [Tooltip("Target world height (m) of the gun's body once in hand — sizing by height keeps chunky and long-thin guns equally readable.")]
    public float heldHeight = 0.34f;
    [Tooltip("Barrel length is clamped into this range regardless of height-based scale.")]
    public Vector2 heldLengthRange = new(0.9f, 1.5f);

    private void Start()
    {
        Transform hand = null;
        foreach (var tr in GetComponentsInChildren<Transform>())
            if (tr.name.EndsWith("RightHand")) { hand = tr; break; }
        if (hand == null) return;

        // Drop the gun the scene shipped with.
        var old = hand.Find("HandGun");
        if (old != null) Destroy(old.gameObject);

        // Hand-authored pose prefab wins (Resources/Guns/Held_<name>, root local
        // transform = the exact in-hand transform; edit them via the GunPreview
        // scene or directly in the prefab). Falls back to auto-alignment.
        string selected = GunSelection.SelectedName;
        var held = Resources.Load<GameObject>("Guns/Held_" + selected)
                ?? Resources.Load<GameObject>("Guns/Held_" + GunSelection.Default);
        if (held != null)
        {
            var g = Instantiate(held);
            g.name = "HandGun";
            g.transform.SetParent(hand, false); // false = keep authored local pose
            return;
        }

        var prefab = Find(selected) ?? Find(GunSelection.Default);
        if (prefab == null) return;

        // Measure the prefab unparented at identity so world axes == local axes.
        var probe = Instantiate(prefab);
        probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var pb = Measure(probe);
        float longest = Mathf.Max(pb.size.x, Mathf.Max(pb.size.y, pb.size.z));
        // Kit guns lie along Z, stand along Y: Y is the visual body height.
        float height = Mathf.Max(0.0001f, pb.size.y);
        Destroy(probe);

        var gun = Instantiate(prefab);
        gun.name = "HandGun";
        gun.transform.SetParent(hand, true);
        // Barrel is -Z: turn it to +Z with a pure YAW (FromToRotation would pick
        // an arbitrary 180° axis and flip the gun upside down).
        gun.transform.rotation = transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        // Height drives the scale; the resulting length is clamped so a sniper
        // stays impressive without a bullpup becoming a pistol.
        float worldScale = heldHeight / height;
        float resultLength = longest * worldScale;
        if (resultLength < heldLengthRange.x) worldScale = heldLengthRange.x / longest;
        else if (resultLength > heldLengthRange.y) worldScale = heldLengthRange.y / longest;
        float heldLength = longest * worldScale;

        float parentScale = hand.lossyScale.x > 0.0001f ? hand.lossyScale.x : 1f;
        gun.transform.localScale = Vector3.one * (worldScale / parentScale);

        // Position by the gun's VISUAL centre, not its pivot — kit pivots vary
        // (grip, origin, base), which is what left some guns floating or buried
        // in the wrist. Centre slightly ahead of the palm along the aim.
        var gb = Measure(gun);
        Vector3 wanted = hand.position + transform.forward * (heldLength * 0.28f) + transform.up * 0.015f;
        gun.transform.position += wanted - gb.center;
    }

    private GameObject Find(string assetName)
    {
        if (guns == null || string.IsNullOrEmpty(assetName)) return null;
        foreach (var g in guns)
            if (g != null && g.name == assetName) return g;
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
