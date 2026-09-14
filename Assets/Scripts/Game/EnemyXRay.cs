using UnityEngine;

/// <summary>
/// The GDD X-ray reveal: an enemy body with a skeleton hidden inside. When the
/// bullet passes through one of its FiredTargets (skull/ribs...), the body swaps
/// to the holographic fresnel shader, the skeleton lights up inside it, and a
/// red flash marks the hit bone.
///
/// LEVEL DESIGNERS: place a body model + skeleton child, add this to the root,
/// wire the two fields, and point each bone FiredTarget's `xrayOwner` here.
/// (ExampleLevelBuilder shows the full pattern.)
/// </summary>
public class EnemyXRay : MonoBehaviour
{
    [Tooltip("The flesh/body renderers that turn holographic on reveal.")]
    public Renderer[] bodyRenderers;
    [Tooltip("Skeleton object hidden inside the body; enabled on reveal.")]
    public GameObject skeleton;

    private static Material _holo;
    private bool _revealed;

    public void Reveal(Vector3 hitPos)
    {
        if (!_revealed)
        {
            _revealed = true;

            if (_holo == null)
            {
                var shader = Shader.Find("FIRED/HoloFresnel");
                _holo = shader != null ? new Material(shader)
                                       : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                _holo.SetColor("_Color", new Color(0f, 0.94f, 1f, 0.4f));
            }

            if (bodyRenderers != null)
                foreach (var r in bodyRenderers)
                {
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = _holo;
                    r.sharedMaterials = mats;
                }

            if (skeleton != null) skeleton.SetActive(true);
        }

        // Red flash at the struck bone (works for repeat hits on other bones too).
        var flash = new GameObject("BoneFlash").AddComponent<Light>();
        flash.transform.position = hitPos;
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.16f, 0.35f);
        flash.intensity = 14f;
        flash.range = 3.5f;
        Destroy(flash.gameObject, 1.2f);
    }
}
