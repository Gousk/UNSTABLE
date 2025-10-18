using UnityEngine;
using System.Collections;

public class DamageScaleFeedback : MonoBehaviour
{
    [Header("Scale Punch")]
    [Tooltip("Tepe noktadaki büyüme miktarı. 0.15 = %15 büyüme")]
    public float punchAmount = 0.15f;

    [Tooltip("Toplam efekt süresi (saniye).")]
    public float duration = 0.12f;

    [Tooltip("Efekt hangi Transform'a uygulansın? Boşsa bu objeye uygulanır.")]
    public Transform target;

    [Tooltip("Vuruşlar üst üste binerken sert geçişleri azaltmak için mevcut ölçekten başla.")]
    public bool startFromCurrentScale = true;

    Vector3 _baseScale;
    Coroutine _routine;

    void Awake()
    {
        if (target == null) target = transform;
        _baseScale = target.localScale;
    }

    void OnEnable()
    {
        // Pool’dan döndüğünde baz ölçeği koru
        _baseScale = target.localScale;
    }

    public void Play()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ScalePunch());
    }

    IEnumerator ScalePunch()
    {
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);

        // Başlangıç ve hedef akıllı seçilsin
        Vector3 start = startFromCurrentScale ? target.localScale : _baseScale;

        // Tek eğri: scale = base * (1 + punch * sin(pi * t))
        // t: 0..1 arası. 0’da 0, 0.5’te tepe, 1’de tekrar 0 (yumuşak).
        while (t < d)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            float wave = Mathf.Sin(u * Mathf.PI); // 0 -> 1 -> 0
            float factor = 1f + punchAmount * wave;

            // Oran base'e göre ama başlangıcı mevcut ölçeğe yakınsamak için blendle
            Vector3 targetScale = _baseScale * factor;
            target.localScale = Vector3.Lerp(start, targetScale, u);

            yield return null;
        }

        target.localScale = _baseScale;
        _routine = null;
    }
}
