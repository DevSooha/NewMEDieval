using UnityEngine;

public static class BossHitResolver
{
    public const float DefaultKnockbackDistance = 1f;
    public const float DefaultKnockbackDuration = 0.2f;

    public static bool TryApplyBossHit(
        Collider2D playerCollider,
        int damage,
        Vector2 attackerPosition,
        float knockbackDistance = DefaultKnockbackDistance,
        float knockbackDuration = DefaultKnockbackDuration,
        bool applyKnockback = true,
        Vector2 fallbackDirection = default)
    {
        if (playerCollider == null)
        {
            return false;
        }

        return TryApplyBossHit(
            playerCollider.transform,
            damage,
            attackerPosition,
            knockbackDistance,
            knockbackDuration,
            applyKnockback,
            fallbackDirection
        );
    }

    public static bool TryApplyBossHit(
        Transform playerTransform,
        int damage,
        Vector2 attackerPosition,
        float knockbackDistance = DefaultKnockbackDistance,
        float knockbackDuration = DefaultKnockbackDuration,
        bool applyKnockback = true,
        Vector2 fallbackDirection = default)
    {
        if (playerTransform == null || damage <= 0)
        {
            return false;
        }

        PlayerHealth health = playerTransform.GetComponent<PlayerHealth>();
        if (health == null) health = playerTransform.GetComponentInParent<PlayerHealth>();
        if (health == null) return false;

        if (!health.gameObject.activeInHierarchy || health.CurrentHP <= 0 || health.IsInvulnerable)
        {
            return false;
        }

        bool didDamage = health.TryTakeDamage(
            damage,
            health.BossHitInvulnerableDuration
        );
        if (!didDamage)
        {
            return false;
        }

        if (!applyKnockback || health.CurrentHP <= 0)
        {
            return true;
        }

        Player player = health.GetComponent<Player>();
        if (player == null) player = health.GetComponentInParent<Player>();
        if (player == null || !player.gameObject.activeInHierarchy)
        {
            return true;
        }

        PlayerStatusController status = player.GetComponent<PlayerStatusController>();
        if (status != null && status.IsKnockbackImmune)
        {
            return true;
        }

        Vector2 knockbackDirection = (Vector2)player.transform.position - attackerPosition;
        if (knockbackDirection.sqrMagnitude <= 0.0001f)
        {
            knockbackDirection = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector2.right;
        }

        player.KnockBackByDistance(
            knockbackDirection.normalized,
            Mathf.Max(0f, knockbackDistance),
            Mathf.Max(0.01f, knockbackDuration)
        );

        return true;
    }
}
