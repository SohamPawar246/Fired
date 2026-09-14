using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// THE one way to change scenes in this project. Never call
/// SceneManager.LoadScene directly — call:
///     GameManager.Instance.SceneFlow.LoadScene("SceneName");
///
/// Transition: a slightly-skewed NEON WIPE — a deep-violet panel with a glowing
/// cyan + pink leading edge sweeps left → right across the screen (same
/// direction the bullet flies), the scene swaps behind it, and it sweeps off.
/// Built entirely in code at Awake; runs on unscaled time so it works while
/// paused. Everything is theme-colored via Theme.cs.
/// </summary>
public class SceneFlowController : MonoBehaviour
{
    [Tooltip("Seconds for each half of the wipe (cover, then reveal).")]
    [SerializeField] private float wipeSeconds = 0.42f;
    [SerializeField] private float wipeTiltDegrees = 8f;

    private CanvasGroup _group;
    private RectTransform _wipe;
    private bool _isLoading;

    public bool IsLoading => _isLoading;

    private void Awake()
    {
        BuildWipeCanvas();
    }

    /// <summary>Wipe covers → load scene → wipe reveals. Safe from any scene.</summary>
    public void LoadScene(string sceneName)
    {
        if (_isLoading) return; // ignore double-clicks mid-transition
        StartCoroutine(LoadRoutine(sceneName));
    }

    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        _isLoading = true;
        _group.blocksRaycasts = true; // eat clicks during the transition

        // Size the wipe for the current resolution (oversized so the tilt never
        // exposes corners) and sweep it in from the left.
        float w = Screen.width;
        _wipe.sizeDelta = new Vector2(w * 2.6f, Screen.height * 2.4f);
        yield return Slide(-2.4f * w, 0f);

        // Anything that paused the game must not leak into the next scene.
        Time.timeScale = 1f;

        var op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;

        // Continue the sweep off to the right, revealing the new scene.
        yield return Slide(0f, 2.4f * w);
        _wipe.anchoredPosition = new Vector2(-3f * w, 0f); // park off-screen

        _group.blocksRaycasts = false;
        _isLoading = false;
    }

    /// <summary>Eased horizontal sweep on unscaled time.</summary>
    private IEnumerator Slide(float fromX, float toX)
    {
        float t = 0f;
        while (t < wipeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / wipeSeconds);
            k = k * k * (3f - 2f * k); // smoothstep
            _wipe.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, k), 0f);
            yield return null;
        }
        _wipe.anchoredPosition = new Vector2(toX, 0f);
    }

    // ---- Construction ------------------------------------------------------------

    private void BuildWipeCanvas()
    {
        var canvasGo = new GameObject("WipeCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // above absolutely everything

        _group = canvasGo.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // The sweeping panel, slightly tilted for style.
        var wipeGo = new GameObject("Wipe", typeof(RectTransform));
        _wipe = (RectTransform)wipeGo.transform;
        _wipe.SetParent(canvasGo.transform, false);
        _wipe.localRotation = Quaternion.Euler(0, 0, wipeTiltDegrees);
        _wipe.sizeDelta = new Vector2(Screen.width * 2.6f, Screen.height * 2.4f);
        _wipe.anchoredPosition = new Vector2(-3f * Screen.width, 0f); // parked off-screen

        MakeChild(_wipe, "Panel", Theme.Background, stretch: true);

        // Neon comet edges on BOTH sides of the panel: the right edge leads while
        // covering, the left edge trails while revealing — the glowing boundary is
        // visible through the whole transition.
        MakeEdge("EdgeGlowR", 130f, 0f, new Color(Theme.NeonCyan.r, Theme.NeonCyan.g, Theme.NeonCyan.b, 0.25f), right: true);
        MakeEdge("EdgeLineR", 16f, 0f, Theme.NeonCyan, right: true);
        MakeEdge("EdgePinkR", 8f, -34f, new Color(Theme.NeonPink.r, Theme.NeonPink.g, Theme.NeonPink.b, 0.8f), right: true);

        MakeEdge("EdgeGlowL", 130f, 0f, new Color(Theme.NeonCyan.r, Theme.NeonCyan.g, Theme.NeonCyan.b, 0.25f), right: false);
        MakeEdge("EdgeLineL", 16f, 0f, Theme.NeonCyan, right: false);
        MakeEdge("EdgePinkL", 8f, 34f, new Color(Theme.NeonPink.r, Theme.NeonPink.g, Theme.NeonPink.b, 0.8f), right: false);
    }

    private void MakeEdge(string name, float width, float inset, Color color, bool right)
    {
        var img = MakeChild(_wipe, name, color, stretch: false);
        var rt = img.rectTransform;
        float ax = right ? 1f : 0f;
        rt.anchorMin = new Vector2(ax, 0);
        rt.anchorMax = new Vector2(ax, 1);
        rt.pivot = new Vector2(ax, 0.5f);
        rt.sizeDelta = new Vector2(width, 0);
        rt.anchoredPosition = new Vector2(inset, 0);
    }

    private static Image MakeChild(RectTransform parent, string name, Color color, bool stretch)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
