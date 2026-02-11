using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LatentThornHitbox : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float multiHitCooldown = 0.2f;

    private Collider2D hitboxCollider;
    private bool canDamage = true;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;
    }

    public void ActivateForSeconds(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(ActivationRoutine(duration));
    }

    public void ResetState()
    {
        StopAllCoroutines();
        canDamage = true;
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        gameObject.SetActive(true);
    }

    public void ApplyElementHit(ElementType attackElement)
    {
        if (attackElement == ElementType.Light)
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator ActivationRoutine(float duration)
    {
        gameObject.SetActive(true);
        canDamage = true;

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
        }

        yield return new WaitForSeconds(duration);

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canDamage) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null) health = other.GetComponentInParent<PlayerHealth>();

            if (health != null)
            {
                health.TakeDamage(damage);
                StartCoroutine(DamageCooldownRoutine());
            }
        }

        BossProjectile projectile = other.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            ApplyElementHit(projectile.projectileElement);
        }

        Bomb bomb = other.GetComponent<Bomb>();
        if (bomb != null)
        {
            ApplyElementHit(bomb.bombElement);
        }
    }

    private IEnumerator DamageCooldownRoutine()
    {
        canDamage = false;
        yield return new WaitForSeconds(multiHitCooldown);
        canDamage = true;
    }
}
