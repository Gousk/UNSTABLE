using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CrosshairHitUI : MonoBehaviour
{
    public Image hitImage;
    [Tooltip("Toplam UI efekt süresi.")]
    public float duration = 0.12f;
    [Tooltip("Tepe alpha (0-1).")]
    [Range(0f, 1f)] public float maxAlpha = 0.9f;
    [Tooltip("Tepe ölçek katsayısı.")]
    public float punchScale = 1.25f;

    Vector3 baseScale;
    Coroutine routine;

    void Awake()
    {
        if (hitImage == null) hitImage = GetComponent<Image>();
        baseScale = hitImage.transform.localScale;
        SetAlpha(0f);
    }

    void OnEnable() { HitConfirm.OnHit += Play; }
    void OnDisable() { HitConfirm.OnHit -= Play; }

    public void Play()
    {
        if (!gameObject.activeInHierarchy) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        float t = 0f, d = Mathf.Max(0.01f, duration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime; // oyun yavaşlasa da his sabit kalsın
            float u = Mathf.Clamp01(t / d);
            float wave = Mathf.Sin(u * Mathf.PI); // 0->1->0

            // alpha ve scale animasyonu
            SetAlpha(wave * maxAlpha);
            hitImage.transform.localScale = Vector3.Lerp(baseScale, baseScale * punchScale, wave);

            yield return null;
        }
        SetAlpha(0f);
        hitImage.transform.localScale = baseScale;
        routine = null;
    }

    void SetAlpha(float a)
    {
        var c = hitImage.color;
        c.a = a;
        hitImage.color = c;
    }
}
