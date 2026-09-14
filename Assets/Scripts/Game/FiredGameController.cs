using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// FIRED. run orchestrator — systems only; levels are hand-authored around
/// LevelInfo / FiredTarget / EnemyXRay / LevelEnd (Docs/LEVEL_DESIGN_GUIDE.md).
///
/// Flow: intro holds on the shooter (animation FROZEN on its aim pose) with a
/// "CLICK TO FIRE" prompt → on click the animation plays and, fireDelaySeconds
/// later, the shot lands: BAM + bang + bullet leaves in slow-mo → chase cam +
/// cursor lock → flight with Deadeye (hold RMB: slow time + crosshair, drains a
/// meter refilled by targets) → death rewinds the whole flight back to the gun
/// → results. Crossing a LevelEnd trigger = WIN.
/// </summary>
public class FiredGameController : MonoBehaviour
{
    [Header("HUD — scene objects under Canvas (restyle them in the Inspector)")]
    [SerializeField] private TMP_Text distanceText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private Image speedFill;
    [SerializeField] private RectTransform hudCanvas;

    [Header("HUD — flight extras (baked by Tools > FIRED > Bake Runtime UI)")]
    [SerializeField] private TMP_Text firePrompt;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private GameObject deadeyeBar;
    [SerializeField] private Image deadeyeFill;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private RectTransform letterboxTop;
    [SerializeField] private RectTransform letterboxBottom;
    [SerializeField] private ComicPopup popupPrefab;
    [Tooltip("Optional. Drives the speed bar's colour states, danger wash and leading cap. Falls back to a plain fillAmount if unassigned.")]
    [SerializeField] private SpeedGauge speedGauge;
    [Tooltip("Shows the player's record: BEST 742 m. The reason to press Retry.")]
    [SerializeField] private TMP_Text bestText;

    [Header("Deadeye")]
    [SerializeField] private float deadeyeScale = 0.3f;
    [SerializeField] private float deadeyeDrainPerSec = 0.4f;

    private LevelInfo _level;
    private BulletController _bullet;
    private ChaseCamera _chase;
    private Camera _cam;
    private Material _brassMat, _tipMat;

    // Everything that should vanish when a menu owns the screen (pause/game over).
    private readonly List<GameObject> _hudGroup = new();

    private int _score;
    private float _displayScore;   // eased toward _score for the fast count-up
    private int _multiplier = 1;
    private float _multiTimer;

    // Combo: consecutive scoring hits without a hard wall slam. Each hit inside
    // the window banks bonus points; a head-on ricochet or the timer breaks it.
    private int _combo;
    private float _comboTimer;
    private const float ComboWindow = 4f;
    private float _comboPunch;
    private bool _fired;
    private bool _ended;
    private bool _slowMoActive;
    private bool _deadeyeActive;
    private float _letterAmount;
    private bool _snapInFlight;
    private float _deadeyeMeter = 1f;
    private readonly List<Material> _runtimeFxMats = new();
    private Material _sharedFxMat;

    // ---- Records (day-1 hook) ----------------------------------------------
    // Snapshotted at run start: the bar to beat stays fixed for the whole run even
    // as SaveSystem updates, so "NEW BEST" fires exactly once at the right metre.
    private float _pbAtRunStart;
    private bool _beatPbThisRun;
    private bool _runBanked;
    private ModuleStreamer _streamer;

    /// <summary>The opening shooter cinematic is a first-impression beat, not a
    /// per-run one. Once it has played, retries drop straight into flight — that is
    /// most of the restart cost. Static so it survives the scene reload.</summary>
    private static bool _introPlayedThisSession;

    private void Awake()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Theme.FromHex("#241246");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Theme.Background;
        RenderSettings.fogStartDistance = 120f;
        RenderSettings.fogEndDistance = 380f;

        _brassMat = Lit(Theme.FromHex("#C9A24A"), 0.6f);
        _tipMat = Neon(Theme.NeonPink);
    }

    private void Start()
    {
        _cam = Camera.main;
        _level = FindFirstObjectByType<LevelInfo>();

        if (_level == null || _level.muzzle == null)
        {
            Debug.LogWarning("[FIRED] No LevelInfo with a muzzle — fallback spawn. See Docs/LEVEL_DESIGN_GUIDE.md.");
            BuildFallbackFloor();
        }

        if (FindFirstObjectByType<Light>() == null)
        {
            var sun = new GameObject("Sun (fallback)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Theme.FromHex("#9AA6FF");
            sun.intensity = 0.9f;
            sun.transform.rotation = Quaternion.Euler(50f, -28f, 0f);
        }

        var bulletGo = new GameObject("Bullet");
        var mesh = BuildBulletMesh(bulletGo.transform);
        _bullet = bulletGo.AddComponent<BulletController>();
        _bullet.Init(this, mesh.transform);
        bulletGo.SetActive(false);

        if (_cam != null)
        {
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 900f;
            _chase = _cam.gameObject.AddComponent<ChaseCamera>();
            _chase.target = bulletGo.transform;
            _chase.bullet = _bullet;
            _chase.enabled = false;
        }

        GameEvents.ClearBestSnap(); // fresh run, fresh highlight reel
        _streamer = FindFirstObjectByType<ModuleStreamer>();
        _pbAtRunStart = SaveSystem.BestDistance;
        _beatPbThisRun = false;
        _runBanked = false;
        BindHud();
        RefreshBestText();
        StartCoroutine(IntroWaitForClick());
    }

    // ---- Opening shot: hold on the shooter until the player pulls the trigger ----

    private IEnumerator IntroWaitForClick()
    {
        Transform muzzle = _level != null ? _level.muzzle : null;
        var anim = _level != null ? _level.shooterAnimator : null;

        // Over-the-shoulder: camera behind and beside the shooter, FACING down
        // the corridor — the character reads in the foreground and the level
        // they're about to fire into fills the frame.
        Transform shooterT = anim != null ? anim.transform : muzzle;
        if (shooterT != null && _cam != null)
        {
            Vector3 chest = shooterT.position + Vector3.up * 1.25f;
            Vector3 pos = chest - shooterT.forward * 3.1f + shooterT.right * 1.5f + Vector3.up * 0.5f;
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation(
                (chest + shooterT.forward * 9f) - pos, Vector3.up);
        }

        // Freeze the shooter on its aim pose (frame 0 of its default state).
        if (anim != null)
        {
            anim.enabled = true;
            anim.Play(0, 0, 0f);
            anim.speed = 0f;
        }

        // RETRY PATH: the intro has already been seen this session, so skip the
        // hold-and-click entirely and fire immediately. This is the single biggest
        // chunk of the death-to-flying-again cost (a second click plus a 0.35s
        // debounce plus reading the prompt), and a runner's retry should be one input.
        if (_introPlayedThisSession)
        {
            if (firePrompt != null) firePrompt.gameObject.SetActive(false);
            if (anim != null) anim.speed = 1f;
            yield return new WaitForSeconds(_level != null ? _level.fireDelaySeconds : 0.35f);
            yield return FireSequence(muzzle);
            yield break;
        }

        if (firePrompt != null) firePrompt.gameObject.SetActive(true);

        // Wait for the trigger pull (debounced so a menu click can't bleed through).
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool pad = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (t > 0.35f && (click || pad)) break;
            yield return null;
        }

        _introPlayedThisSession = true;
        if (firePrompt != null) firePrompt.gameObject.SetActive(false);

        // NOW the shoot animation plays, and the shot lands fireDelaySeconds into it.
        if (anim != null) anim.speed = 1f;
        float delay = _level != null ? _level.fireDelaySeconds : 0.35f;
        yield return new WaitForSeconds(delay);

        yield return FireSequence(muzzle);
    }

    private IEnumerator FireSequence(Transform muzzle)
    {
        if (_fired) yield break;
        _fired = true;

        Vector3 pos = muzzle != null ? muzzle.position : new Vector3(0f, 2f, 0f);
        Quaternion rot = muzzle != null ? muzzle.rotation : Quaternion.identity;
        float speed = _level != null ? _level.bulletStartSpeed : 50f;

        SpawnBam(pos + (rot * Vector3.forward) * 0.5f, 1.6f);
        AudioManager.Instance?.PlayUIBang();

        _bullet.gameObject.SetActive(true);
        _bullet.Launch(pos + (rot * Vector3.forward) * 0.6f, rot, speed);

        // Dramatic beat: the camera GLIDES from the intro pose onto the bullet's
        // tail during the slow-mo, instead of hard-cutting there afterwards (the
        // old cut produced a one-frame flash of whatever was in front of the gun).
        // _slowMoActive MUST be set: Update() re-asserts timeScale every frame and
        // would otherwise stomp this back to 1 on the very next frame, silently
        // throwing away the whole beat.
        _slowMoActive = true;
        Time.timeScale = 0.22f;
        Vector3 fromPos = _cam != null ? _cam.transform.position : Vector3.zero;
        Quaternion fromRot = _cam != null ? _cam.transform.rotation : Quaternion.identity;
        float t = 0f;
        const float beat = 0.55f;
        while (t < beat)
        {
            t += Time.unscaledDeltaTime;
            if (_cam != null && _chase != null && _bullet != null)
            {
                float k = Mathf.Clamp01(t / beat);
                k = k * k * (3f - 2f * k); // ease both ends
                Vector3 chasePos = _bullet.transform.position
                    - _bullet.transform.forward * _chase.distance + Vector3.up * _chase.height;
                Quaternion chaseRot = Quaternion.LookRotation(
                    (_bullet.transform.position + _bullet.transform.forward * _chase.lookAhead) - chasePos, Vector3.up);
                _cam.transform.position = Vector3.Lerp(fromPos, chasePos, k);
                _cam.transform.rotation = Quaternion.Slerp(fromRot, chaseRot, k);
            }
            yield return null;
        }
        _slowMoActive = false;
        if (Time.timeScale != 0f) Time.timeScale = 1f;

        if (_chase != null)
        {
            _chase.BeginFollow();
            _chase.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SpawnBam(Vector3 worldPos, float scale)
    {
        if (_level == null || _level.bamVfxPrefab == null) return;
        var go = Instantiate(_level.bamVfxPrefab, worldPos, Quaternion.identity);
        go.transform.localScale = Vector3.one * scale;
        if (_cam != null)
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - _cam.transform.position);
        Destroy(go, 1.1f);
    }

    // ---- Per-frame: HUD, Deadeye, cursor -----------------------------------------

    private void Update()
    {
        if (_bullet == null) return;

        bool uiOwnsScreen = PauseMenu.IsPaused || GameOverScreen.IsShowing || _ended;

        // Cursor: locked only while actually flying.
        bool wantLock = _fired && !uiOwnsScreen && !_bullet.IsDead;
        if (wantLock && Cursor.lockState != CursorLockMode.Locked)
        { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        else if (!wantLock && Cursor.lockState == CursorLockMode.Locked)
        { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        // Deadeye: hold RMB (or LT) to slow time while the meter lasts.
        bool held = (Mouse.current != null && Mouse.current.rightButton.isPressed)
                 || (Gamepad.current != null && Gamepad.current.leftTrigger.isPressed);
        bool canDeadeye = _fired && !uiOwnsScreen && !_bullet.IsDead && _deadeyeMeter > 0.02f;
        _deadeyeActive = held && canDeadeye;

        if (!uiOwnsScreen && !_slowMoActive)
            Time.timeScale = _deadeyeActive ? deadeyeScale : 1f;
        if (_deadeyeActive)
            _deadeyeMeter = Mathf.Max(0f, _deadeyeMeter - deadeyeDrainPerSec * Time.unscaledDeltaTime);

        if (crosshair != null && crosshair.activeSelf != _deadeyeActive)
            crosshair.SetActive(_deadeyeActive);
        if (_chase != null)
            _chase.baseFov = _deadeyeActive ? 52f : 70f; // scope-in zoom

        if (_multiTimer > 0f)
        {
            _multiTimer -= Time.deltaTime;
            if (_multiTimer <= 0f) _multiplier = 1;
        }

        // Combo readout: visible from 2 hits, punches on every extension, fades
        // urgency as the window runs out.
        if (_comboTimer > 0f)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f) _combo = 0;
        }
        if (comboText != null)
        {
            bool show = _combo >= 2 && _fired && !uiOwnsScreen;
            if (comboText.gameObject.activeSelf != show) comboText.gameObject.SetActive(show);
            if (show)
            {
                _comboPunch = Mathf.MoveTowards(_comboPunch, 0f, Time.unscaledDeltaTime * 3.5f);
                comboText.text = $"COMBO x{_combo}";
                comboText.transform.localScale = Vector3.one * (1f + 0.35f * _comboPunch);
                float urgency = Mathf.Clamp01(_comboTimer / ComboWindow);
                comboText.color = Color.Lerp(Theme.Negative, Theme.NeonYellow, urgency);
            }
        }

        // Score counter races up to the real value — the gap closes at 8x/sec
        // plus a floor rate, so big awards visibly "spin" and small ones snap.
        if (_displayScore < _score)
            _displayScore = Mathf.Min(_score,
                _displayScore + Mathf.Max(60f, (_score - _displayScore) * 8f) * Time.unscaledDeltaTime);

        // Crossing your record MID-RUN is the moment worth having: you are still
        // flying and can still extend it. Firing this only on the results screen
        // would land after the emotion has already passed.
        if (!_beatPbThisRun && _fired && !_bullet.IsDead && _pbAtRunStart > 1f
            && _bullet.Distance > _pbAtRunStart)
        {
            _beatPbThisRun = true;
            OnNewRecordInFlight();
        }

        if (distanceText != null) distanceText.text = $"{Mathf.FloorToInt(_bullet.Distance)} m";
        if (multiplierText != null) multiplierText.text = Mathf.FloorToInt(_displayScore).ToString();
        float speed01 = Mathf.Clamp01(Mathf.InverseLerp(_bullet.StallSpeed, _bullet.SoftCap, _bullet.Speed));
        // Stalling into the fall IS the death beat as far as the player is concerned,
        // so the tank reads empty from that moment — not once the corpse settles.
        bool stillFlying = !_bullet.IsDead && !_bullet.IsFalling;
        if (speedGauge != null) speedGauge.SetNormalized(speed01, stillFlying);
        else if (speedFill != null) speedFill.fillAmount = stillFlying ? speed01 : 0f;
        if (deadeyeFill != null) deadeyeFill.fillAmount = _deadeyeMeter;

        // Letterbox: ease in while a cinematic slow-mo beat owns the screen.
        bool cinematic = _slowMoActive && !uiOwnsScreen;
        _letterAmount = Mathf.MoveTowards(_letterAmount, cinematic ? 1f : 0f,
            Time.unscaledDeltaTime * (cinematic ? 6f : 3f));
        if (letterboxTop != null || letterboxBottom != null)
        {
            // Size in CANVAS units, not pixels: the canvas scales to a 1920x1080
            // reference, so Screen.height made the bars balloon on high-DPI displays.
            float canvasH = hudCanvas != null ? hudCanvas.rect.height : Screen.height;
            float h = canvasH * 0.085f * _letterAmount;
            if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0f, h);
            if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0f, h);
        }

        // Flight HUD only exists while flying — menus own the screen otherwise.
        bool showHud = _fired && !uiOwnsScreen;
        foreach (var go in _hudGroup)
            if (go != null && go.activeSelf != showHud) go.SetActive(showHud);
        if (uiOwnsScreen && firePrompt != null && firePrompt.gameObject.activeSelf)
            firePrompt.gameObject.SetActive(false);
    }

    /// <summary>Line break for the two-line results summary (TMP renders rich text).</summary>
    private static readonly string Break = "\n";

    /// <summary>NEW BEST, fired the instant the player passes their record.</summary>
    private void OnNewRecordInFlight()
    {
        SpawnPopup(_bullet.transform.position + _bullet.transform.forward * 8f,
                   "NEW BEST!", -1, Theme.NeonYellow);
        AudioManager.Instance?.PlayUIConfirm();
        if (bestText != null) bestText.color = Theme.NeonYellow;
    }

    /// <summary>Paints the record readout. Hidden entirely on a first-ever run so a
    /// new player is not shown an empty "BEST 0 m".</summary>
    private void RefreshBestText()
    {
        if (bestText == null) return;
        bool has = SaveSystem.BestDistance > 1f;
        bestText.gameObject.SetActive(has);
        if (!has) return;
        bestText.text = $"BEST {Mathf.FloorToInt(SaveSystem.BestDistance)} m";
        bestText.color = Theme.TextDim;
    }

    /// <summary>Bank the run exactly once, and build the results line around the gap
    /// to the record — that line is what puts the thumb back on Retry.</summary>
    private string BankRunAndBuildSummary(float distance, string cause)
    {
        if (_runBanked) return GameEvents.LastRunSummary;
        _runBanked = true;

        int zoneIdx = 0;
        string zoneName = "";
        if (_streamer != null && _streamer.zones != null && _streamer.CurrentZone != null)
        {
            zoneName = _streamer.CurrentZone.name;
            for (int i = 0; i < _streamer.zones.Length; i++)
                if (_streamer.zones[i] == _streamer.CurrentZone) { zoneIdx = i; break; }
        }

        float prevBest = SaveSystem.BestDistance;
        var rec = SaveSystem.RecordRun(_score, distance, zoneIdx, zoneName);
        GameEvents.LastRunRecord = rec;
        RefreshBestText();

        int m = Mathf.FloorToInt(distance);
        string head = $"{m} m   ·   {_score} pts";
        if (!string.IsNullOrEmpty(zoneName)) head += $"   ·   {zoneName}";

        if (rec.NewBestDistance)
        {
            int gain = Mathf.Max(0, m - Mathf.FloorToInt(prevBest));
            return prevBest > 1f
                ? head + Break + "<color=#FFE600>NEW BEST  +" + gain + " m</color>"
                : head + Break + "<color=#FFE600>NEW BEST</color>";
        }

        int shortBy = Mathf.Max(1, Mathf.FloorToInt(prevBest) - m);
        return head + Break + "<color=#8B7FB8>" + shortBy + " m short of your best ("
             + Mathf.FloorToInt(prevBest) + " m)</color>";
    }

    // ---- Callbacks -------------------------------------------------------------------

    public void OnScored(int points, Vector3 worldPos, string label, Color color, bool enemyHit = false)
    {
        _multiplier = Mathf.Min(_multiplier + 1, 9);
        _multiTimer = 6f;

        // Combo: every chained hit is worth +10% per combo step, so keeping a
        // streak alive matters more than any single target.
        _combo++;
        _comboTimer = ComboWindow;
        _comboPunch = 1f;
        int awarded = Mathf.RoundToInt(points * _multiplier * (1f + 0.1f * (_combo - 1)));
        _score += awarded;
        _deadeyeMeter = Mathf.Min(1f, _deadeyeMeter + 0.3f);
        // Speed IS health — hits should visibly push the bar up. Ring ≈ +13,
        // ribs ≈ +15.5, pelvis ≈ +18, skull ≈ +33 (pre-SpeedScale).
        _bullet?.Refund(8f + points * 0.5f);
        SpawnPopup(worldPos, label, awarded, color);
        if (points >= 30) SpawnBam(worldPos, 0.8f);
        // A REAL slow-mo beat — the hit should be felt, not subliminal.
        if (enemyHit)
        {
            AudioManager.Instance?.PlayEnemyImpact();
            _chase?.PlayEnemyImpactCinematic(worldPos, _bullet != null ? _bullet.transform.forward : Vector3.forward);
            if (!_deadeyeActive) StartCoroutine(SlowMo(0.12f, 0.7f));
            StartCoroutine(CaptureSnap(awarded, label, 0.3f));
        }
        else if (!_deadeyeActive)
        {
            AudioManager.Instance?.PlayScore();
            // Rings are frequent pickups — keep the beat subtle so flow survives.
            StartCoroutine(SlowMo(0.55f, 0.14f));
        }
    }

    private float _lastRicochetSfx;

    public void OnRicochet(Vector3 point, Vector3 normal, Vector3 reflection, bool hard)
    {
        // Sliding along a wall reports a hit every frame — throttle the clink.
        if (Time.unscaledTime - _lastRicochetSfx > 0.12f)
        {
            AudioManager.Instance?.PlayRicochet(hard);
            _lastRicochetSfx = Time.unscaledTime;
        }
        StartCoroutine(RicochetFx(point, normal, reflection, hard));
        // Only HARD (head-on) hits earn the cinematic + slow-mo. Grazes happen
        // constantly in corridors — interrupting flow every time made the whole
        // run feel stuttery and uncontrollable.
        if (hard)
        {
            _combo = 0; // head-on slam breaks the streak
            _comboTimer = 0f;
            _chase?.PlayRicochetCinematic(point, normal, reflection, true);
            if (!_deadeyeActive) StartCoroutine(SlowMo(0.2f, 0.35f));
        }
    }

    public void OnStall() => AudioManager.Instance?.PlayStall();

    private float _lastWrongWay;

    /// <summary>Flying against the flow (ModuleStreamer detects it): red warning
    /// burst; the streamer bleeds speed at the same time — turn around or stall.</summary>
    public void OnWrongWay(Vector3 worldPos)
    {
        if (Time.unscaledTime - _lastWrongWay < 0.9f) return;
        _lastWrongWay = Time.unscaledTime;
        SpawnPopup(worldPos, "WRONG WAY!", -1, Theme.Negative);
        AudioManager.Instance?.PlayUIBack();
    }

    /// <summary>Called by a LevelEnd trigger — the bullet made it. WIN.</summary>
    public void OnLevelComplete()
    {
        if (_ended || _bullet == null || _bullet.IsDead) return;
        _ended = true;
        GameEvents.LastRunSummary = BankRunAndBuildSummary(_bullet.Distance, "TARGET ELIMINATED");
        GameEvents.RaiseGameOver(true);
    }

    public void OnBulletDied(string cause, float distance)
    {
        if (_ended) return;
        _ended = true;
        GameEvents.LastRunSummary = BankRunAndBuildSummary(distance, cause);
        StartCoroutine(BeatThenGameOver());
    }

    /// <summary>Short breath after the bullet dies, then straight to results.</summary>
    private IEnumerator BeatThenGameOver()
    {
        if (_chase != null) _chase.enabled = false;
        float t = 0f;
        while (t < 0.7f) { t += Time.unscaledDeltaTime; yield return null; }
        Time.timeScale = 1f; // GameOverScreen re-freezes
        GameEvents.RaiseGameOver(false);
    }

    // ---- Runtime HUD extras --------------------------------------------------------

    /// <summary>The HUD is authored in the scene, not built here. This only hides
    /// the pieces that start off-screen and registers what should disappear while a
    /// menu owns the screen. Missing references degrade gracefully — every consumer
    /// below is null-checked — but run Tools > FIRED > Bake Runtime UI to restore them.</summary>
    private void BindHud()
    {
        if (firePrompt != null) firePrompt.gameObject.SetActive(false);
        if (crosshair != null) crosshair.SetActive(false);
        if (comboText != null) comboText.gameObject.SetActive(false);
        if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0f, 0f);
        if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0f, 0f);

        if (deadeyeBar != null) _hudGroup.Add(deadeyeBar);
        if (distanceText != null) _hudGroup.Add(distanceText.gameObject);
        if (multiplierText != null) _hudGroup.Add(multiplierText.gameObject);
        if (speedFill != null && speedFill.transform.parent != null)
            _hudGroup.Add(speedFill.transform.parent.gameObject);
        if (hudCanvas != null)
        {
            var speedLabel = hudCanvas.Find("SpeedLabel");
            if (speedLabel != null) _hudGroup.Add(speedLabel.gameObject);
        }
    }

    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        foreach (var mat in _runtimeFxMats)
            if (mat != null) Destroy(mat);
    }

    // ---- Helpers ----------------------------------------------------------------------

    /// <summary>Photograph the cinematic at its most dramatic frame and keep it
    /// (in memory only) if it beats the run's best-scoring moment so far.</summary>
    private IEnumerator CaptureSnap(int awarded, string label, float delayRealtime)
    {
        if (_snapInFlight || awarded <= GameEvents.BestSnapScore) yield break;
        _snapInFlight = true;

        float t = 0f;
        while (t < delayRealtime) { t += Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForEndOfFrame(); // full frame incl. UI-free world

        // Don't waste the snap on a menu that popped mid-cinematic.
        if (!PauseMenu.IsPaused && !GameOverScreen.IsShowing && awarded > GameEvents.BestSnapScore)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            GameEvents.ClearBestSnap();
            GameEvents.BestSnapTexture = tex;
            GameEvents.BestSnapScore = awarded;
            GameEvents.BestSnapLabel = label;
        }
        _snapInFlight = false;
    }

    private IEnumerator SlowMo(float scale, float durationRealtime)
    {
        if (Time.timeScale == 0f) yield break;
        _slowMoActive = true;
        Time.timeScale = scale;
        float t = 0f;
        while (t < durationRealtime)
        {
            t += Time.unscaledDeltaTime;
            if (Time.timeScale == 0f) { _slowMoActive = false; yield break; }
            yield return null;
        }
        if (Time.timeScale != 0f) Time.timeScale = 1f;
        _slowMoActive = false;
    }

    private IEnumerator RicochetFx(Vector3 point, Vector3 normal, Vector3 reflection, bool hard)
    {
        var root = new GameObject(hard ? "HardRicochetFX" : "RicochetFX");
        root.transform.position = point;

        var line = new GameObject("GlowLine").AddComponent<LineRenderer>();
        line.transform.SetParent(root.transform, false);
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 6;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = CreateFxMaterial();
        line.startColor = Theme.NeonYellow;
        line.endColor = Theme.NeonPink;
        line.widthMultiplier = hard ? 0.24f : 0.16f;

        float lineLength = hard ? 5.2f : 3.6f;
        line.SetPosition(0, point - reflection * 0.6f + normal * 0.04f);
        line.SetPosition(1, point + reflection * lineLength + normal * 0.04f);

        var flash = new GameObject("Flash").AddComponent<Light>();
        flash.transform.SetParent(root.transform, false);
        flash.transform.position = point + normal * 0.15f;
        flash.type = LightType.Point;
        flash.color = Theme.NeonYellow;
        flash.intensity = hard ? 18f : 12f;
        flash.range = hard ? 7f : 4.5f;

        Vector3 tangent = Vector3.Cross(normal, reflection).normalized;
        if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.up).normalized;

        for (int i = 0; i < (hard ? 12 : 8); i++)
        {
            float spread = Random.Range(-1f, 1f);
            Vector3 dir = (reflection + tangent * spread + normal * Random.Range(0.2f, 0.8f)).normalized;
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(spark.GetComponent<Collider>());
            spark.name = "Spark";
            spark.transform.SetParent(root.transform, false);
            spark.transform.position = point + normal * 0.05f;
            spark.transform.localScale = Vector3.one * Random.Range(0.05f, 0.11f);
            spark.GetComponent<Renderer>().sharedMaterial = CreateFxMaterial();
            StartCoroutine(AnimateSpark(spark.transform, dir, Random.Range(2.8f, 6.4f), hard ? 0.38f : 0.25f));
        }

        float t = 0f;
        float duration = hard ? 0.45f : 0.28f;
        Color startA = line.startColor;
        Color endA = line.endColor;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);
            line.widthMultiplier = Mathf.Lerp(0f, hard ? 0.24f : 0.16f, k);
            line.startColor = new Color(startA.r, startA.g, startA.b, k);
            line.endColor = new Color(endA.r, endA.g, endA.b, k * 0.8f);
            flash.intensity = Mathf.Lerp(0f, hard ? 18f : 12f, k);
            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator AnimateSpark(Transform spark, Vector3 direction, float speed, float duration)
    {
        Vector3 start = spark.position;
        Vector3 startScale = spark.localScale;
        float t = 0f;
        while (t < duration && spark != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            spark.position = start + direction * speed * t + Physics.gravity * (0.18f * t * t);
            spark.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
            yield return null;
        }

        if (spark != null) Destroy(spark.gameObject);
    }

    /// <summary>One shared emissive material for every ricochet line and spark.
    /// This used to allocate a fresh Material per spark — 13 per hard ricochet,
    /// none of them freed until the scene unloaded, which bled memory badly on
    /// WebGL over a long run. Nothing tints them per-hit, so one instance does.</summary>
    private Material CreateFxMaterial()
    {
        if (_sharedFxMat == null)
        {
            _sharedFxMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _sharedFxMat.SetColor("_BaseColor", Color.white);
            _sharedFxMat.EnableKeyword("_EMISSION");
            _sharedFxMat.SetColor("_EmissionColor", Theme.NeonYellow * 4.5f);
            _runtimeFxMats.Add(_sharedFxMat);
        }
        return _sharedFxMat;
    }

    private void SpawnPopup(Vector3 worldPos, string label, int value, Color color)
    {
        if (popupPrefab == null || hudCanvas == null || _cam == null) return;
        Vector3 sp = _cam.WorldToScreenPoint(worldPos);
        if (sp.z < 0f) return;

        var popup = Instantiate(popupPrefab, hudCanvas);
        ((RectTransform)popup.transform).position = sp;
        popup.Play(label, value, color);
    }

    private void BuildFallbackFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "FallbackFloor";
        floor.transform.position = new Vector3(0f, -3f, 100f);
        floor.transform.localScale = new Vector3(20f, 1f, 60f);
        floor.GetComponent<Renderer>().sharedMaterial = Lit(Theme.FromHex("#160B34"), 0.1f);
    }

    private static Material Lit(Color baseColor, float smoothness)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    private static Material Neon(Color c)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * 3.2f);
        return m;
    }

    private GameObject BuildBulletMesh(Transform parent)
    {
        var mesh = new GameObject("Mesh");
        mesh.transform.SetParent(parent, false);

        // Designer-supplied bullet model (e.g. Kenney foamBulletA) wins.
        var level = FindFirstObjectByType<LevelInfo>();
        if (level != null && level.bulletModel != null)
        {
            var vis = Instantiate(level.bulletModel, mesh.transform, false);
            foreach (var c in vis.GetComponentsInChildren<Collider>()) Destroy(c);

            // Normalize: longest side ≈ 0.6 m, long axis along +Z.
            var rends = vis.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                if (b.size.x >= b.size.y && b.size.x >= b.size.z)
                    vis.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                else if (b.size.y >= b.size.x && b.size.y >= b.size.z)
                    vis.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (longest > 0.0001f) vis.transform.localScale = Vector3.one * (0.6f / longest);
            }
            return mesh;
        }

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(mesh.transform, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.7f, 0.5f);
        body.GetComponent<Renderer>().sharedMaterial = _brassMat;

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(mesh.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.62f);
        tip.transform.localScale = Vector3.one * 0.52f;
        tip.GetComponent<Renderer>().sharedMaterial = _tipMat;

        return mesh;
    }
}
