using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LatentThornHitbox : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float multiHitCooldown = 0.2f;
    [SerializeField] private bool drawHitboxGizmo = true;

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
            bool didDamage = BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position
            );

            if (didDamage)
            {
                StartCoroutine(DamageCooldownRoutine());
            }
        }

        if (TryGetElementFromAttacker(other, out ElementType attackerElement))
        {
            ApplyElementHit(attackerElement);
        }
    }

    private IEnumerator DamageCooldownRoutine()
    {
        canDamage = false;
        yield return new WaitForSeconds(multiHitCooldown);
        canDamage = true;
    }

    private void OnDrawGizmos()
    {
        DrawHitboxGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        DrawHitboxGizmo();
    }

    private void DrawHitboxGizmo()
    {
        if (!drawHitboxGizmo) return;

        Collider2D col = hitboxCollider != null ? hitboxCollider : GetComponent<Collider2D>();
        if (col == null) return;

        bool isActiveHitbox = col.enabled && gameObject.activeInHierarchy;
        Gizmos.color = isActiveHitbox ? new Color(1f, 0.2f, 0.2f, 0.85f) : new Color(1f, 0.75f, 0f, 0.65f);

        if (col is PolygonCollider2D poly)
        {
            Vector2[] points = poly.points;
            if (points == null || points.Length < 2) return;

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 a = poly.transform.TransformPoint(points[i] + poly.offset);
                Vector3 b = poly.transform.TransformPoint(points[(i + 1) % points.Length] + poly.offset);
                Gizmos.DrawLine(a, b);
            }
            return;
        }

        Bounds bounds = col.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    private static bool TryGetElementFromAttacker(Collider2D other, out ElementType element)
    {
        BossProjectile projectile = other.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            element = projectile.projectileElement;
            return true;
        }

        Bomb bomb = other.GetComponent<Bomb>();
        if (bomb != null)
        {
            element = bomb.bombElement;
            return true;
        }

        element = default;
        return false;
    }
}
