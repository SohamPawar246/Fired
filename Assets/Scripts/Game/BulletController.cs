using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE PLAYER — you are the bullet. Flies forward constantly; mouse steers; speed
/// is your health. See Docs/FIRED_GDD.md §3.
///
///  - constant forward flight along transform.forward
///  - mouse / right-stick steering, turn rate capped and scaled by speed
///  - speed decays over time, refunds from targets, stalls at the floor value
///  - forward spherecast handles solids: shallow angle = ricochet, head-on = death
///  - capsule overlap handles pass-through targets (no tunnelling at high speed)
///  - stall → ballistic fall → death
///
/// Movement is done in code (no Rigidbody) for tight, predictable feel. Targets
/// are trigger colliders; solids are non-trigger — that's how the two casts stay
/// separated without custom layers.
/// </summary>
public class BulletController : MonoBehaviour
{
    [Header("Speed economy (m/s)")]
    [Tooltip("Global multiplier on the whole speed economy. 1 = the classic tuning.")]
    public float SpeedScale = 0.62f;
    public float StartSpeed = 22f;   // readable, steerable speed
    public float SoftCap = 46f;
    public float StallSpeed = 8f;
    public float Decay = 1.55f;

    [Header("Steering")]
    [Tooltip("Safety ceiling on turn rate (deg/s). Keep well above what normal mouse motion produces so it never bottlenecks steering.")]
    public float MaxTurnRate = 340f;
    public float MouseSensitivity = 3.2f;
    public float StickSensitivity = 145f;
    [Tooltip("Input low-pass: higher = snappier but rawer, lower = smoother but laggier. ~10-14 kills jitter without noticeable lag.")]
    public float SteerSmoothing = 12f;
    [Tooltip("Pitch is clamped to ±this so the bullet can never loop vertically or dive straight into the floor.")]
    public float MaxPitch = 72f;

    // Explicit yaw/pitch state — rotation is ALWAYS Euler(pitch, yaw, 0), so the
    // horizon stays level. (Chaining transform.Rotate(pitch, yaw) in local space
    // accumulates roll drift, which is why steering used to feel increasingly
    // skewed and "uncontrollable" the longer a run went.)
    private float _yaw, _pitch;
    private Vector2 _smoothedLook;

    [Header("Bounds")]
    [Tooltip("If the bullet ever ends up below this world Y it has left the map — end the run instead of falling forever.")]
    public float VoidKillY = -25f;

    [Header("Collision")]
    public float Radius = 0.35f;
    [Tooltip("Angle from the surface normal. Above this = glancing ricochet; below = head-on death.")]
    public float GrazeAngleFromNormal = 55f;
    public float RicochetSpeedMul = 0.93f;
    public float HardRicochetSpeedMul = 0.84f;
    [Tooltip("After a ricochet, clamp the bullet to at least this multiple of StallSpeed so walls redirect the run instead of instantly ending it.")]
    public float PostRicochetMinSpeedMul = 1.05f;
    [Tooltip("Seconds of immunity after a ricochet before another one can drain speed again — stops back-to-back grazes in tight corridors from compounding into an instant stall.")]
    public float RicochetGraceTime = 0.25f;
    private float _ricochetGraceTimer;

    [Header("Launch feel")]
    [Tooltip("Seconds after launch during which speed eases from a calm value up to the fired speed, ricochets cost nothing, and steering is forgiving — the opening moment should feel guided, not like being shot out of a cannon.")]
    public float LaunchRampTime = 1.6f;
    [Tooltip("Fraction of the fired speed the bullet actually starts at; it ramps up to full over LaunchRampTime.")]
    public float LaunchRampStart = 0.55f;
    private float _launchTimer;
    private float _launchTargetSpeed;

    public float Speed { get; private set; }
    public float Distance { get; private set; }
    public bool IsDead { get; private set; }
    public bool Launched { get; private set; }
    public bool IsFalling => _falling;

    private FiredGameController _game;
    private Transform _mesh;
    private bool _falling;
    private Vector3 _fallVel;
    private float _fallTimer;
    private float _fallDelay;
    private Vector3 _fallSpin;
    private Rigidbody _fallRb;
    private SphereCollider _fallCollider;
    private float _fallRestTimer;

    // Small reusable buffer for target overlap (avoids per-frame allocation).
    private readonly Collider[] _overlap = new Collider[16];

    /// <summary>Flight path breadcrumbs (~every 2 m) for the death rewind.</summary>
    public readonly System.Collections.Generic.List<Vector3> Path = new();
    private Vector3 _lastCrumb;

    public void Init(FiredGameController game, Transform mesh)
    {
        _game = game;
        _mesh = mesh;

        _fallCollider = GetComponent<SphereCollider>();
        if (_fallCollider == null) _fallCollider = gameObject.AddComponent<SphereCollider>();
        _fallCollider.radius = Radius;
        _fallCollider.isTrigger = false;
        _fallCollider.enabled = false;

        _fallRb = GetComponent<Rigidbody>();
        if (_fallRb == null) _fallRb = gameObject.AddComponent<Rigidbody>();
        _fallRb.isKinematic = true;
        _fallRb.useGravity = false;
        // CRITICAL: no interpolation while the bullet flies kinematically — an
        // interpolating rigidbody rewrites the transform every frame from its
        // own physics pose, silently overriding (and jittering) the manual
        // flight movement AND the mouse steering. Interpolation is enabled only
        // when the fall physics takes over (BeginFall).
        _fallRb.interpolation = RigidbodyInterpolation.None;
        _fallRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _fallRb.maxAngularVelocity = 80f;

        // Apply SpeedScale to all speed economy parameters
        StartSpeed *= SpeedScale;
        SoftCap *= SpeedScale;
        StallSpeed *= SpeedScale;
        Decay *= SpeedScale;

        Speed = StartSpeed;
    }

    /// <summary>External speed drain (e.g. flying the WRONG WAY bleeds you out).</summary>
    public void Bleed(float amount) => Speed = Mathf.Max(0f, Speed - amount * SpeedScale);

    /// <summary>Small positive refund on score so the run can sustain itself.</summary>
    public void Refund(float amount) => Speed = Mathf.Min(SoftCap, Speed + amount * SpeedScale);

    /// <summary>Fired from the gun: place at the muzzle and go. Until this is
    /// called the bullet is inert (the intro sequence owns the screen).</summary>
    public void Launch(Vector3 position, Quaternion rotation, float startSpeed)
    {
        transform.SetPositionAndRotation(position, rotation);
        SyncYawPitchFromForward();
        _smoothedLook = Vector2.zero;

        // Ease into the fired speed instead of starting at it — the first two
        // seconds are where new players lose control and eat accidental walls.
        _launchTargetSpeed = Mathf.Min(startSpeed * SpeedScale, SoftCap);
        Speed = _launchTargetSpeed * LaunchRampStart;
        _launchTimer = LaunchRampTime;
        _ricochetGraceTimer = LaunchRampTime; // opening bumps are free

        Launched = true;
        Path.Clear();
        Path.Add(position);
        _lastCrumb = position;
    }

    /// <summary>Re-derive the explicit yaw/pitch state from wherever the
    /// transform currently points (launch, ricochet reflection).</summary>
    private void SyncYawPitchFromForward()
    {
        Vector3 f = transform.forward;
        _yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        _pitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
        _pitch = Mathf.Clamp(_pitch, -MaxPitch, MaxPitch);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void Update()
    {
        if (!Launched || IsDead) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused

        // Left the map somehow (fell through a seam, flew over a wall) — end the
        // run cleanly instead of falling forever with no feedback.
        if (transform.position.y < VoidKillY) { Die("THE VOID"); return; }

        if (_falling) { UpdateFall(dt); return; }

        if (_mesh != null) _mesh.Rotate(0f, 0f, 720f * dt, Space.Self); // rifling spin

        if (_ricochetGraceTimer > 0f) _ricochetGraceTimer -= dt;

        // Launch ramp: glide up to the fired speed, and don't decay while ramping.
        if (_launchTimer > 0f)
        {
            _launchTimer -= dt;
            float k = 1f - Mathf.Clamp01(_launchTimer / LaunchRampTime);
            Speed = Mathf.Max(Speed, _launchTargetSpeed * Mathf.Lerp(LaunchRampStart, 1f, k * k));
        }

        HandleSteer(dt);
        MoveAndCollide(dt);

        if (_launchTimer <= 0f) Speed = Mathf.Max(0f, Speed - Decay * dt);
        if (Speed <= StallSpeed) BeginFall();
    }

    private void HandleSteer(float dt)
    {
        // Raw input in degrees. mouse.delta is per-frame pixels, so the mouse
        // term needs no dt; the stick term does.
        Vector2 rawLook = Vector2.zero;

        var mouse = Mouse.current;
        if (mouse != null)
            rawLook += mouse.delta.ReadValue() * (MouseSensitivity * SaveSystem.MouseSensitivity * 0.02f);

        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 s = pad.rightStick.ReadValue();
            rawLook += s * (StickSensitivity * dt);
        }

        // Low-pass filter: kills frame-to-frame mouse jitter without adding
        // noticeable lag. NEVER clamp the raw delta against a per-frame cap —
        // that flattens every real mouse move to the same tiny step (the old
        // "have to force the mouse" bug).
        _smoothedLook = Vector2.Lerp(_smoothedLook, rawLook, 1f - Mathf.Exp(-SteerSmoothing * dt));

        // Fast = slightly heavier steering; forgiving while the launch ramp runs.
        float speedMul = Mathf.Lerp(1.1f, 0.85f, Mathf.InverseLerp(StallSpeed, SoftCap, Speed));
        if (_launchTimer > 0f) speedMul = Mathf.Max(speedMul, 1.1f);

        float maxStep = MaxTurnRate * dt; // generous safety ceiling only
        _yaw += Mathf.Clamp(_smoothedLook.x * speedMul, -maxStep, maxStep);
        _pitch = Mathf.Clamp(_pitch - Mathf.Clamp(_smoothedLook.y * speedMul, -maxStep, maxStep), -MaxPitch, MaxPitch);
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void MoveAndCollide(float dt)
    {
        Vector3 prev = transform.position;
        Vector3 dir = transform.forward;
        float step = Speed * dt;

        if (Physics.SphereCast(prev, Radius, dir, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
        {
            // EVERY wall hit ricochets — you are a bullet, not glass. The angle
            // decides the price: grazes are cheap, head-on slams bleed hard (and
            // trigger the cinematic). Death comes only from running out of speed.
            float angFromNormal = Vector3.Angle(-dir, hit.normal); // 0 = head-on, 90 = parallel graze
            Vector3 refl = Vector3.Reflect(dir, hit.normal).normalized;

            // Tame the reflection: never send the player steeply up/down off a
            // wall — a vertical exit is disorienting and usually ends in the
            // floor or ceiling before they can react.
            refl.y = Mathf.Clamp(refl.y, -0.35f, 0.45f);
            refl = refl.normalized;

            transform.rotation = Quaternion.LookRotation(refl, Vector3.up);
            transform.position = hit.point + hit.normal * (Radius + 0.02f);

            // The bounce changed our heading — resync steering state and drop
            // buffered mouse motion so the bullet flies straight out of the
            // ricochet until the player gives NEW input.
            SyncYawPitchFromForward();
            _smoothedLook = Vector2.zero;

            bool hard = angFromNormal < GrazeAngleFromNormal;
            if (_ricochetGraceTimer <= 0f)
            {
                float mul = hard ? HardRicochetSpeedMul : RicochetSpeedMul;
                Speed = Mathf.Max(StallSpeed * PostRicochetMinSpeedMul, Speed * mul);
                _ricochetGraceTimer = RicochetGraceTime;
            }
            _game.OnRicochet(hit.point, hit.normal, refl, hard);
        }
        else
        {
            transform.position = prev + dir * step;
        }

        Distance += (transform.position - prev).magnitude;
        if ((transform.position - _lastCrumb).sqrMagnitude > 4f)
        {
            Path.Add(transform.position);
            _lastCrumb = transform.position;
        }
        CheckTargets(prev, transform.position);
    }

    private void CheckTargets(Vector3 a, Vector3 b)
    {
        int n = Physics.OverlapCapsuleNonAlloc(a, b, Radius, _overlap, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var c = _overlap[i];
            if (c == null || !c.isTrigger) continue;

            // Finish line: the bullet is collider-less (spherecast movement), so
            // LevelEnd is detected here instead of via OnTriggerEnter.
            if (c.GetComponent<LevelEnd>() != null)
            {
                _game.OnLevelComplete();
                continue;
            }

            var t = c.GetComponent<FiredTarget>();
            if (t == null || !t.Available) continue;

            var r = t.Consume();
            _game.OnScored(r.points, c.transform.position, r.label, r.color, r.enemyHit);
        }
    }

    // ---- Stall & fall ------------------------------------------------------------

    private void BeginFall()
    {
        _falling = true;
        _fallTimer = 0f;
        _fallDelay = Mathf.Lerp(0.04f, 0.1f, Mathf.InverseLerp(StallSpeed, SoftCap, Speed));
        _fallVel = transform.forward * Mathf.Max(Speed * 0.52f, StallSpeed * 0.85f);
        _fallSpin = new Vector3(
            Random.Range(280f, 520f),
            Random.Range(-180f, 180f),
            Random.Range(360f, 700f));
        _fallRestTimer = 0f;

        if (_fallCollider != null) _fallCollider.enabled = true;
        if (_fallRb != null)
        {
            _fallRb.isKinematic = false;
            _fallRb.useGravity = true;
            _fallRb.interpolation = RigidbodyInterpolation.Interpolate;
            _fallRb.linearDamping = 1.2f;
            _fallRb.angularDamping = 4.5f;
            _fallRb.linearVelocity = _fallVel + Vector3.down * 0.65f;
            _fallRb.angularVelocity = (transform.right * _fallSpin.x + transform.up * _fallSpin.y + transform.forward * _fallSpin.z) * Mathf.Deg2Rad * 0.28f;
        }

        _game.OnStall();
    }

    private void UpdateFall(float dt)
    {
        _fallTimer += dt;

        if (_fallDelay > 0f)
        {
            _fallDelay = Mathf.Max(0f, _fallDelay - dt);
            _fallVel = Vector3.Lerp(_fallVel, transform.forward * Mathf.Max(Speed * 0.28f, StallSpeed * 0.45f), 1f - Mathf.Exp(-2.5f * dt));
            _fallVel += Physics.gravity * (0.7f * dt);
            if (_fallRb != null) _fallRb.linearVelocity = _fallVel + Vector3.down * 0.25f;
            else transform.position += _fallVel * dt;
            if (_fallDelay > 0f) return;
        }

        if (_fallRb != null)
        {
            float linear = _fallRb.linearVelocity.magnitude;
            float angular = _fallRb.angularVelocity.magnitude;
            if (linear < 0.2f && angular < 0.5f)
                _fallRestTimer += dt;
            else
                _fallRestTimer = 0f;

            if (_fallRestTimer > 0.55f || _fallTimer > 6.5f || _fallRb.IsSleeping())
            {
                Die("GRAVITY");
                return;
            }
            return;
        }

        _fallVel += Physics.gravity * (1.8f * dt);
        transform.position += _fallVel * dt;
        transform.Rotate(_fallSpin * dt, Space.Self);
        if (_fallTimer > 8.5f) Die("GRAVITY");
    }

    private void Die(string cause)
    {
        if (IsDead) return;
        IsDead = true;
        _game.OnBulletDied(cause, Distance);
    }
}
