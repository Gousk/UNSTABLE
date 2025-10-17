using UnityEngine;

/// <summary>
/// Koşu (cap'li) + momentum koruma + responsive turning + WALLRUN (iki tarafta da doğru, hız korunur).
/// Tilt yönü her kare: sign(dot(-wallNormal, cameraRig.right)) * cameraTiltOnWall  → daima duvara doğru.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraRig;
    [SerializeField] private Transform groundProbe;
    [SerializeField] private PlayerCameraController cameraController; // opsiyonel tilt
    private CharacterController cc;

    [Header("Run Speeds")]
    [SerializeField] private float walkSpeed = 5.0f;
    [SerializeField] private float sprintSpeed = 8.0f;

    [Header("Ground Responsiveness")]
    [SerializeField] private float groundAccel = 80f;        // m/s^2
    [SerializeField] private float groundBrake = 140f;       // m/s^2
    [SerializeField] private float strafeFriction = 60f;     // m/s^2
    [SerializeField] private float groundDecelNoInput = 28f; // m/s^2

    [Header("Air Control")]
    [SerializeField, Range(0f, 1f)] private float airControl = 0.65f;
    [SerializeField] private float airTurnRate = 12f;        // 1/s
    [SerializeField] private float airAccel = 0f;            // genelde 0 bırak

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.35f;
    [SerializeField] private float gravity = -22f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBuffer = 0.12f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float probeRadius = 0.28f;
    [SerializeField] private float probeRay = 0.2f;
    [SerializeField] private float snapDistance = 0.08f;

    // ================= WALLRUN =================
    [Header("Wallrun")]
    [SerializeField] private LayerMask wallMask = ~0;
    [SerializeField] private float wallCheckDistance = 0.9f;
    [SerializeField, Range(0f, 1f)] private float wallGravityScale = 0.25f;
    [SerializeField] private float wallRunMaxTime = 3f;      // 0 = sınırsız
    [SerializeField] private float wallRunCooldown = 0.15f;
    [SerializeField] private float wallJumpUp = 7.5f;
    [SerializeField] private float wallJumpAway = 6.0f;
    [SerializeField] private float wallJumpForward = 4.0f;
    [SerializeField] private float wallAlignRate = 10f;      // 1/s
    [SerializeField] private float cameraTiltOnWall = 10f;   // 0 kapalı

    // State
    private Vector3 planarVel;   // XZ
    private float verticalVel; // Y
    private float lastGroundedTime;
    private float lastJumpPressedTime;
    private bool sprintActive; // yerde güncellenir (latch)

    // Wallrun state
    private bool isWallRunning;
    private Vector3 wallNormal;
    private Vector3 wallTangent;     // işareti stabil
    private float wallRunStartTime;
    private float lastWallRunEndTime;

    private Vector3 Up => Vector3.up;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraRig == null) cameraRig = transform;

        if (groundProbe == null)
        {
            var g = new GameObject("GroundProbe");
            g.transform.SetParent(transform);
            g.transform.localPosition = new Vector3(0f, -cc.height * 0.5f + cc.skinWidth + 0.02f, 0f);
            groundProbe = g.transform;
        }
    }

    // ========= Update (called by Player.cs) =========
    public void TickMovement(Vector2 moveInput, bool sprintHeld, float dt)
    {
        bool grounded = IsGrounded();
        if (grounded) lastGroundedTime = Time.time;
        if (grounded) sprintActive = sprintHeld;

        // Input yönleri
        Vector3 fwd = cameraRig.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 rgt = cameraRig.right; rgt.y = 0f; rgt.Normalize();
        Vector3 wishDir = (fwd * moveInput.y + rgt * moveInput.x);
        float wishMag = Mathf.Clamp01(wishDir.magnitude);
        if (wishMag > 0f) wishDir /= Mathf.Max(wishMag, 0.0001f);

        float runCap = (sprintActive ? sprintSpeed : walkSpeed) * wishMag;

        // Jump'ı önce tüket
        bool jumpedThisFrame = TryConsumeJumpImmediate(grounded);

        // ---- Wallrun FSM ----
        if (!grounded && !jumpedThisFrame)
        {
            if (!isWallRunning) TryStartWallRun();
            else
            {
                bool keep = UpdateWallRun(dt);
                if (!keep) StopWallRun();
            }
        }
        else if (isWallRunning) StopWallRun();

        // ---- Normal / Air movement ----
        if (grounded)
        {
            if (wishMag > 0f)
            {
                Vector3 desiredDir = wishDir;
                float vAlong = Vector3.Dot(planarVel, desiredDir);
                Vector3 vPerp = planarVel - desiredDir * vAlong;

                // yan bileşeni söndür (responsive)
                float perpMag = vPerp.magnitude;
                if (perpMag > 0f)
                {
                    float drop = strafeFriction * dt;
                    float newPerpMag = Mathf.Max(0f, perpMag - drop);
                    vPerp = (vPerp / perpMag) * newPerpMag;
                }

                // karşı yöne fren
                if (vAlong < 0f) vAlong = Mathf.Min(0f, vAlong + groundBrake * dt);

                // cap'e kadar hızlan (cap üstünde ivme verme → momentum korunur)
                if (vAlong < runCap) vAlong = Mathf.Min(runCap, vAlong + groundAccel * dt);

                planarVel = desiredDir * vAlong + vPerp;
            }
            else
            {
                planarVel = Vector3.MoveTowards(planarVel, Vector3.zero, groundDecelNoInput * dt);
            }

            if (verticalVel < 0f) verticalVel = -2f; // zemine hafif yapış
            TryGroundSnap();
        }
        else if (!isWallRunning) // normal havada
        {
            if (planarVel.sqrMagnitude > 0.000001f && wishMag > 0f && airControl > 0f)
            {
                float t = 1f - Mathf.Exp(-airTurnRate * Mathf.Clamp01(airControl) * dt);
                Vector3 newDir = Vector3.Slerp(planarVel.normalized, wishDir, t).normalized;
                planarVel = newDir * planarVel.magnitude; // hız büyüklüğünü koru
            }

            if (airAccel > 0f && wishMag > 0f)
            {
                float along = Vector3.Dot(planarVel, wishDir);
                if (along < runCap)
                {
                    along = Mathf.Min(runCap, along + airAccel * dt);
                    Vector3 vPerp = planarVel - wishDir * Vector3.Dot(planarVel, wishDir);
                    planarVel = wishDir * along + vPerp;
                }
            }

            verticalVel += gravity * dt;
        }

        // Uygula
        Vector3 total = planarVel + Up * verticalVel;
        cc.Move(total * dt);
    }

    public void QueueJump() => lastJumpPressedTime = Time.time;

    // ================= WALLRUN CORE =================

    private void TryStartWallRun()
    {
        if (Time.time < lastWallRunEndTime + wallRunCooldown) return;

        // 8 yönlü tarama (transform uzayında): sağ/sol/ileri/geri + çaprazlar
        Vector3 up = Vector3.up;
        Vector3 f = transform.forward; f.y = 0f; f.Normalize();
        Vector3 r = transform.right; r.y = 0f; r.Normalize();

        Vector3[] dirs = new Vector3[]
        {
            r, -r, f, -f,
            (r+f).normalized, (r-f).normalized, (-r+f).normalized, (-r-f).normalized
        };

        Vector3 origin = transform.position + Vector3.up * 0.9f;

        bool found = false;
        RaycastHit bestHit = default;
        float bestDist = float.MaxValue;

        for (int i = 0; i < dirs.Length; i++)
        {
            if (Physics.Raycast(origin, dirs[i], out RaycastHit hit, wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Angle(hit.normal, up) < 15f) continue; // zemine yakın yüzeyleri alma
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }
        }
        if (!found) return;

        // Tangent: up x normal; işaretini mevcut yatay hız (yoksa kamera forward) ile seç
        Vector3 n = bestHit.normal.normalized;
        Vector3 t = Vector3.Cross(up, n).normalized;

        Vector3 pref = planarVel; pref.y = 0f;
        if (pref.sqrMagnitude < 0.01f)
        {
            pref = (cameraRig != null ? cameraRig.forward : transform.forward);
            pref.y = 0f;
        }

        if (pref.sqrMagnitude < 0.0001f)
        {
            pref = transform.forward;
            pref.y = 0f;
        }

        if (pref.sqrMagnitude > 0.0001f)
        {
            pref.Normalize();
            if (Vector3.Dot(pref, t) < 0f) t = -t;
        }
        
        wallNormal = n;
        wallTangent = t;
        isWallRunning = true;
        wallRunStartTime = Time.time;

        // --- TILT: kameraya göre duvar hangi tarafta? (her kare hesaplanacak ama ilk karede de ver)
        if (cameraController && cameraTiltOnWall != 0f)
        {
            float tiltSign = Mathf.Sign(Vector3.Dot(-wallNormal, cameraRig.right)); // duvar kameranın sağıdaysa +1
            cameraController.SetRollTarget(tiltSign * cameraTiltOnWall);
        }
    }

    private bool UpdateWallRun(float dt)
    {
        if (wallRunMaxTime > 0f && Time.time > wallRunStartTime + wallRunMaxTime) return false;

        // Duvarla teması doğrula: -wallNormal yönüne kısa ray
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        if (!Physics.Raycast(origin, -wallNormal, out RaycastHit h, wallCheckDistance + 0.15f, wallMask, QueryTriggerInteraction.Ignore))
            return false;

        // Normal & tangent güncelle (tangent işaretini stabil tut)
        Vector3 newN = h.normal.normalized;
        Vector3 newT = Vector3.Cross(Vector3.up, newN).normalized;
        if (Vector3.Dot(newT, wallTangent) < 0f) newT = -newT;

        wallNormal = Vector3.Slerp(wallNormal, newN, 0.5f).normalized;
        wallTangent = newT;

        // Momentum-preserving alignment (sadece yön değişir)
        float speed = planarVel.magnitude;
        if (speed > 0.0001f)
        {
            float t = 1f - Mathf.Exp(-wallAlignRate * dt);
            Vector3 dir = Vector3.Slerp(planarVel.normalized, wallTangent, t).normalized;
            planarVel = dir * speed;
        }

        // Gravity azalt
        verticalVel += gravity * wallGravityScale * dt;

        // --- TILT: her kare kameraya göre taraftan hesapla (daima duvara doğru)
        if (cameraController && cameraTiltOnWall != 0f)
        {
            float tiltSign = Mathf.Sign(Vector3.Dot(-wallNormal, cameraRig.right)); // sağda +, solda -
            cameraController.SetRollTarget(tiltSign * cameraTiltOnWall);
        }

        // Wall jump?
        bool bufferedJump = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (bufferedJump)
        {
            Vector3 away = wallNormal * wallJumpAway;
            Vector3 fwdBoost = wallTangent * wallJumpForward;
            verticalVel = wallJumpUp;
            planarVel += away + fwdBoost; // momentuma ekle
            lastJumpPressedTime = -999f;
            return false;
        }

        return true;
    }

    private void StopWallRun()
    {
        isWallRunning = false;
        lastWallRunEndTime = Time.time;
        if (cameraController) cameraController.SetRollTarget(0f);
    }

    // ============== CORE HELPERS ==============

    private bool TryConsumeJumpImmediate(bool groundedNow)
    {
        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBuffer;

        if (buffered && (groundedNow || canCoyote))
        {
            float jumpVel = Mathf.Sqrt(-2f * gravity * jumpHeight);
            verticalVel = jumpVel;
            lastJumpPressedTime = -999f;
            return true;
        }
        return false;
    }

    private bool IsGrounded()
    {
        if (cc.isGrounded) return true;

        Vector3 origin = groundProbe.position + Up * 0.05f;
        if (Physics.SphereCast(origin, probeRadius, Vector3.down, out RaycastHit hit, probeRay, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (Vector3.Angle(hit.normal, Up) <= cc.slopeLimit + 0.5f) return true;
        }
        if (Physics.CheckSphere(groundProbe.position, probeRadius * 0.9f, groundMask, QueryTriggerInteraction.Ignore))
            return true;

        return false;
    }

    private void TryGroundSnap()
    {
        Vector3 origin = transform.position + Up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, snapDistance + 0.1f, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (Vector3.Angle(hit.normal, Up) <= cc.slopeLimit + 0.5f)
            {
                float diff = (origin.y - hit.point.y) - 0.1f;
                if (diff > 0f) cc.Move(Vector3.down * Mathf.Min(diff, snapDistance));
            }
        }
    }
}
