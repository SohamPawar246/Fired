using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The ONLY script in the Boot scene. GameManager self-instantiates before any
/// scene loads (see GameManager.Bootstrap), so Boot just kicks off the intro.
///
/// On WebGL it first shows CLICK TO START and waits for a press: browsers only
/// allow audio (and unmuted video) after a user gesture, so gating here is what
/// lets the studio intro play WITH sound in web builds.
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    private IEnumerator Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        BuildPrompt();
        // A frame of grace so a click that launched the page can't leak through.
        yield return null;
        while (!JamInput.AnyPressThisFrame()) yield return null;
#else
        yield return null;
#endif
        GameManager.Instance.SceneFlow.LoadScene("StudioIntro");
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void BuildPrompt()
    {
        var canvasGo = new GameObject("BootCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var txtGo = new GameObject("Prompt", typeof(RectTransform));
        txtGo.transform.SetParent(canvasGo.transform, false);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = "CLICK TO START";
        txt.fontSize = 72;
        txt.color = new Color(1f, 0.9f, 0f);
        txt.alignment = TextAlignmentOptions.Center;
        var rt = (RectTransform)txtGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(1200, 120);
        txtGo.AddComponent<PulseAlpha>();
    }
#endif
}
