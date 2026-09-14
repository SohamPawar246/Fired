using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The speed readout — the most important number in the game, since speed IS health.
///
/// The flat cyan bar it replaces said only "some amount". This says three things at once:
///   · how much speed you have          (fill)
///   · how close the floor is           (a redline marking under the low end)
///   · that you are about to die        (fill colour + pulse as it goes critical)
///
/// The redline is a thin stripe along the BOTTOM EDGE of the track, never a panel
/// sitting where the fill would be — a full-height red block at the low end is
/// indistinguishable from a red fill, which made an empty tank look quarter-full.
///
/// Everything visual lives in the scene under SpeedBar — track, redline, ticks, fill,
/// glow and leading cap are all normal objects you can restyle in the Inspector.
/// This component only moves and tints them.
/// </summary>
public class SpeedGauge : MonoBehaviour
{
    [Header("Parts (baked under SpeedBar)")]
    [SerializeField] private RectTransform track;   // the bar background; defines the width
    [SerializeField] private Image fill;
    [SerializeField] private RectTransform cap;     // bright leading edge
    [SerializeField] private Graphic capGlow;       // soft bloom blob behind the cap
    [SerializeField] private Graphic dangerWash;    // redline stripe under the low end of the track

    [Header("Thresholds (normalised 0 = stall, 1 = soft cap)")]
    [Tooltip("Below this the gauge reads as caution and the fill warms toward yellow.")]
    [SerializeField, Range(0f, 1f)] private float cautionAt = 0.38f;
    [Tooltip("Below this the gauge reads as critical: red fill and a pulse. This is the 'you are about to fall out of the air' state.")]
    [SerializeField, Range(0f, 1f)] private float criticalAt = 0.18f;

    [Header("Feel")]
    [Tooltip("How fast the displayed value chases the real one. Lower = heavier, more readable.")]
    [SerializeField] private float followSpeed = 9f;
    [Tooltip("Pulses per second while critical.")]
    [SerializeField] private float criticalPulseHz = 3.2f;
    [Tooltip("Extra brightness punch when speed is refunded by a pickup.")]
    [SerializeField] private float gainPunch = 0.55f;

    private float _shown = 1f;
    private float _punch;
    private float _lastTarget = 1f;

    /// <summary>
    /// Feed the normalised speed each frame (0 = stall floor, 1 = soft cap).
    /// Pass alive:false once the run is over — a dead bullet must read as an EMPTY
    /// tank, with the redline marking dimmed out, or the gauge keeps implying the
    /// player still has speed to spend.
    /// </summary>
    public void SetNormalized(float target01, bool alive = true)
    {
        target01 = Mathf.Clamp01(target01);

        if (!alive)
        {
            _shown = 0f;
            _punch = 0f;
            _lastTarget = 0f;
            ApplyDead();
            return;
        }


        // A jump upward means a pickup just paid out — flash the gauge so the
        // refund is felt, not just seen.
        if (target01 > _lastTarget + 0.012f) _punch = 1f;
        _lastTarget = target01;

        _shown = Mathf.Lerp(_shown, target01, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
        _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 2.2f);

        Apply(_shown);
    }

    /// <summary>Run over: everything off, redline dimmed to an inert trace.</summary>
    private void ApplyDead()
    {
        if (fill != null) { fill.fillAmount = 0f; fill.color = Theme.Negative; }
        if (cap != null) cap.gameObject.SetActive(false);
        if (capGlow != null) capGlow.gameObject.SetActive(false);
        if (dangerWash != null)
        {
            // Fully clear, not a faint trace: any leftover red at the low end still
            // reads as "some speed remaining" on an otherwise empty track.
            var dc = Theme.Negative;
            dc.a = 0f;
            dangerWash.color = dc;
        }
    }

    private void Apply(float t)
    {
        if (fill != null) fill.fillAmount = t;

        // ---- colour by state -------------------------------------------------
        // cyan (safe) → yellow (caution) → pink (critical). The green in Theme is
        // reserved for combos, so it deliberately never appears here.
        Color c;
        if (t <= criticalAt)
            c = Theme.Negative;
        else if (t <= cautionAt)
            c = Color.Lerp(Theme.Negative, Theme.NeonYellow, Mathf.InverseLerp(criticalAt, cautionAt, t));
        else
            c = Color.Lerp(Theme.NeonYellow, Theme.NeonCyan, Mathf.InverseLerp(cautionAt, 1f, t));

        // Critical pulse — the only animated thing on the HUD, so it reads as alarm.
        float pulse = 1f;
        if (t <= criticalAt)
            pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * criticalPulseHz * 2f * Mathf.PI);

        float punchBoost = 1f + gainPunch * _punch;
        if (fill != null) fill.color = c * pulse * punchBoost;

        // ---- leading cap rides the fill edge ---------------------------------
        if (track != null && cap != null)
        {
            float w = track.rect.width;
            var p = cap.anchoredPosition;
            p.x = w * t;
            cap.anchoredPosition = p;
            cap.gameObject.SetActive(t > 0.005f);

            if (cap.TryGetComponent<Graphic>(out var capG))
                capG.color = Color.white * Mathf.Min(1f, pulse * punchBoost);
        }
        if (capGlow != null)
        {
            // Ride the same edge as the cap — a glow pinned to the left end reads as
            // a light leak, not as the head of the bar.
            if (track != null)
            {
                var gp = capGlow.rectTransform.anchoredPosition;
                gp.x = track.rect.width * t;
                capGlow.rectTransform.anchoredPosition = gp;
            }
            var gc = c;
            gc.a = 0.5f * pulse * punchBoost;
            capGlow.color = gc;
            capGlow.gameObject.SetActive(t > 0.005f);
        }

        // ---- redline brightens as you sink toward it -------------------------
        if (dangerWash != null)
        {
            float near = 1f - Mathf.Clamp01(Mathf.InverseLerp(0f, cautionAt, t));
            var dc = Theme.Negative;
            dc.a = Mathf.Lerp(0.25f, 0.95f, near) * (t <= criticalAt ? pulse : 1f);
            dangerWash.color = dc;
        }
    }
}
