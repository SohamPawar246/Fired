using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The PLAY-button gunshot flourish: activate this object and it plays a bang,
/// punches a comic "BANG!" starburst in, flashes the screen, then deactivates
/// itself. MainMenuController activates it and waits ~0.45 s before loading the
/// Game scene, so selecting PLAY literally fires the run into existence.
/// </summary>
public class MenuGunshot : MonoBehaviour
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private Image flash;          // full-screen white, starts alpha 0
    [SerializeField] private RectTransform burst;  // comic starburst + BANG! text

    public const float Duration = 0.45f;

    private void OnEnable()
    {
        AudioManager.Instance?.PlayUIBang();
        StartCoroutine(Fire());
    }

    private IEnumerator Fire()
    {
        float t = 0f;
        while (t < Duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Duration);

            // Flash: instant white, fast falloff.
            if (flash != null)
            {
                var c = flash.color;
                c.a = 0.55f * Mathf.Pow(1f - k, 2.2f);
                flash.color = c;
            }

            // Burst: overshoot pop (0 → 1.2 → 1), then shrink out at the end.
            if (burst != null)
            {
                float pop = k < 0.35f
                    ? Mathf.Lerp(0f, 1.2f, k / 0.35f)
                    : Mathf.Lerp(1.2f, k > 0.75f ? 0.6f : 1f, (k - 0.35f) / 0.65f);
                burst.localScale = Vector3.one * pop;
                burst.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-10f, 4f, k));
            }
            yield return null;
        }
        gameObject.SetActive(false); // reset for next time
    }
}
