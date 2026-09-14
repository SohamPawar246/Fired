using UnityEngine;

/// <summary>
/// ============ LEVEL DESIGNER COMPONENT (see Docs/LEVEL_DESIGN_GUIDE.md) ============
/// A scoring target the bullet flies THROUGH.
///
/// How to use: add this to any object (skeleton skull, a pickup prop, a sign...),
/// give it a collider with **Is Trigger = ON**, pick a preset — done. The bullet
/// detects it, awards points × multiplier, refunds speed, and pops the comic burst.
///
/// Presets fill the values from the GDD bone table (Skull 500 … Pinky 50).
/// Pick Custom to type your own.
/// ====================================================================================
/// </summary>
public class FiredTarget : MonoBehaviour
{
    public enum Preset { Custom, Skull, Spine, Pelvis, Ribs, Limb, Pinky, Core, MegaCore }

    [Tooltip("Auto-fills points/refund/label/color. Custom = edit fields yourself.")]
    public Preset preset = Preset.Core;

    public int points = 100;
    public float speedRefund = 18f;
    public string label = "CORE";
    public Color color = new(0f, 0.94f, 1f); // neon cyan

    [Tooltip("Slowly rotate the object so it reads as pick-uppable (off for skeletons/props).")]
    public bool spin;

    [Header("X-ray (optional — for bones inside an enemy)")]
    [Tooltip("If this target is a bone inside an EnemyXRay body, wire it here: the hit reveals the skeleton.")]
    public EnemyXRay xrayOwner;

    public bool Available { get; private set; } = true;

    public struct Result
    {
        public int points;
        public float speedRefund;
        public string label;
        public Color color;
        public bool enemyHit;
    }

    /// <summary>Called once by the bullet on pass-through. Hides + destroys shortly after.</summary>
    public Result Consume()
    {
        Available = false;
        if (xrayOwner != null) xrayOwner.Reveal(transform.position);
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Destroy(gameObject, 0.1f);
        return new Result
        {
            points = points,
            speedRefund = speedRefund,
            label = label,
            color = color,
            enemyHit = xrayOwner != null
        };
    }

    private void Update()
    {
        if (spin) transform.Rotate(0f, 0f, 90f * Time.deltaTime, Space.Self);
    }

    private void OnValidate()
    {
        switch (preset)
        {
            case Preset.Skull:    Set(500, 30f, "SKULL!", "#FF2A6D"); break;
            case Preset.Spine:    Set(300, 25f, "SPINE!", "#FF2A6D"); break;
            case Preset.Pelvis:   Set(200, 20f, "PELVIS!", "#FF2A6D"); break;
            case Preset.Ribs:     Set(150, 18f, "RIBS!", "#FF2A6D"); break;
            case Preset.Limb:     Set(100, 14f, "BONE!", "#FF2A6D"); break;
            case Preset.Pinky:    Set(50, 8f, "PINKY!", "#FFE600"); break;
            case Preset.Core:     Set(100, 18f, "CORE", "#00F0FF"); break;
            case Preset.MegaCore: Set(500, 30f, "MEGA", "#FFE600"); break;
        }
    }

    private void Set(int p, float refund, string l, string hex)
    {
        points = p;
        speedRefund = refund;
        label = l;
        if (ColorUtility.TryParseHtmlString(hex, out var c)) color = c;
    }
}
