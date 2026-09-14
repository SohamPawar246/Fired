using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ARSENAL screen — pick your blaster. The selected gun sits large in the
/// centre with the neighbouring guns small at either side; arrows slide the
/// whole row like a carousel (the side gun glides in, grows, and takes the
/// centre while the old centre shrinks out). The centre gun idles on a slow
/// turntable spin.
///
/// The 3-D models live on an isolated rig far below the menu scene, rendered
/// by a private camera into a RenderTexture that a RawImage shows inside the
/// panel — so it works regardless of how the menu canvas is set up.
/// </summary>
public class GunCarousel : MonoBehaviour
{
    [Header("Wiring (set by Tools > Jam Scaffold > Build Gun Select)")]
    public GameObject[] guns;
    public RawImage preview;
    public TMP_Text nameText;

    [Header("Layout")]
    public float slotSpacing = 1.35f;     // world X between centre and side slot
    public float sideScale = 0.45f;       // side guns relative to centre
    public float sideBack = 0.6f;         // side guns pushed away from camera
    public float slideSpeed = 7f;         // carousel ease speed
    public float spinDegPerSec = 40f;     // centre turntable

    private static readonly Vector3 RigHome = new(0f, -500f, 0f);

    private Transform _rig;
    private Camera _rigCam;
    private RenderTexture _rt;
    private Transform[] _items;
    private float[] _baseScale;
    private int _index;
    private float _pos;          // eased carousel position (in index space)
    private float _spin;

    private void Awake()
    {
        // Start on the saved selection.
        _index = 0;
        for (int i = 0; i < (guns?.Length ?? 0); i++)
            if (guns[i] != null && guns[i].name == GunSelection.SelectedName) { _index = i; break; }
        _pos = _index;

        BuildRig();
    }

    private void OnEnable()
    {
        if (_rig != null) _rig.gameObject.SetActive(true);
        UpdateNameText();
    }

    private void OnDisable()
    {
        if (_rig != null) _rig.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        // URP doesn't reliably schedule a runtime-created RT camera on its own,
        // so render it explicitly once per frame while the panel is open.
        if (_rigCam == null || _rt == null) return;
        var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = _rt };
        if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(_rigCam, req))
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(_rigCam, req);
    }

    private void OnDestroy()
    {
        if (_rig != null) Destroy(_rig.gameObject);
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
    }

    private void BuildRig()
    {
        _rig = new GameObject("GunCarouselRig").transform;
        _rig.position = RigHome;

        // Private camera: only ever sees the rig (nothing else lives down here).
        var camGo = new GameObject("RigCam");
        camGo.transform.SetParent(_rig, false);
        camGo.transform.localPosition = new Vector3(0f, 0.15f, -2.6f);
        camGo.transform.localRotation = Quaternion.identity;
        _rigCam = camGo.AddComponent<Camera>();
        _rigCam.clearFlags = CameraClearFlags.SolidColor;
        _rigCam.backgroundColor = Theme.FromHex("#12082A");
        _rigCam.fieldOfView = 34f;
        _rigCam.nearClipPlane = 0.05f;
        _rigCam.farClipPlane = 20f;
        _rigCam.depth = -50f;
        _rigCam.enabled = false; // rendered manually via SubmitRenderRequest
        var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        camData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
        camData.renderShadows = false;

        _rt = new RenderTexture(1024, 512, 16);
        _rigCam.targetTexture = _rt;
        if (preview != null) preview.texture = _rt;

        var key = new GameObject("RigLight").AddComponent<Light>();
        key.transform.SetParent(_rig, false);
        key.transform.localPosition = new Vector3(1.4f, 1.6f, -1.6f);
        key.transform.localRotation = Quaternion.Euler(38f, -35f, 0f);
        key.type = LightType.Directional;
        key.color = Theme.FromHex("#FFF2DC");
        key.intensity = 1.35f;
        key.cullingMask = ~0; // directional only matters down here anyway

        var fill = new GameObject("RigFill").AddComponent<Light>();
        fill.transform.SetParent(_rig, false);
        fill.transform.localPosition = new Vector3(-1.6f, 0.4f, -1.2f);
        fill.type = LightType.Point;
        fill.color = Theme.NeonCyan;
        fill.intensity = 2.2f;
        fill.range = 6f;

        // One normalized item per gun: longest axis along X (profile view).
        int n = guns?.Length ?? 0;
        _items = new Transform[n];
        _baseScale = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (guns[i] == null) continue;
            var holder = new GameObject(guns[i].name).transform;
            holder.SetParent(_rig, false);

            var model = Instantiate(guns[i], holder, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);

            var b = Measure(model);
            // Barrel to the right for the profile view (kit convention: -Z).
            // Pure yaw keeps the gun upright; -90° about Y sends -Z to +X.
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float s = longest > 0.0001f ? 1.05f / longest : 1f;
            model.transform.localScale = Vector3.one * s;

            // Recentre so the holder pivot is the visual centre.
            b = Measure(model);
            model.transform.localPosition -= holder.InverseTransformVector(b.center - holder.position);

            _items[i] = holder;
            _baseScale[i] = 1f;
        }
        Layout(true);
        _rig.gameObject.SetActive(false);
    }

    private void Update()
    {
        int n = _items?.Length ?? 0;
        if (n == 0) return;

        var kb = Keyboard.current;
        var pad = Gamepad.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Move(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Move(1);
            if (kb.enterKey.wasPressedThisFrame) OnEquip();
            if (kb.escapeKey.wasPressedThisFrame) OnBack();
        }
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) Move(-1);
            if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) Move(1);
            if (pad.buttonSouth.wasPressedThisFrame) OnEquip();
            if (pad.buttonEast.wasPressedThisFrame) OnBack();
        }

        // Ease the carousel toward the target index (shortest wrap direction).
        float diff = Mathf.DeltaAngle(_pos * (360f / n), _index * (360f / n)) / (360f / n);
        _pos += diff * (1f - Mathf.Exp(-slideSpeed * Time.unscaledDeltaTime));
        _spin += spinDegPerSec * Time.unscaledDeltaTime;

        Layout(false);
    }

    private void Layout(bool instant)
    {
        int n = _items.Length;
        for (int i = 0; i < n; i++)
        {
            var item = _items[i];
            if (item == null) continue;

            // Wrapped distance from the eased carousel position, in slots.
            float d = i - _pos;
            d -= Mathf.Round(d / n) * n;

            bool visible = Mathf.Abs(d) < 1.7f;
            if (item.gameObject.activeSelf != visible) item.gameObject.SetActive(visible);
            if (!visible) continue;

            float t = Mathf.Clamp(d, -1f, 1f);
            float centre = 1f - Mathf.Abs(t);                       // 1 at middle, 0 at sides
            Vector3 pos = new(
                t * slotSpacing,
                Mathf.Lerp(-0.06f, 0f, centre),
                Mathf.Lerp(sideBack, 0f, centre));
            float scale = Mathf.Lerp(sideScale, 1f, centre) * _baseScale[i];

            item.localPosition = pos;
            item.localScale = Vector3.one * scale;

            // Centre gun turntables; side guns hold a 3/4 profile.
            float yaw = Mathf.Lerp(24f * Mathf.Sign(t == 0f ? 1f : t), _spin % 360f, centre);
            item.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    private void Move(int dir)
    {
        int n = _items.Length;
        _index = ((_index + dir) % n + n) % n;
        AudioManager.Instance?.PlayUIClick();
        UpdateNameText();
    }

    private void UpdateNameText()
    {
        if (nameText != null && guns != null && _index < guns.Length && guns[_index] != null)
            nameText.text = GunSelection.DisplayName(guns[_index].name);
    }

    // ---- Buttons (also wired by the builder) --------------------------------

    public void OnLeft() => Move(-1);
    public void OnRight() => Move(1);

    public void OnEquip()
    {
        if (guns != null && _index < guns.Length && guns[_index] != null)
            GunSelection.SelectedName = guns[_index].name;
        AudioManager.Instance?.PlayUIConfirm();
        UpdateNameText();
        gameObject.SetActive(false);
    }

    public void OnBack()
    {
        AudioManager.Instance?.PlayUIBack();
        gameObject.SetActive(false);
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
