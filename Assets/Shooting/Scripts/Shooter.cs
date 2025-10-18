using UnityEngine;
using System.Collections;

public class Shooter : MonoBehaviour
{
    public enum ShotType { Projectile, Hitscan }
    public enum FireMode { Single, Burst, Auto }

    [Header("General")]
    public ShotType shotType = ShotType.Projectile;
    public FireMode fireMode = FireMode.Single;

    [Tooltip("Merminin/ışının çıkacağı referans nokta (namlu ucu).")]
    public Transform muzzle;

    [Tooltip("Raycast/mermi çarpacağı katmanlar.")]
    public LayerMask hitMask = ~0;

    [Tooltip("Verilecek hasar.")]
    public float damage = 10f;

    [Tooltip("Hitscan menzili ve kamera ray uzunluğu.")]
    public float range = 150f;

    [Tooltip("Saniyedeki atış sayısı (Single/Auto kadansı).")]
    public float fireRate = 10f;

    [Header("Burst")]
    [Tooltip("Burst modunda atılacak mermi sayısı.")]
    public int burstCount = 3;

    [Tooltip("Burst içindeki atışlar arası süre (saniye).")]
    public float burstInterval = 0.08f;

    [Header("Spread")]
    [Tooltip("0 = lazer gibi, 1-3° hafif; 5-8° belirgin dağılma.")]
    public float spreadDegrees = 0f;

    [Header("Aiming")]
    [Tooltip("Boşsa Camera.main kullanılır.")]
    public Camera aimCamera;

    [Tooltip("Kamera merkezine (crosshair) kilitli hedefleme. Kapalıysa muzzle.forward kullanılır.")]
    public bool useCameraCenterAim = true;

    [Header("Projectile Mode")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 60f;
    public float projectileLifetime = 5f;

    [Header("Hit Reaction (from weapon)")]
    [Tooltip("Hedefe uygulanacak impuls (N·s). 0 = kapalı.")]
    public float impactImpulse = 8f;

    [Tooltip("Impulse'u hasarla çar (pompalı daha çok iter).")]
    public bool scaleImpulseByDamage = true;

    [Header("FX (optional)")]
    public ParticleSystem muzzleFlash;
    public GameObject hitVfx;         // çarpışma efekti
    public LineRenderer tracerPrefab; // hitscan iz çizgisi

    [Header("SFX")]
    [Tooltip("Played each time the weapon fires.")]
    public AudioClip fireClip;
    [Range(0f, 2f)] public float fireVolume = 1f;
    [Tooltip("Base pitch for the fire sound.")]
    [Range(0.5f, 2f)] public float firePitch = 1f;
    [Tooltip("Random pitch variation added/subtracted each shot (e.g., 0.06).")]
    [Range(0f, 0.5f)] public float firePitchJitter = 0.06f;

    [Header("Recoil v2 (Spring-based)")]
    [Tooltip("RecoilAnimator bileşenini buraya sürükle (genelde silah mesh root).")]
    public RecoilAnimator recoilAnimator;
    [Tooltip("RecoilAnimator'a gönderilecek ekstra çarpan.")]
    public float recoilIntensity = 1f;

    // --- runtime ---
    float _nextShotTime;
    bool _isFiringBurst;

    void Awake()
    {
        if (aimCamera == null) aimCamera = Camera.main;
    }

    void Update()
    {
        bool pressed = Input.GetButtonDown("Fire1");
        bool held = Input.GetButton("Fire1");

        switch (fireMode)
        {
            case FireMode.Single:
                if (pressed) TryShoot();
                break;

            case FireMode.Auto:
                if (held) TryShoot();
                break;

            case FireMode.Burst:
                if (pressed && !_isFiringBurst)
                    StartCoroutine(BurstRoutine(burstCount, burstInterval));
                break;
        }

        // Spring recoil tick
        if (recoilAnimator) recoilAnimator.Tick(Time.deltaTime);
    }

    void TryShoot()
    {
        if (Time.time < _nextShotTime) return;
        _nextShotTime = Time.time + 1f / Mathf.Max(1f, fireRate);
        ShootOnce();
    }

    IEnumerator BurstRoutine(int count, float interval)
    {
        _isFiringBurst = true;
        for (int i = 0; i < count; i++)
        {
            ShootOnce();
            if (i < count - 1) yield return new WaitForSeconds(interval);
        }
        _nextShotTime = Time.time + 1f / Mathf.Max(1f, fireRate);
        _isFiringBurst = false;
    }

    void ShootOnce()
    {
        if (muzzle == null) return;

        if (muzzleFlash) muzzleFlash.Play();
        PlayFireSfx();

        // Recoil v2
        if (recoilAnimator)
        {
            bool isAuto = (fireMode == FireMode.Auto) || (fireMode == FireMode.Burst && _isFiringBurst);
            recoilAnimator.Kick(recoilIntensity, isAuto);
        }

        // 1) Kamera merkezinden hedef noktayı çöz
        Vector3 targetPoint = GetCameraCenterTargetPoint(out Ray camRay);

        // 2) Namludan hedefe yön
        Vector3 dirFromMuzzle = (targetPoint - muzzle.position).normalized;

        if (shotType == ShotType.Projectile)
            FireProjectile(dirFromMuzzle);
        else
            FireHitscan(camRay, targetPoint, dirFromMuzzle);
    }

    /// Kamera merkezinden (0.5,0.5) ray at; spread'i kamera ekseninde uygula.
    Vector3 GetCameraCenterTargetPoint(out Ray camRay)
    {
        if (!useCameraCenterAim || aimCamera == null)
        {
            Vector3 baseDir = GetSpreadDirection(muzzle.forward, spreadDegrees);
            camRay = new Ray(muzzle.position, baseDir);
            return muzzle.position + baseDir * range;
        }

        camRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Spread kameranın yönünde uygulansın ki crosshair etrafına saçılım olsun
        Vector3 dir = GetSpreadDirection(camRay.direction, spreadDegrees);
        camRay = new Ray(camRay.origin, dir);

        if (Physics.Raycast(camRay, out RaycastHit camHit, range, hitMask, QueryTriggerInteraction.Ignore))
            return camHit.point;

        return camRay.origin + camRay.direction * range;
    }

    Vector3 GetSpreadDirection(Vector3 baseDir, float degrees)
    {
        if (degrees <= 0f) return baseDir;
        float rad = degrees * Mathf.Deg2Rad;

        // baseDir'e dik iki eksen
        Vector3 ortho = Vector3.Cross(baseDir, Vector3.up);
        if (ortho.sqrMagnitude < 1e-4f) ortho = Vector3.Cross(baseDir, Vector3.right);
        ortho.Normalize();
        Vector3 ortho2 = Vector3.Cross(baseDir, ortho).normalized;

        // koni içinde uniform sapma
        float a = Random.Range(0f, 2f * Mathf.PI);
        float s = Random.Range(0f, Mathf.Sin(rad)); // uniform koni
        float off = Mathf.Asin(s);
        Vector3 axis = (Mathf.Cos(a) * ortho + Mathf.Sin(a) * ortho2).normalized;

        return (Quaternion.AngleAxis(off * Mathf.Rad2Deg, axis) * baseDir).normalized;
    }

    void FireProjectile(Vector3 dirFromMuzzle)
    {
        if (!projectilePrefab) return;

        GameObject go = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(dirFromMuzzle));

        var proj = go.GetComponent<SimpleProjectile>();
        if (!proj) proj = go.AddComponent<SimpleProjectile>();

        // Silahdan impulse ayarlarını mermiye aktar
        proj.Initialize(damage, projectileSpeed, hitMask, projectileLifetime, hitVfx,
                        impactImpulse, scaleImpulseByDamage);
    }

    /// Hitscan iki aşamalı:
    /// 1) Kamera ray'i hedefi belirler (crosshair ile hizalı)
    /// 2) Namludan hedefe ray: namlu önündeki engelleri önce vurur
    void FireHitscan(Ray cameraRay, Vector3 cameraTargetPoint, Vector3 dirFromMuzzle)
    {
        if (Physics.Raycast(muzzle.position, dirFromMuzzle, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            ApplyDamageAndImpulse(hit);
            HitConfirm.Raise();
            if (hitVfx) Instantiate(hitVfx, hit.point, Quaternion.LookRotation(hit.normal));
            SpawnTracer(muzzle.position, hit.point);
            return;
        }

        // Engel yoksa tracer'ı kamera hedef noktasına (menzil içindeyse) çiz
        Vector3 end = muzzle.position + dirFromMuzzle * range;
        float camDist = Vector3.Distance(cameraRay.origin, cameraTargetPoint);
        if (camDist <= range + 0.01f) end = cameraTargetPoint;

        SpawnTracer(muzzle.position, end);
    }

    void ApplyDamageAndImpulse(RaycastHit hit)
    {
        // Hasar (IDamageable child/parent fark etmez)
        var dmg = hit.collider.GetComponent<IDamageable>()
               ?? hit.collider.GetComponentInParent<IDamageable>();
        if (dmg != null) dmg.TakeDamage(damage, hit);

        // Fiziksel itme (silahdan)
        var rb = hit.rigidbody ?? hit.collider.attachedRigidbody;
        if (rb != null && impactImpulse > 0f)
        {
            Vector3 forceDir = -hit.normal; // yüzeye dik, içeri doğru
            float impulse = impactImpulse * (scaleImpulseByDamage ? Mathf.Max(0.01f, damage) : 1f);
            rb.AddForceAtPosition(forceDir * impulse, hit.point, ForceMode.Impulse);
        }
    }

    void SpawnTracer(Vector3 start, Vector3 end)
    {
        if (!tracerPrefab) return;
        var tracer = Instantiate(tracerPrefab);
        tracer.positionCount = 2;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        Destroy(tracer.gameObject, 0.05f);
    }

    void PlayFireSfx()
    {
        if (!fireClip) return;

        float jitter = (firePitchJitter > 0f) ? Random.Range(-firePitchJitter, firePitchJitter) : 0f;
        float pitch = Mathf.Clamp(firePitch + jitter, 0.1f, 3f);

        // pooled one-shot; ses namlu pozisyonundan gelir
        Vector3 pos = muzzle ? muzzle.position : transform.position;
        OneShotAudioPool.PlayClipAt(pos, fireClip, fireVolume, pitch);
    }
}
