using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central audio hub (child of the persistent GameManager). Two music sources
/// crossfade between tracks; one SFX source plays one-shots.
///
/// PLACEHOLDER AUDIO: if the inspector clip slots below are empty, procedural
/// clips are generated at startup (see ProceduralAudio.cs) so the shell is never
/// silent. To use real audio, just assign clips on the GameManager prefab
/// (Assets/Resources/GameManager.prefab) — no code changes needed.
///
/// Volumes are simple floats (master * music/sfx), loaded from SaveSystem and
/// driven live by the Settings menu.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music (leave empty to use generated placeholder pads)")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;

    [Header("UI SFX (leave empty to use generated placeholder blips)")]
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip uiHover;
    [SerializeField] private AudioClip uiConfirm;
    [SerializeField] private AudioClip uiBack;
    [SerializeField] private AudioClip uiBang; // menu gunshot (PLAY flourish)

    [Header("Gameplay SFX (loaded from Resources/Audio if empty)")]
    [SerializeField] private AudioClip sfxRicochet;
    [SerializeField] private AudioClip sfxRicochetHard;
    [SerializeField] private AudioClip sfxScore;
    [SerializeField] private AudioClip sfxEnemyImpact;
    [SerializeField] private AudioClip sfxStall;

    [Header("Crossfade")]
    [SerializeField] private float musicFadeSeconds = 1.2f;

    private AudioSource _musicA, _musicB, _sfx;
    private bool _usingA = true;
    private Coroutine _crossfade;
    private float _musicTrackVolume = 1f; // per-track headroom, kept for ApplyVolumes

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _musicA = gameObject.AddComponent<AudioSource>();
        _musicB = gameObject.AddComponent<AudioSource>();
        _sfx = gameObject.AddComponent<AudioSource>();
        _musicA.loop = _musicB.loop = true;
        _musicA.playOnAwake = _musicB.playOnAwake = _sfx.playOnAwake = false;

        GeneratePlaceholdersIfNeeded();
        ApplyVolumes();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    /// <summary>Music policy: gameplay track in the Game scene, menu track everywhere
    /// else, silence during intro/warning. Adjust here when the game exists.</summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "Boot":
            case "StudioIntro":
            case "EpilepsyWarning":
                break; // keep whatever is playing (nothing, on first boot)
            case "Game":
                PlayMusic(gameplayMusic);
                break;
            default:
                PlayMusic(menuMusic);
                break;
        }
    }

    // ---- Music ---------------------------------------------------------------

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        var current = _usingA ? _musicA : _musicB;
        if (current.clip == clip && current.isPlaying) return; // already on it

        if (_crossfade != null) StopCoroutine(_crossfade);
        _crossfade = StartCoroutine(CrossfadeTo(clip));
    }

    private IEnumerator CrossfadeTo(AudioClip clip)
    {
        var from = _usingA ? _musicA : _musicB;
        var to = _usingA ? _musicB : _musicA;
        _usingA = !_usingA;

        to.clip = clip;
        to.volume = 0f;
        to.Play();

        float target = MusicVolume01();
        float t = 0f;
        float fromStart = from.volume;
        while (t < musicFadeSeconds)
        {
            t += Time.unscaledDeltaTime; // music keeps fading while paused
            float k = Mathf.Clamp01(t / musicFadeSeconds);
            to.volume = Mathf.Lerp(0f, target, k);
            from.volume = Mathf.Lerp(fromStart, 0f, k);
            yield return null;
        }
        from.Stop();
        to.volume = target;
        _crossfade = null;
    }

    // ---- SFX -------------------------------------------------------------------

    /// <summary>Generic one-shot. Slight random pitch keeps repeated sounds fresh.</summary>
    public void PlaySFX(AudioClip clip, float pitchJitter = 0.05f)
    {
        if (clip == null) return;
        _sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        _sfx.PlayOneShot(clip, SfxVolume01());
    }

    // Named helpers so UI code reads nicely and clips stay swappable in one place.
    public void PlayUIClick() => PlaySFX(uiClick);
    public void PlayUIHover() => PlaySFX(uiHover, 0.02f);
    public void PlayUIConfirm() => PlaySFX(uiConfirm);
    public void PlayUIBack() => PlaySFX(uiBack);
    public void PlayUIBang() => PlaySFX(uiBang, 0.03f);

    public void PlayRicochet(bool hard) => PlaySFX(hard ? sfxRicochetHard : sfxRicochet, 0.08f);
    public void PlayScore() => PlaySFX(sfxScore, 0.06f);
    public void PlayEnemyImpact() => PlaySFX(sfxEnemyImpact, 0.05f);
    public void PlayStall() => PlaySFX(sfxStall, 0.04f);

    // ---- Volume (wired to Settings sliders via these setters) -------------------

    public void SetMasterVolume(float v) { SaveSystem.MasterVolume = Mathf.Clamp01(v); ApplyVolumes(); }
    public void SetMusicVolume(float v) { SaveSystem.MusicVolume = Mathf.Clamp01(v); ApplyVolumes(); }
    public void SetSfxVolume(float v) { SaveSystem.SfxVolume = Mathf.Clamp01(v); ApplyVolumes(); }

    private float MusicVolume01() => SaveSystem.MasterVolume * SaveSystem.MusicVolume * _musicTrackVolume;
    private float SfxVolume01() => SaveSystem.MasterVolume * SaveSystem.SfxVolume;

    public void ApplyVolumes()
    {
        var current = _usingA ? _musicA : _musicB;
        if (_crossfade == null) current.volume = MusicVolume01();
    }

    // ---- Placeholder generation --------------------------------------------------

    private void GeneratePlaceholdersIfNeeded()
    {
        // Real clips first (Kenney CC0 packs, see Resources/Audio/LICENSE_Kenney.txt),
        // procedural blips only as a last resort.
        if (uiClick == null) uiClick = Resources.Load<AudioClip>("Audio/ui_click");
        if (uiHover == null) uiHover = Resources.Load<AudioClip>("Audio/ui_hover");
        if (uiConfirm == null) uiConfirm = Resources.Load<AudioClip>("Audio/ui_confirm");
        if (uiBack == null) uiBack = Resources.Load<AudioClip>("Audio/ui_back");
        if (uiBang == null) uiBang = Resources.Load<AudioClip>("Audio/ui_bang");
        if (sfxRicochet == null) sfxRicochet = Resources.Load<AudioClip>("Audio/sfx_ricochet");
        if (sfxRicochetHard == null) sfxRicochetHard = Resources.Load<AudioClip>("Audio/sfx_ricochet_hard");
        if (sfxScore == null) sfxScore = Resources.Load<AudioClip>("Audio/sfx_score");
        if (sfxEnemyImpact == null) sfxEnemyImpact = Resources.Load<AudioClip>("Audio/sfx_enemy_impact");
        if (sfxStall == null) sfxStall = Resources.Load<AudioClip>("Audio/sfx_stall");

        if (uiClick == null) uiClick = ProceduralAudio.Blip("gen_click", 880f, 620f, 0.07f, 0.45f);
        if (uiHover == null) uiHover = ProceduralAudio.Blip("gen_hover", 1250f, 1250f, 0.035f, 0.18f);
        if (uiConfirm == null) uiConfirm = ProceduralAudio.Blip("gen_confirm", 660f, 990f, 0.14f, 0.4f);
        if (uiBack == null) uiBack = ProceduralAudio.Blip("gen_back", 520f, 340f, 0.11f, 0.35f);
        if (uiBang == null) uiBang = ProceduralAudio.GunBang("gen_bang");

        // Driving synth loops (E minor). Menu: mid-tempo groove; gameplay: faster,
        // higher-energy — the run should feel like a chase, not an aquarium.
        if (menuMusic == null)
            menuMusic = ProceduralAudio.DrivingLoop("gen_menu_drive", 112f,
                new[] { 41.2f, 41.2f, 49f, 55f },              // E1 E1 G1 A1
                new[] { 329.63f, 392f, 440f, 493.88f });       // E4 G4 A4 B4
        if (gameplayMusic == null)
            gameplayMusic = ProceduralAudio.DrivingLoop("gen_game_drive", 134f,
                new[] { 41.2f, 49f, 41.2f, 61.74f },           // E1 G1 E1 B1
                new[] { 659.26f, 587.33f, 659.26f, 783.99f },  // E5 D5 E5 G5
                0.32f);
    }
}
