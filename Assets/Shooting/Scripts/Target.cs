using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class Target : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public bool destroyOnDeath = true;
    public float destroyDelay = 0f;

    [Header("Events (optional)")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    [Header("VFX")]
    public GameObject deathVfx;

    [Header("SFX")]
    [Tooltip("Target hasar aldığında çalınacak ses.")]
    public AudioClip onHitClip;

    [Tooltip("Target öldüğünde çalınacak ses.")]
    public AudioClip onDeathClip;

    [Range(0f, 2f)] public float sfxVolume = 1f;
    [Range(0.5f, 2f)] public float sfxPitch = 1f;
    [Range(0f, 0.5f)] public float sfxPitchJitter = 0.05f;

    // runtime
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    DamageScaleFeedback[] _scaleFx;
    DamageColorFlash[] _colorFx;

    void Awake()
    {
        CurrentHealth = maxHealth;
        _scaleFx = GetComponentsInChildren<DamageScaleFeedback>(true);
        _colorFx = GetComponentsInChildren<DamageColorFlash>(true);
    }

    public void TakeDamage(float amount, RaycastHit hitInfo)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth -= amount;

        // FEEDBACK: scale + color + sfx
        if (_scaleFx != null) foreach (var fx in _scaleFx) fx.Play();
        if (_colorFx != null) foreach (var fx in _colorFx) fx.Play();

        // 🔊 HASAR SESİ
        if (onHitClip)
        {
            float jitter = Random.Range(-sfxPitchJitter, sfxPitchJitter);
            OneShotAudioPool.PlayClipAt(transform.position, onHitClip, sfxVolume, sfxPitch + jitter);
        }

        onDamaged?.Invoke();

        if (CurrentHealth <= 0f) Die(hitInfo);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    void Die(RaycastHit hitInfo)
    {
        if (IsDead) return;
        IsDead = true;

        onDeath?.Invoke();

        if (deathVfx) Instantiate(deathVfx, hitInfo.point, Quaternion.LookRotation(hitInfo.normal));

        // 🔊 ÖLÜM SESİ
        if (onDeathClip)
        {
            float jitter = Random.Range(-sfxPitchJitter, sfxPitchJitter);
            OneShotAudioPool.PlayClipAt(transform.position, onDeathClip, sfxVolume, sfxPitch + jitter);
        }

        if (destroyOnDeath)
        {
            if (destroyDelay <= 0f) Destroy(gameObject);
            else Destroy(gameObject, destroyDelay);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        if (Application.isPlaying && !IsDead)
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
    }
#endif
}
