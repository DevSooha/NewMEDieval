using System.Collections;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LatentThornHitbox : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float multiHitCooldown = 0.2f;
    [SerializeField] private float durationScale = 1f;
    [SerializeField] private float additionalDuration = 0f;
    [SerializeField] private float baseWidth = 1f;
    [SerializeField] private float maxHeight = 2.5f;
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField] private bool drawHitboxGizmo = true;

    private Collider2D hitboxCollider;
    private PolygonCollider2D polygonCollider;
    private bool canDamage = true;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        polygonCollider = hitboxCollider as PolygonCollider2D;
        hitboxCollider.isTrigger = true;
        hitboxCollider.enabled = false;
        ApplyConfiguredShape();
    }

    public void ActivateForSeconds(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(ActivationRoutine(GetAdjustedDuration(duration)));
    }

    public void ResetState()
    {
        StopAllCoroutines();
        canDamage = true;
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        ApplyConfiguredShape();
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
        ApplyConfiguredShape();

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
        TryDamagePlayer(other);
    }

    private float GetAdjustedDuration(float requestedDuration)
    {
        float scaledDuration = Mathf.Max(0f, requestedDuration) * Mathf.Max(0f, durationScale);
        return Mathf.Max(0.01f, scaledDuration + additionalDuration);
    }

    private void ApplyConfiguredShape()
    {
        if (polygonCollider != null)
        {
            float halfWidth = Mathf.Max(0.01f, baseWidth) * 0.5f;
            float height = Mathf.Max(0.01f, maxHeight);
            float bottomY = verticalOffset;
            float topY = verticalOffset + height;
            polygonCollider.points = new[]
            {
                new Vector2(-halfWidth, bottomY),
                new Vector2(halfWidth, bottomY),
                new Vector2(0f, topY)
            };
            return;
        }

        if (hitboxCollider is BoxCollider2D box)
        {
            Vector2 size = box.size;
            size.y = Mathf.Max(0.01f, maxHeight);
            box.size = size;
            Vector2 offset = box.offset;
            offset.y = verticalOffset + size.y * 0.5f;
            box.offset = offset;
            return;
        }

        if (hitboxCollider is CapsuleCollider2D capsule)
        {
            Vector2 size = capsule.size;
            size.y = Mathf.Max(0.01f, maxHeight);
            capsule.size = size;
            Vector2 offset = capsule.offset;
            offset.y = verticalOffset + size.y * 0.5f;
            capsule.offset = offset;
        }
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
