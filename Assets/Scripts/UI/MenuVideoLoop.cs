using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Looping background video for the main menu.
///
/// HOW TO USE: drop a video anywhere under Assets/ with "menu", "loop" or "bg"
/// in the file name (e.g. Assets/Video/MenuLoop.mp4), then run
/// Tools > Jam Scaffold > Build All Scenes. The builder copies it into
/// StreamingAssets and wires it here. When the video starts, the procedural
/// backdrop is hidden automatically; if no video is wired (or it fails), the
/// procedural backdrop simply stays.
///
/// Muted by default — the menu music keeps playing over it.
/// </summary>
public class MenuVideoLoop : MonoBehaviour
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage surface;
    [SerializeField] private GameObject backdropToHide; // procedural backdrop, hidden once video runs
    [SerializeField] private string videoFileName = ""; // inside StreamingAssets; empty = dormant

    [SerializeField] private bool muteAudio = true;

    [Tooltip("Slight violet dim over the video so UI reads on top of bright frames.")]
    [SerializeField] private Color videoTint = new(0.8f, 0.77f, 0.9f, 1f);

    private RenderTexture _rt;

    private void Start()
    {
        if (videoPlayer == null || string.IsNullOrEmpty(videoFileName)) return; // dormant

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = Application.streamingAssetsPath + "/" + videoFileName;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.errorReceived += (_, msg) =>
            Debug.LogWarning($"[MenuVideoLoop] Video failed, keeping procedural backdrop: {msg}");
        videoPlayer.Prepare();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        _rt = new RenderTexture((int)vp.width, (int)vp.height, 0);
        vp.targetTexture = _rt;

        surface.texture = _rt;
        surface.uvRect = CropToFill((float)vp.width / vp.height);
        surface.color = videoTint;
        surface.enabled = true;

        vp.SetDirectAudioMute(0, muteAudio);
        if (!muteAudio) vp.SetDirectAudioVolume(0, Mathf.Clamp01(SaveSystem.MasterVolume * SaveSystem.MusicVolume));
        vp.Play();

        if (backdropToHide != null) backdropToHide.SetActive(false);
    }

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

    private void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }
}
