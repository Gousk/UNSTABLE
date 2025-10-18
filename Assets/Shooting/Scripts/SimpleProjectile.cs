using UnityEngine;

public class SimpleProjectile : MonoBehaviour
{
    float _damage, _speed, _life;
    LayerMask _mask;
    GameObject _hitVfx;

    float _impactImpulse = 0f;
    bool _scaleImpulseByDamage = true;

    bool _initialized;

    public void Initialize(float damage, float speed, LayerMask mask, float lifetime, GameObject hitVfx,
                           float impactImpulse, bool scaleImpulseByDamage)
    {
        _damage = damage;
        _speed = speed;
        _mask = mask;
        _life = lifetime;
        _hitVfx = hitVfx;
        _impactImpulse = impactImpulse;
        _scaleImpulseByDamage = scaleImpulseByDamage;

        _initialized = true;
        Destroy(gameObject, _life);
    }

    void Update()
    {
        if (!_initialized) return;

        float step = _speed * Time.deltaTime;
        Vector3 dir = transform.forward;

        if (Physics.Raycast(transform.position, dir, out RaycastHit hit, step + 0.05f, _mask, QueryTriggerInteraction.Ignore))
            Impact(hit);
        else
            transform.position += dir * step;
    }

    void Impact(RaycastHit hit)
    {
        // hasar
        var dmg = hit.collider.GetComponentInParent<IDamageable>(); 
        if (dmg != null)
        {
            dmg.TakeDamage(_damage, hit);
            HitConfirm.Raise(); // ✅ crosshair UI
        }


        // 🔹 fiziksel itme (silahdan gelen parametrelerle)
        var rb = hit.rigidbody ?? hit.collider.attachedRigidbody;
        if (rb != null && _impactImpulse > 0f)
        {
            Vector3 forceDir = -hit.normal;
            float impulse = _impactImpulse * (_scaleImpulseByDamage ? Mathf.Max(0.01f, _damage) : 1f);
            rb.AddForceAtPosition(forceDir * impulse, hit.point, ForceMode.Impulse);
        }

        if (_hitVfx) Instantiate(_hitVfx, hit.point, Quaternion.LookRotation(hit.normal));
        Destroy(gameObject);
    }
}
