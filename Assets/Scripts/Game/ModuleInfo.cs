using UnityEngine;

/// <summary>
/// Metadata on every level-module prefab (built by SpaceModuleBuilder).
/// Convention: the module's ENTRY is at the prefab origin (0,0,0), on the ground,
/// with the flight direction entering along +Z. Exits are declared in UNSCALED
/// local space; the streamer uses TransformPoint so prefab scale just works.
/// </summary>
public class ModuleInfo : MonoBehaviour
{
    [System.Serializable]
    public struct Exit
    {
        public Vector3 localPos;  // exit socket (local, unscaled)
        public float yawDelta;    // 0 straight, +90 right, -90 left
    }

    public Exit[] exits;

    [Tooltip("Local-space footprint (unscaled): width (x) and depth (z) from entry.")]
    public Vector2 footprint = new(8, 8);

    [Tooltip("Big open module — streamer may place enemies inside.")]
    public bool roomy;

    [Tooltip("Optional prefab used to seal exits the streamer did NOT choose.")]
    public GameObject doorBlocker;
}
