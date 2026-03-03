using System.Collections;
using System.Reflection;
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

    public void DespawnImmediate()
    {
        StopAllCoroutines();
        canDamage = false;

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        gameObject.SetActive(false);
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
        TryApplyElementHit(other);
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyElementHit(other);
    }

    private void TryApplyElementHit(Collider2D other)
    {
        if (TryGetElementFromAttacker(other, out ElementType attackerElement))
        {
            ApplyElementHit(attackerElement);
        }
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (!canDamage) return;
        if (other == null || !other.CompareTag("Player")) return;

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
        if (other == null)
        {
            element = default;
            return false;
        }

        BossProjectile projectile = other.GetComponentInParent<BossProjectile>();
        if (projectile != null)
        {
            element = projectile.projectileElement;
            return true;
        }

        Bomb bomb = other.GetComponentInParent<Bomb>();
        if (bomb != null)
        {
            element = bomb.bombElement;
            return true;
        }

        // Fallback for custom attack hitboxes:
        // If a collider (or its parent) has an ElementType field/property,
        // read it by common member names.
        if (TryReadElementViaReflection(other, out element))
        {
            return true;
        }

        element = default;
        return false;
    }

    private static bool TryReadElementViaReflection(Collider2D other, out ElementType element)
    {
        string[] memberNames =
        {
            "attackElement",
            "elementType",
            "element",
            "projectileElement",
            "bombElement",
            "currentElement"
        };

        Component[] components = other.GetComponentsInParent<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component comp = components[i];
            if (comp == null) continue;

            System.Type type = comp.GetType();
            for (int n = 0; n < memberNames.Length; n++)
            {
                string memberName = memberNames[n];

                FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(ElementType))
                {
                    element = (ElementType)field.GetValue(comp);
                    return true;
                }

                PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(ElementType) && property.CanRead)
                {
                    element = (ElementType)property.GetValue(comp);
                    return true;
                }
            }
        }

        element = default;
        return false;
    }
}
