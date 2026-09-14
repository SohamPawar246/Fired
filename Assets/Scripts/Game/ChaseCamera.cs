using UnityEngine;

/// <summary>
/// The "sitting on the back of the bullet" chase cam (GDD §3.1). Loose-follows
/// the bullet from behind and above so turns read and impacts happen around and
/// past the bullet, which stays lower-centre of frame. Pulls in at low speed,
/// pushes back + widens FOV at high speed.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public BulletController bullet;

    [Header("Rig")]
    public float distance = 6f;
    public float height = 1.6f;
    public float lookAhead = 8f;
    public float posLag = 0.08f;
    public float rotLag = 12f;
    public float collisionRadius = 0.3f;
    public float collisionPadding = 0.08f;

    [Header("FOV")]
    public float baseFov = 70f;
    public float maxFov = 96f;

    private Camera _cam;
    private Vector3 _vel;
    private float _ricochetTimer;
    private float _ricochetDuration;
    private Vector3 _ricochetPos;
    private Vector3 _ricochetLook;
    private float _ricochetRoll;
    private float _ricochetFov;

    private float _impactTimer;
    private float _impactDuration;
    private Vector3 _impactPos;
    private Vector3 _impactLook;
    private float _impactRoll;
    private float _impactFov;
    private Vector3 _stableForward = Vector3.forward;
    private float _collisionDistance;
    private float _collisionDistanceVel;

    private void Awake() => _cam = GetComponent<Camera>();

    /// <summary>Call right before enabling: zero the follow velocities and adopt
    /// the current transform as the starting pose, so the first frames blend
    /// smoothly from wherever the intro camera left off instead of popping.</summary>
    public void BeginFollow()
    {
        _vel = Vector3.zero;
        _collisionDistance = 0f;
        _collisionDistanceVel = 0f;
        _ricochetTimer = 0f;
        _impactTimer = 0f;
        if (target != null) _stableForward = target.forward;
    }

    public void PlayRicochetCinematic(Vector3 point, Vector3 normal, Vector3 reflection, bool hard)
    {
        Vector3 side = Vector3.Cross(Vector3.up, reflection).normalized;
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        if (Vector3.Dot(side, normal) < 0f) side = -side;

        float sideOffset = hard ? 3.2f : 2.6f;
        float backOffset = hard ? 1.35f : 0.95f;
        _ricochetPos = point + side * sideOffset + Vector3.up * 1.15f - reflection * backOffset;
        _ricochetLook = point + reflection * 2.8f + Vector3.up * 0.15f;
        _ricochetRoll = hard ? 10f : 5f;
        _ricochetFov = hard ? 66f : 70f;
        _ricochetDuration = hard ? 0.72f : 0.46f;
        _ricochetTimer = _ricochetDuration;
    }

    public void PlayEnemyImpactCinematic(Vector3 point, Vector3 forward)
    {
        Vector3 dir = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;

        _impactPos = point - dir * 4.8f + Vector3.up * 1.9f + side * 1.1f;
        _impactLook = point + dir * 2.2f + Vector3.up * 0.55f;
        _impactRoll = Vector3.Dot(side, transform.right) >= 0f ? 9f : -9f;
        _impactFov = 58f;
        _impactDuration = 1.05f;
        _impactTimer = _impactDuration;
    }

    private void LateUpdate()
    {
        if (target == null) return;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        float t = bullet != null
            ? Mathf.InverseLerp(bullet.StallSpeed, bullet.SoftCap, bullet.Speed)
            : 0.5f;

        bool falling = bullet != null && bullet.IsFalling;
        if (!falling && target.forward.sqrMagnitude > 0.0001f)
            _stableForward = Vector3.Slerp(_stableForward, target.forward.normalized, 1f - Mathf.Exp(-6f * dt));

        float dist = Mathf.Lerp(distance * 0.78f, distance, t);
        Vector3 desired = target.position - target.forward * dist + target.up * height;
        Vector3 lookPoint = target.position + target.forward * lookAhead;
        float targetFov = Mathf.Lerp(baseFov, maxFov, t);

        if (falling)
        {
            desired = target.position - _stableForward * (distance * 0.85f) + Vector3.up * (height + 0.6f);
            lookPoint = target.position + Vector3.up * 0.6f;
            targetFov = Mathf.Lerp(60f, 66f, t);
        }

        if (!falling && _ricochetTimer > 0f)
        {
            _ricochetTimer = Mathf.Max(0f, _ricochetTimer - dt);
            float holdT = 1f - (_ricochetTimer / Mathf.Max(_ricochetDuration, 0.0001f));
            float blend = holdT < 0.7f ? 1f : Mathf.Clamp01(1f - ((holdT - 0.7f) / 0.3f));
            desired = Vector3.Lerp(desired, _ricochetPos, blend);
            lookPoint = Vector3.Lerp(lookPoint, _ricochetLook, blend);
            targetFov = Mathf.Lerp(targetFov, _ricochetFov, blend);
        }

        if (!falling && _impactTimer > 0f)
        {
            _impactTimer = Mathf.Max(0f, _impactTimer - dt);
            float holdT = 1f - (_impactTimer / Mathf.Max(_impactDuration, 0.0001f));
            float blend = holdT < 0.65f ? 1f : Mathf.Clamp01(1f - ((holdT - 0.65f) / 0.35f));
            desired = Vector3.Lerp(desired, _impactPos, blend);
            lookPoint = Vector3.Lerp(lookPoint, _impactLook, blend);
            targetFov = Mathf.Lerp(targetFov, _impactFov, blend);
        }

        Quaternion look = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        if (!falling && _ricochetTimer > 0f)
        {
            float holdT = 1f - (_ricochetTimer / Mathf.Max(_ricochetDuration, 0.0001f));
            float blend = holdT < 0.7f ? 1f : Mathf.Clamp01(1f - ((holdT - 0.7f) / 0.3f));
            look *= Quaternion.Euler(0f, 0f, _ricochetRoll * blend);
        }
        if (!falling && _impactTimer > 0f)
        {
            float holdT = 1f - (_impactTimer / Mathf.Max(_impactDuration, 0.0001f));
            float blend = holdT < 0.65f ? 1f : Mathf.Clamp01(1f - ((holdT - 0.65f) / 0.35f));
            look *= Quaternion.Euler(0f, 0f, _impactRoll * blend);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, posLag, Mathf.Infinity, dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotLag * dt));

        if (!falling)
        {
            Vector3 toDesired = transform.position - target.position;
            float cameraDistance = toDesired.magnitude;
            if (cameraDistance > 0.001f)
            {
                float desiredDistance = cameraDistance;
                if (Physics.SphereCast(target.position, collisionRadius, toDesired.normalized, out RaycastHit hit, cameraDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    desiredDistance = Mathf.Max(0.25f, hit.distance - collisionPadding);
                }

                _collisionDistance = Mathf.SmoothDamp(_collisionDistance <= 0f ? cameraDistance : _collisionDistance, desiredDistance, ref _collisionDistanceVel, 0.06f, Mathf.Infinity, dt);
                Vector3 clippedPos = target.position + toDesired.normalized * _collisionDistance;
                transform.position = clippedPos;
            }
        }

        if (_cam != null)
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, 1f - Mathf.Exp(-6f * dt));
    }
}
