using System.Collections.Generic;
using UnityEngine;

public static class PotionHitResolver
{
    private const string GrassTag = "Grass";
    private const string ObstacleTag = "Obstacle";
    private const string WallTag = "Wall";
    private const string NpcTag = "NPC";
    private const string CampfireTag = "Campfire";

    private static readonly Dictionary<ulong, int> LastHitFrameByPair = new Dictionary<ulong, int>();

    public static bool TryResolveHit(PotionProjectileController projectile, Collider2D other)
    {
        if (projectile == null || other == null) return false;

        PotionPhaseSpec spec = projectile.PhaseSpec;
        if (spec == null) return false;

        return TryResolveHitWithFrameGate(
            spec,
            other,
            projectile.Owner,
            projectile.GetInstanceID(),
            allowSelfHitRules: true);
    }

    public static bool TryResolveAreaHit(PotionPhaseSpec spec, Collider2D other, int sourceInstanceId = 0)
    {
        if (spec == null || other == null) return false;
        return TryResolveHitWithFrameGate(
            spec,
            other,
            null,
            sourceInstanceId,
            allowSelfHitRules: false);
    }

    public static bool TryResolveEnvironmentHit(PotionProjectileController projectile, Collider2D other)
    {
        if (projectile == null || other == null)
        {
            return false;
        }

        if (IsOwnerCollider(projectile, other))
        {
            return false;
        }

        if (IsProjectileOrHazardCollider(other))
        {
            return false;
        }

        if (IsCombatTargetCollider(other))
        {
            return false;
        }

        if (other.CompareTag(GrassTag))
        {
            if (projectile.PhaseSpec != null && projectile.PhaseSpec.primaryElement == ElementType.Fire)
            {
                DestroyColliderOwner(other);
            }

            return true;
        }

        if (IsNpcCollider(other))
        {
            return true;
        }

        if (other.CompareTag(ObstacleTag) || other.CompareTag(WallTag) || other.CompareTag(CampfireTag))
        {
            return true;
        }

        return false;
    }

    public static void ApplySpecToPlayer(PotionPhaseSpec spec, PlayerHealth health)
    {
        if (spec == null || health == null) return;
        ApplyPlayerHit(spec, health, isSelfHit: false);
    }

    public static void ApplySpecToEnemy(PotionPhaseSpec spec, EnemyCombat enemy)
    {
        if (spec == null || enemy == null) return;
        ApplyEnemyHit(spec, enemy);
    }

    public static void ApplySpecToBoss(PotionPhaseSpec spec, BossHealth boss)
    {
        if (spec == null || boss == null) return;
        ApplyBossHit(spec, boss);
    }

    private static bool TryResolveHitWithFrameGate(
        PotionPhaseSpec spec,
        Collider2D other,
        Transform owner,
        int sourceInstanceId,
        bool allowSelfHitRules)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            bool isSelfHit = allowSelfHitRules && IsOwnerTarget(owner, playerHealth.transform);
            if (!isSelfHit && spec.damageTarget == DamageTargetType.EnemyOnly)
            {
                return false;
            }

            if (!ReserveFrameHit(sourceInstanceId, playerHealth)) return false;
            ApplyPlayerHit(spec, playerHealth, isSelfHit);
            return true;
        }

        EnemyCombat enemyCombat = other.GetComponent<EnemyCombat>();
        if (enemyCombat == null) enemyCombat = other.GetComponentInParent<EnemyCombat>();
        if (enemyCombat != null)
        {
            if (!ReserveFrameHit(sourceInstanceId, enemyCombat)) return false;
            ApplyEnemyHit(spec, enemyCombat);
            return true;
        }

        BossHealth bossHealth = other.GetComponent<BossHealth>();
        if (bossHealth == null) bossHealth = other.GetComponentInParent<BossHealth>();
        if (bossHealth != null)
        {
            if (!ReserveFrameHit(sourceInstanceId, bossHealth)) return false;
            ApplyBossHit(spec, bossHealth);
            return true;
        }

        return false;
    }

    private static void ApplyPlayerHit(PotionPhaseSpec spec, PlayerHealth health, bool isSelfHit)
    {
        if (health == null) return;

        if (isSelfHit && spec.healsPlayerOnSelfHit)
        {
            health.HealWithOvercap(1, 3);
        }
        else if (isSelfHit && !spec.ignoreSelfHitPenalty)
        {
            health.TakeDamage(1);
        }

        PlayerStatusController status = GetOrAdd<PlayerStatusController>(health.gameObject);
        ApplyEffectsToPlayer(spec, health, status);
    }

    private static void ApplyEnemyHit(PotionPhaseSpec spec, EnemyCombat enemy)
    {
        if (enemy == null) return;
        if (spec.damageTarget == DamageTargetType.PlayerOnly) return;

        if (spec.baseDamage > 0)
        {
            enemy.EnemyTakeDamage(spec.baseDamage);
        }

        EnemyStatusController status = GetOrAdd<EnemyStatusController>(enemy.gameObject);
        ApplyEffectsToEnemy(spec, enemy, status);
    }

    private static void ApplyBossHit(PotionPhaseSpec spec, BossHealth boss)
    {
        if (boss == null) return;
        if (spec.damageTarget == DamageTargetType.PlayerOnly) return;

        if (spec.baseDamage > 0)
        {
            float primaryMultiplier = ElementManager.GetDamageMultiplier(spec.primaryElement, boss.currentElement);
            if (Mathf.Abs(primaryMultiplier) < 0.0001f)
            {
                primaryMultiplier = 1f;
            }

            float combinedMultiplier = ElementManager.GetCombinedDamageMultiplier(spec.primaryElement, spec.subElement, boss.currentElement);
            float subAdjustedScale = combinedMultiplier / primaryMultiplier;
            int preAdjustedDamage = Mathf.RoundToInt(spec.baseDamage * subAdjustedScale);
            boss.TakeDamage(Mathf.Max(1, preAdjustedDamage), spec.primaryElement);
        }

        ApplyEffectsToBoss(spec, boss);
    }

    private static void ApplyEffectsToPlayer(PotionPhaseSpec spec, PlayerHealth health, PlayerStatusController status)
    {
        for (int i = 0; i < spec.onPlayerHitEffects.Count; i++)
        {
            StatusEffectSpec fx = spec.onPlayerHitEffects[i];
            if (fx == null) continue;

            switch (fx.effectType)
            {
                case StatusEffectType.HealPlayerFlat:
                    health.HealWithOvercap(Mathf.Max(1, Mathf.RoundToInt(fx.magnitude)), 3);
                    break;
                default:
                    if (status != null)
                    {
                        status.ApplyEffect(fx);
                    }
                    break;
            }
        }
    }

    private static void ApplyEffectsToEnemy(PotionPhaseSpec spec, EnemyCombat enemy, EnemyStatusController status)
    {
        ApplyEnemyLikeEffects(
            spec.onEnemyHitEffects,
            () => enemy.CurrentHealth,
            enemy.Heal,
            status);
    }

    private static void ApplyEffectsToBoss(PotionPhaseSpec spec, BossHealth boss)
    {
        EnemyStatusController status = boss.GetComponent<EnemyStatusController>();
        if (status == null)
        {
            status = boss.GetComponentInParent<EnemyStatusController>();
        }

        ApplyEnemyLikeEffects(
            spec.onEnemyHitEffects,
            () => boss.currentHP,
            boss.Heal,
            status);
    }

    private static void ApplyEnemyLikeEffects(
        List<StatusEffectSpec> effects,
        System.Func<int> currentHpGetter,
        System.Action<int> heal,
        EnemyStatusController status)
    {
        if (effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffectSpec fx = effects[i];
            if (fx == null) continue;

            switch (fx.effectType)
            {
                case StatusEffectType.HealEnemyCurrentHpPercent:
                {
                    int currentHp = currentHpGetter != null ? currentHpGetter() : 0;
                    int healAmount = Mathf.RoundToInt(currentHp * (fx.magnitude / 100f));
                    heal?.Invoke(healAmount);
                    break;
                }
                default:
                    if (status != null)
                    {
                        status.ApplyEffect(fx);
                    }
                    break;
            }
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        if (target == null) return null;

        T comp = target.GetComponent<T>();
        if (comp == null)
        {
            comp = target.AddComponent<T>();
        }

        return comp;
    }

    private static bool ReserveFrameHit(int sourceInstanceId, Object target)
    {
        if (target == null)
        {
            return false;
        }

        int targetId = target.GetInstanceID();
        ulong hitKey = ComposeHitKey(sourceInstanceId, targetId);
        int frame = Time.frameCount;

        if (LastHitFrameByPair.TryGetValue(hitKey, out int lastFrame) && lastFrame == frame)
        {
            return false;
        }

        if (LastHitFrameByPair.Count > 8192)
        {
            LastHitFrameByPair.Clear();
        }

        LastHitFrameByPair[hitKey] = frame;
        return true;
    }

    private static ulong ComposeHitKey(int sourceInstanceId, int targetInstanceId)
    {
        unchecked
        {
            return ((ulong)(uint)sourceInstanceId << 32) | (uint)targetInstanceId;
        }
    }

    private static bool IsOwnerTarget(Transform owner, Transform target)
    {
        if (owner == null || target == null)
        {
            return false;
        }

        return target == owner || target.IsChildOf(owner) || owner.IsChildOf(target);
    }

    private static bool IsOwnerCollider(PotionProjectileController projectile, Collider2D other)
    {
        Transform owner = projectile.Owner;
        if (owner == null)
        {
            return false;
        }

        Transform target = other.transform;
        return target == owner || target.IsChildOf(owner) || owner.IsChildOf(target);
    }

    private static bool IsProjectileOrHazardCollider(Collider2D other)
    {
        if (other.GetComponent<PotionProjectileController>() != null) return true;
        if (other.GetComponentInParent<PotionProjectileController>() != null) return true;
        if (other.GetComponent<PotionAreaHazard>() != null) return true;
        if (other.GetComponentInParent<PotionAreaHazard>() != null) return true;
        return false;
    }

    private static bool IsCombatTargetCollider(Collider2D other)
    {
        if (other.GetComponent<PlayerHealth>() != null || other.GetComponentInParent<PlayerHealth>() != null) return true;
        if (other.GetComponent<EnemyCombat>() != null || other.GetComponentInParent<EnemyCombat>() != null) return true;
        if (other.GetComponent<BossHealth>() != null || other.GetComponentInParent<BossHealth>() != null) return true;
        return false;
    }

    private static bool IsNpcCollider(Collider2D other)
    {
        if (other.CompareTag(NpcTag)) return true;
        if (other.GetComponent<NPC>() != null) return true;
        if (other.GetComponentInParent<NPC>() != null) return true;
        return false;
    }

    private static void DestroyColliderOwner(Collider2D other)
    {
        GameObject target = other.attachedRigidbody != null
            ? other.attachedRigidbody.gameObject
            : other.gameObject;
        Object.Destroy(target);
    }
}
