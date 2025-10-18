using UnityEngine;

/// Procedural weapon recoil + FOV wobble using critically-damped springs (pos, rot, fov).
/// Attach to a weapon model root (your existing recoilTarget). Call Kick() on fire.
/// Call Tick(Time.deltaTime) each frame from your update loop.
[DisallowMultipleComponent]
public class RecoilAnimator : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("If empty, uses this transform.")]
    public Transform target;

    [Header("Kick (local space)")]
    [Tooltip("Base position kick (x=right, y=up, z=forward). Use negative Z to move back.")]
    public Vector3 posKick = new Vector3(0.0f, 0.01f, -0.06f);
    [Tooltip("Base rotation kick in degrees (x=pitch up/down, y=yaw, z=roll).")]
    public Vector3 rotKick = new Vector3(-3.5f, 0.8f, 1.2f);

    [Header("Randomness (per shot, +/- ranges)")]
    public Vector3 posRand = new Vector3(0.005f, 0.005f, 0.015f);
    public Vector3 rotRand = new Vector3(1.5f, 1.25f, 2.0f);

    [Header("Spring (shared)")]
    [Tooltip("Oscillation frequency in Hz (stiffness). 10–16 feels snappy.")]
    [Range(1f, 30f)] public float frequency = 14f;
    [Tooltip("1 = critical damping (no overshoot). 0.6–0.9 usually nice.")]
    [Range(0.3f, 2f)] public float damping = 0.9f;

    [Header("Clamp")]
    [Tooltip("Max positional offset from rest (local units).")]
    public Vector3 maxPos = new Vector3(0.03f, 0.03f, 0.09f);
    [Tooltip("Max rotational offset from rest (degrees).")]
    public Vector3 maxRot = new Vector3(10f, 6f, 10f);

    [Header("Multipliers")]
    [Tooltip("Global scaler for all kicks.")]
    public float intensity = 1f;
    [Tooltip("Extra scaling for full-auto to feel heavier if needed.")]
    public float autoFireMultiplier = 1f;

    [Header("Optional Camera Micro Kick (rotation)")]
    public Camera kickCamera;              // leave null to ignore
    public float camPitchPerKick = -0.15f; // tiny pitch (negative = up)
    public float camYawPerKick = 0.05f;    // tiny yaw
    public float camDamp = 9f;             // recovery speed

    [Header("FOV Wobble (Spring-based)")]
    [Tooltip("Enable springy FOV wobble on fire.")]
    public bool enableFovWobble = true;
    [Tooltip("Base FOV kick added per shot (in degrees).")]
    public float fovKick = 0.8f;
    [Tooltip("+/- randomness added to each FOV kick (degrees).")]
    public float fovRand = 0.35f;
    [Tooltip("Max absolute FOV offset from base (degrees).")]
    public float fovMax = 4.0f;
    [Tooltip("Spring frequency for FOV (Hz). If <= 0 uses shared frequency.")]
    public float fovFrequency = 0f;
    [Tooltip("Damping for FOV spring. If <= 0 uses shared damping.")]
    public float fovDamping = 0f;

    [Header("FOV Micro Jitter (subtle, continuous)")]
    [Tooltip("Adds a tiny subtle noise to FOV (0 to disable).")]
    public float fovNoiseAmplitude = 0.15f;
    [Tooltip("Speed of the FOV noise wobble.")]
    public float fovNoiseSpeed = 5.5f;
    [Tooltip("Random per-instance seed for FOV noise.")]
    public float fovNoiseSeed = 0.123f;

    // runtime
    Transform _t;
    Vector3 _baseLocalPos, _pos, _posVel;
    Vector3 _baseLocalEuler, _rot, _rotVel;

    // camera state (rotation micro kick)
    float _camPitch, _camYaw;
    Quaternion _camBaseLocalRot;
    float _time;

    // FOV spring state
    float _baseFov;
    float _fov;     // current offset from base
    float _fovVel;  // velocity for spring

    void Awake()
    {
        _t = target ? target : transform;
        _baseLocalPos = _t.localPosition;
        _baseLocalEuler = _t.localEulerAngles;

        if (kickCamera)
        {
            _camBaseLocalRot = kickCamera.transform.localRotation;
            _baseFov = kickCamera.fieldOfView;
        }
    }

    /// <summary>
    /// Call this on every shot. multiplier can scale per-weapon or ADS.
    /// </summary>
    public void Kick(float multiplier = 1f, bool isAuto = false)
    {
        float k = intensity * multiplier * (isAuto ? autoFireMultiplier : 1f);

        // Per-shot randomized impulses (add to velocity for spring)
        Vector3 p = posKick + new Vector3(
            Random.Range(-posRand.x, posRand.x),
            Random.Range(-posRand.y, posRand.y),
            Random.Range(-posRand.z, posRand.z)
        );
        Vector3 r = rotKick + new Vector3(
            Random.Range(-rotRand.x, rotRand.x),
            Random.Range(-rotRand.y, rotRand.y),
            Random.Range(-rotRand.z, rotRand.z)
        );

        _posVel += p * k;
        _rotVel += r * k;

        // Optional camera rotation micro kick (very subtle)
        if (kickCamera)
        {
            _camPitch += camPitchPerKick * k;
            _camYaw += camYawPerKick * k;

            // FOV kick (with randomness) -> add to velocity of FOV spring
            if (enableFovWobble)
            {
                float fKick = fovKick + Random.Range(-fovRand, fovRand);
                _fovVel += fKick * k;
            }
        }
    }

    /// <summary>
    /// Call this every frame with deltaTime.
    /// </summary>
    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        _time += dt;

        // Shared spring parameters
        float sharedW = Mathf.PI * 2f * Mathf.Max(0.01f, frequency);
        float sharedD = Mathf.Clamp(damping, 0.01f, 4f);
        float f1 = -(sharedW * sharedW);
        float f2 = -2f * sharedD * sharedW;

        // Position spring
        Vector3 accP = f1 * _pos + f2 * _posVel;
        _posVel += accP * dt;
        _pos += _posVel * dt;
        _pos = new Vector3(
            Mathf.Clamp(_pos.x, -maxPos.x, maxPos.x),
            Mathf.Clamp(_pos.y, -maxPos.y, maxPos.y),
            Mathf.Clamp(_pos.z, -maxPos.z, maxPos.z)
        );

        // Rotation spring (Euler degrees)
        Vector3 accR = f1 * _rot + f2 * _rotVel;
        _rotVel += accR * dt;
        _rot += _rotVel * dt;
        _rot = new Vector3(
            Mathf.Clamp(_rot.x, -maxRot.x, maxRot.x),
            Mathf.Clamp(_rot.y, -maxRot.y, maxRot.y),
            Mathf.Clamp(_rot.z, -maxRot.z, maxRot.z)
        );

        // Apply to target transform
        _t.localPosition = _baseLocalPos + _pos;
        _t.localRotation = Quaternion.Euler(_baseLocalEuler) * Quaternion.Euler(_rot);

        // Camera stuff
        if (kickCamera)
        {
            // Camera micro-aim return (rotation), applied relative to base rot (no accumulation bugs)
            float rt = Mathf.Exp(-camDamp * dt);
            _camPitch *= rt;
            _camYaw *= rt;

            Quaternion camKickRot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            kickCamera.transform.localRotation = camKickRot * _camBaseLocalRot;

            // FOV spring
            if (enableFovWobble)
            {
                // Use dedicated spring constants for FOV if provided, else shared
                float w = (fovFrequency > 0f ? fovFrequency : frequency);
                float d = (fovDamping > 0f ? fovDamping : damping);
                float wf = Mathf.PI * 2f * Mathf.Max(0.01f, w);
                float df = Mathf.Clamp(d, 0.01f, 4f);

                float ff1 = -(wf * wf);
                float ff2 = -2f * df * wf;

                // Integrate 1D spring for FOV offset
                float accF = ff1 * _fov + ff2 * _fovVel;
                _fovVel += accF * dt;
                _fov += _fovVel * dt;
                _fov = Mathf.Clamp(_fov, -fovMax, fovMax);

                // Add a tiny continuous noise (micro jitter). Set amplitude to 0 to disable.
                float noise = 0f;
                if (fovNoiseAmplitude > 0f)
                {
                    // Perlin gives 0..1; remap to -1..+1
                    float n = Mathf.PerlinNoise(_time * fovNoiseSpeed, fovNoiseSeed) * 2f - 1f;
                    noise = n * fovNoiseAmplitude;
                }

                kickCamera.fieldOfView = _baseFov + _fov + noise;
            }
        }
    }

    // Optional: helper to instantly reset to rest (e.g., on weapon swap/ADS change)
    public void ResetToRest()
    {
        _pos = Vector3.zero; _posVel = Vector3.zero;
        _rot = Vector3.zero; _rotVel = Vector3.zero;
        _camPitch = 0f; _camYaw = 0f;
        _fov = 0f; _fovVel = 0f;

        if (kickCamera)
        {
            kickCamera.transform.localRotation = _camBaseLocalRot;
            kickCamera.fieldOfView = _baseFov;
        }

        if (_t)
        {
            _t.localPosition = _baseLocalPos;
            _t.localRotation = Quaternion.Euler(_baseLocalEuler);
        }
    }
}
