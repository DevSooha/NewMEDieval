using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CarmaExcisionTrueHitbox : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float multiHitCooldown = 0.2f;

    private BoxCollider2D triggerCollider;
    private bool canDamage = true;
    private Vector2 attackerPosition;

    private void Awake()
    {
        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = false;
    }

    public void Activate(Vector2 worldCenter, Vector2 worldSize, float duration, Vector2 attackerWorldPosition)
    {
        StopAllCoroutines();
        transform.position = worldCenter;
        triggerCollider.size = new Vector2(Mathf.Max(0.01f, worldSize.x), Mathf.Max(0.01f, worldSize.y));
        attackerPosition = attackerWorldPosition;
        StartCoroutine(ActivationRoutine(Mathf.Max(0.01f, duration)));
    }

    public void DeactivateImmediate()
    {
        StopAllCoroutines();
        canDamage = false;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    private IEnumerator ActivationRoutine(float duration)
    {
        canDamage = true;
        triggerCollider.enabled = true;
        yield return new WaitForSeconds(duration);
        triggerCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (!canDamage || !triggerCollider.enabled) return;
        if (!other.CompareTag("Player")) return;

        bool didDamage = BossHitResolver.TryApplyBossHit(
            other,
            damage,
            attackerPosition
        );

        if (!didDamage) return;
        StartCoroutine(DamageCooldownRoutine());
    }

    private IEnumerator DamageCooldownRoutine()
    {
        canDamage = false;
        yield return new WaitForSeconds(Mathf.Max(0.01f, multiHitCooldown));
        canDamage = true;
    }
}
