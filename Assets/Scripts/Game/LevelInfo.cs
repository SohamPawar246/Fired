using UnityEngine;

/// <summary>
/// ============ THE LEVEL DESIGNER'S ENTRY POINT (see Docs/LEVEL_DESIGN_GUIDE.md) ============
/// Put exactly ONE of these in your level (on the root "Level" object). The game
/// finds it at run start and uses it to fire the opening shot and launch the bullet.
///
/// Minimum setup: assign `muzzle` — an empty child placed at the gun barrel tip,
/// with its BLUE arrow (+Z) pointing the direction the bullet should fly.
/// Everything else is optional and has sensible defaults.
/// ============================================================================================
/// </summary>
public class LevelInfo : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("Empty transform at the gun barrel tip. +Z (blue arrow) = flight direction.")]
    public Transform muzzle;

    [Header("Opening shot (optional)")]
    [Tooltip("The shooter character's Animator (e.g. Ch44 with the Gunplay controller). Its default state plays during the intro.")]
    public Animator shooterAnimator;
    [Tooltip("Seconds into the intro when the gun actually fires (BAM + bang + bullet launch).")]
    public float fireDelaySeconds = 1.4f;
    [Tooltip("Spawned at the muzzle at the moment of firing (e.g. Prefabs/Bam VFX). Also reused for big score hits.")]
    public GameObject bamVfxPrefab;

    [Header("Tuning (optional)")]
    public float bulletStartSpeed = 50f;

    [Tooltip("Model used as the bullet's visual (e.g. Kenney foamBulletA). Auto-scaled to ~0.6 m. Empty = simple brass capsule.")]
    public GameObject bulletModel;

    private void OnDrawGizmos()
    {
        if (muzzle == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(muzzle.position, 0.08f);
        Gizmos.DrawRay(muzzle.position, muzzle.forward * 3f);
    }
}
