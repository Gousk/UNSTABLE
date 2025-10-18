using UnityEngine;

[ExecuteAlways]
public class BillboardParallaxLocalBinder : MonoBehaviour
{
    public enum LocalAxis { X, Y, Z }

    [Header("Sprite quad (uses a shader that reads _DeltaUVMeters)")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Moving object (use the ROOT that actually translates)")]
    [SerializeField] private Transform anchorRoot; // we read localPosition from this

    [Header("Map local axes to U/V scrolling")]
    [SerializeField] private LocalAxis uFrom = LocalAxis.X;  // horizontal scroll source
    [SerializeField] private LocalAxis vFrom = LocalAxis.Y;  // vertical scroll source

    [Header("Axis options")]
    [SerializeField] private bool invertU = false;  // flip left/right
    [SerializeField] private bool invertV = false;  // flip up/down

    [Header("Units")]
    [Tooltip("If ON, converts local delta into world meters using parent's lossyScale.")]
    [SerializeField] private bool convertLocalToWorldMeters = true;

    [Header("Filtering")]
    [SerializeField] private float deadzone = 0.00005f; // ignore tiny jitter

    [Header("Shader property name")]
    [SerializeField] private string deltaMetersProp = "_DeltaUVMeters";

    private MaterialPropertyBlock _mpb;
    private Vector3 _startLocalPos;

    void Reset()
    {
        targetRenderer = GetComponent<Renderer>();
        anchorRoot = transform;
    }

    void OnEnable()
    {
        if (!anchorRoot) return;
        _startLocalPos = anchorRoot.localPosition;
        Push();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Push();
    }

    void LateUpdate() => Push();

    void Push()
    {
        if (!targetRenderer || !anchorRoot) return;

        // 1) Local delta since start
        Vector3 dLocal = anchorRoot.localPosition - _startLocalPos;

        // 2) Optional: convert to world meters using parent's lossy scale
        Vector3 dMeters = dLocal;
        var parent = anchorRoot.parent;
        if (convertLocalToWorldMeters && parent != null)
        {
            Vector3 s = parent.lossyScale;
            // Avoid division by ~0; treat as 1 if very small.
            if (Mathf.Abs(s.x) < 1e-6f) s.x = 1f;
            if (Mathf.Abs(s.y) < 1e-6f) s.y = 1f;
            if (Mathf.Abs(s.z) < 1e-6f) s.z = 1f;
            // Local axis units to world meters (assuming model units == meters before scaling)
            dMeters = new Vector3(dLocal.x * s.x, dLocal.y * s.y, dLocal.z * s.z);
        }

        // 3) Pick which local components drive U and V
        float u = Select(dMeters, uFrom);
        float v = Select(dMeters, vFrom);

        // 4) Invert (we want image to move opposite to object motion)
        if (!invertU) u = +u; else u = -u; // you can flip here if you want opposite feel
        if (!invertV) v = +v; else v = -v;

        Vector4 uvMeters = new Vector4(u, v, 0, 0);

        // 5) Deadzone
        if (new Vector2(uvMeters.x, uvMeters.y).sqrMagnitude < deadzone * deadzone)
            uvMeters = Vector4.zero;

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(_mpb);
        _mpb.SetVector(deltaMetersProp, uvMeters);
        targetRenderer.SetPropertyBlock(_mpb);
    }

    static float Select(Vector3 v, LocalAxis ax)
    {
        switch (ax)
        {
            case LocalAxis.X: return v.x;
            case LocalAxis.Y: return v.y;
            default: return v.z;
        }
    }

    // Call to re-zero at runtime (e.g., after teleport)
    public void ReanchorHere()
    {
        if (anchorRoot) _startLocalPos = anchorRoot.localPosition;
    }
}
