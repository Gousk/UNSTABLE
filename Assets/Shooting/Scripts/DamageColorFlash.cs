using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DamageColorFlash : MonoBehaviour
{
    [System.Serializable]
    public class TargetRenderer
    {
        public Renderer renderer;
        [Tooltip("If empty, apply to all material slots of this renderer.")]
        public List<int> materialIndices = new List<int>();
    }

    [Header("Targets")]
    [Tooltip("If empty, auto-collect all child Renderers and apply to all their materials.")]
    public List<TargetRenderer> targets = new List<TargetRenderer>();

    [Header("Flash Settings")]
    public Color flashColor = Color.white;
    [Range(0f, 1f)] public float intensity = 0.8f; // blend strength
    public float duration = 0.12f;
    public bool useEmission = true;

    static readonly int ID_Color = Shader.PropertyToID("_Color");
    static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ID_Emission = Shader.PropertyToID("_EmissionColor");

    struct Slot
    {
        public Renderer r;
        public int matIndex;
        public MaterialPropertyBlock mpb;

        public bool hasBaseColor;
        public Color baseColor;

        public bool hasEmission;
        public Color baseEmission;
    }

    List<Slot> _slots = new List<Slot>();
    Coroutine _routine;

    void Awake()
    {
        if (targets.Count == 0)
        {
            // auto-discover
            var rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
                targets.Add(new TargetRenderer { renderer = r });
        }

        // Build slots
        foreach (var tr in targets)
        {
            if (!tr.renderer) continue;

            var r = tr.renderer;
            var sharedMats = r.sharedMaterials;
            if (sharedMats == null || sharedMats.Length == 0) continue;

            // if no indices provided, apply to all
            var indices = tr.materialIndices != null && tr.materialIndices.Count > 0
                        ? tr.materialIndices
                        : new List<int>(System.Linq.Enumerable.Range(0, sharedMats.Length));

            foreach (var idx in indices)
            {
                if (idx < 0 || idx >= sharedMats.Length) continue;
                var mat = sharedMats[idx];
                if (!mat) continue;

                var s = new Slot { r = r, matIndex = idx, mpb = new MaterialPropertyBlock() };
                r.GetPropertyBlock(s.mpb, idx);

                // COLOR: try _BaseColor first (URP/HDRP), then _Color (Built-in)
                if (mat.HasProperty(ID_BaseColor))
                {
                    s.hasBaseColor = true;
                    s.baseColor = mat.GetColor(ID_BaseColor);
                }
                else if (mat.HasProperty(ID_Color))
                {
                    s.hasBaseColor = true;
                    s.baseColor = mat.GetColor(ID_Color);
                }

                // EMISSION
                if (useEmission && mat.HasProperty(ID_Emission))
                {
                    s.hasEmission = true;
                    s.baseEmission = mat.GetColor(ID_Emission);

                    // ensure emission keyword on (URP/HDRP/Standard)
                    mat.EnableKeyword("_EMISSION");
                }

                _slots.Add(s);
            }
        }
    }

    public void Play()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        float t = 0f, d = Mathf.Max(0.01f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            float w = Mathf.Sin(u * Mathf.PI) * intensity; // 0->1->0

            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (!s.r) continue;

                // write into MPB
                if (s.hasBaseColor)
                {
                    Color c = Color.Lerp(s.baseColor, flashColor, w);
                    // write to whichever property exists
                    if (s.r.sharedMaterials[s.matIndex].HasProperty(ID_BaseColor))
                        s.mpb.SetColor(ID_BaseColor, c);
                    else
                        s.mpb.SetColor(ID_Color, c);
                }
                if (s.hasEmission)
                {
                    Color e = Color.Lerp(s.baseEmission, flashColor, w);
                    s.mpb.SetColor(ID_Emission, e);
                }

                s.r.SetPropertyBlock(s.mpb, s.matIndex);
            }
            yield return null;
        }

        // restore
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (!s.r) continue;
            if (s.hasBaseColor)
            {
                if (s.r.sharedMaterials[s.matIndex].HasProperty(ID_BaseColor))
                    s.mpb.SetColor(ID_BaseColor, s.baseColor);
                else
                    s.mpb.SetColor(ID_Color, s.baseColor);
            }
            if (s.hasEmission)
            {
                s.mpb.SetColor(ID_Emission, s.baseEmission);
            }
            s.r.SetPropertyBlock(s.mpb, s.matIndex);
        }

        _routine = null;
    }
}
