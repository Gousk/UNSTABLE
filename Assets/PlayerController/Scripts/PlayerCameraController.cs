using UnityEngine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraRig;   // yaw + pitch pivot
    [SerializeField] private Camera mainCamera;   // MainCamera
    [SerializeField] private Transform weaponRig;   // el/silah parent (tercihen MainCamera child'ı)

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 1.5f;
    [SerializeField] private float gamepadSensitivity = 3.0f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private float mouseDeltaClamp = 500f;

    [Header("Tilt (Roll)")]
    [SerializeField] private float rollSmooth = 14f;  // ne kadar hızlı hedef roll'e gider
    private float rollTarget = 0f;
    private float currentRoll = 0f;

    private float yaw;
    private float pitch;

    public void SetSensitivity(float mouse, float gamepad)
    {
        mouseSensitivity = mouse;
        gamepadSensitivity = gamepad;
    }

    public void SetRollTarget(float rollDeg)
    {
        rollTarget = rollDeg;
    }

    private void Reset()
    {
        if (cameraRig == null)
        {
            var rig = transform.Find("CameraRig");
            if (rig) cameraRig = rig;
        }
        if (mainCamera == null) mainCamera = Camera.main;
        if (weaponRig == null && mainCamera != null)
        {
            var wr = mainCamera.transform.Find("WeaponRig");
            if (wr) weaponRig = wr;
        }
    }

    private void Awake()
    {
        if (cameraRig == null) cameraRig = transform;
        var e = cameraRig.eulerAngles;
        yaw = e.y; pitch = e.x;
        currentRoll = 0f;
        rollTarget = 0f;
    }

    /// <summary>
    /// lookDelta: Input System'den gelen değer.
    /// fromMouse: true → mouse delta (frame-based)
    ///            false → gamepad stick (per-second, dt ile çarp)
    /// </summary>
    public void TickLook(Vector2 lookDelta, bool fromMouse, float dt)
    {
        if (fromMouse)
        {
            lookDelta.x = Mathf.Clamp(lookDelta.x, -mouseDeltaClamp, mouseDeltaClamp);
            lookDelta.y = Mathf.Clamp(lookDelta.y, -mouseDeltaClamp, mouseDeltaClamp);
        }

        float mx = fromMouse ? (lookDelta.x * mouseSensitivity) : (lookDelta.x * gamepadSensitivity * 100f * dt);
        float my = fromMouse ? (lookDelta.y * mouseSensitivity) : (lookDelta.y * gamepadSensitivity * 100f * dt);

        yaw += mx;
        pitch -= my;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // roll'ü hedefe yumuşat
        currentRoll = Mathf.Lerp(currentRoll, rollTarget, 1f - Mathf.Exp(-rollSmooth * dt));

        cameraRig.rotation = Quaternion.Euler(pitch, yaw, currentRoll);

        if (weaponRig != null && mainCamera != null && weaponRig.parent != mainCamera.transform)
        {
            weaponRig.position = mainCamera.transform.position;
            weaponRig.rotation = mainCamera.transform.rotation;
        }
    }
}
