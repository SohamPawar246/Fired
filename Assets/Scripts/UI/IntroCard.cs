using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Reusable "logo card" screen: fade in → hold (or play a video) → advance.
///
/// ======================= HOW TO USE A VIDEO ========================
/// 1. Drop your video anywhere under Assets/ with "intro", "studio" or
///    "logo" in the file name (e.g. Assets/Video/StudioIntro.mp4).
/// 2. Run  Tools > Jam Scaffold > Build All Scenes.
/// That's it. The builder copies the file into StreamingAssets and wires it
/// here; this script creates the RenderTexture at runtime, plays the video's
/// own audio, crops it to fill the screen, and advances when it ends.
/// (When a video is wired, the placeholder text is not built at all.)
///
/// Videos play via URL from StreamingAssets — the only method WebGL supports —
/// and the same path works in the editor and desktop builds.
/// ====================================================================
/// </summary>
public class IntroCard : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private string nextSceneName = "EpilepsyWarning";
    [Tooltip("How long the placeholder card holds before auto-advancing (ignored when a video is wired).")]
    [SerializeField] private float holdSeconds = 3f;
    [SerializeField] private bool skippable = true;
    [Tooltip("Input is ignored for this long so a leftover keypress from the previous scene can't skip-chain through multiple cards.")]
    [SerializeField] private float inputDebounceSeconds = 0.5f;

    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private CanvasGroup contentGroup; // fades the card contents in
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoSurface;
    [SerializeField] private string videoFileName = ""; // inside StreamingAssets; empty = text placeholder mode

    [SerializeField] private float fadeInSeconds = 0.5f;

    private float _elapsed;
    private bool _advancing;
    private bool _videoFinished;
    private RenderTexture _videoRt;

    private bool HasVideo => videoPlayer != null && !string.IsNullOrEmpty(videoFileName);

    private void Start()
    {
        if (contentGroup != null)
        {
            contentGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }

        if (HasVideo) StartVideo();
    }

    private void StartVideo()
    {
        videoPlayer.gameObject.SetActive(true);
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = Application.streamingAssetsPath + "/" + videoFileName;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.loopPointReached += _ => _videoFinished = true;
        videoPlayer.errorReceived += (_, msg) =>
        {
            // Bad codec / missing file — don't hang the boot flow, fall through.
            Debug.LogWarning($"[IntroCard] Video failed, advancing: {msg}");
            _videoFinished = true;
        };
        videoPlayer.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        // Render texture sized to the actual video — no manual asset needed.
        _videoRt = new RenderTexture((int)vp.width, (int)vp.height, 0);
        vp.targetTexture = _videoRt;

        if (videoSurface != null)
        {
            videoSurface.texture = _videoRt;
            videoSurface.uvRect = CropToFill((float)vp.width / vp.height);
            videoSurface.gameObject.SetActive(true);
        }

        // The clip's own audio, at the player's master volume. Safe on WebGL:
        // the Boot scene's CLICK TO START gate guarantees a user gesture has
        // already unlocked audio before this scene ever loads.
        vp.SetDirectAudioVolume(0, Mathf.Clamp01(SaveSystem.MasterVolume));
        vp.Play();
    }

    /// <summary>UV rect that crops the video to cover the screen (no letterboxing,
    /// no stretching) — like CSS background-size: cover.</summary>
    private static Rect CropToFill(float videoAspect)
    {
        float screenAspect = (float)Screen.width / Screen.height;
        if (videoAspect > screenAspect)
        {
            float w = screenAspect / videoAspect;
            return new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        float h = videoAspect / screenAspect;
        return new Rect(0f, (1f - h) * 0.5f, 1f, h);
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;

        if (skippable && _elapsed > inputDebounceSeconds && JamInput.AnyPressThisFrame())
        {
            Advance();
            return;
        }

        bool timeUp = HasVideo ? _videoFinished : _elapsed >= holdSeconds;
        if (timeUp) Advance();
    }

    private void Advance()
    {
        if (_advancing) return;
        _advancing = true;
        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        GameManager.Instance.SceneFlow.LoadScene(nextSceneName);
    }

    private void OnDestroy()
    {
        if (_videoRt != null) _videoRt.Release();
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInSeconds)
        {
            t += Time.unscaledDeltaTime;
            contentGroup.alpha = Mathf.Clamp01(t / fadeInSeconds);
            yield return null;
        }
        contentGroup.alpha = 1f;
    }
}
