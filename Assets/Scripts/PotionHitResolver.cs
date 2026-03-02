using System.Collections.Generic;
using UnityEngine;

public static class PotionHitResolver
{
    private const string GrassTag = "Grass";
    private const string ObstacleTag = "Obstacle";
    private const string WallTag = "Wall";
    private const string NpcTag = "NPC";
    private const string CampfireTag = "Campfire";

    private static readonly Dictionary<int, int> LastHitFrameByTarget = new Dictionary<int, int>();

    public static bool TryResolveHit(PotionProjectileController projectile, Collider2D other)
    {
        if (projectile == null || other == null) return false;

        PotionPhaseSpec spec = projectile.PhaseSpec;
        if (spec == null) return false;

        return TryResolveHitWithFrameGate(spec, other);
    }

    public static bool TryResolveAreaHit(PotionPhaseSpec spec, Collider2D other)
    {
        if (spec == null || other == null) return false;
        return TryResolveHitWithFrameGate(spec, other);
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
        ApplyPlayerHit(spec, health);
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

    private static bool TryResolveHitWithFrameGate(PotionPhaseSpec spec, Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            if (!ReserveFrameHit(playerHealth)) return false;
            ApplyPlayerHit(spec, playerHealth);
            return true;
        }

        EnemyCombat enemyCombat = other.GetComponent<EnemyCombat>();
        if (enemyCombat == null) enemyCombat = other.GetComponentInParent<EnemyCombat>();
        if (enemyCombat != null)
        {
            if (!ReserveFrameHit(enemyCombat)) return false;
            ApplyEnemyHit(spec, enemyCombat);
            return true;
        }

        BossHealth bossHealth = other.GetComponent<BossHealth>();
        if (bossHealth == null) bossHealth = other.GetComponentInParent<BossHealth>();
        if (bossHealth != null)
        {
            if (!ReserveFrameHit(bossHealth)) return false;
            ApplyBossHit(spec, bossHealth);
            return true;
        }

        return false;
    }

    private static void ApplyPlayerHit(PotionPhaseSpec spec, PlayerHealth health)
    {
        if (health == null) return;

        if (spec.healsPlayerOnSelfHit)
        {
            health.HealWithOvercap(1, 3);
        }
        else if (!spec.ignoreSelfHitPenalty)
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
        for (int i = 0; i < spec.onEnemyHitEffects.Count; i++)
        {
            StatusEffectSpec fx = spec.onEnemyHitEffects[i];
            if (fx == null) continue;

            switch (fx.effectType)
            {
                case StatusEffectType.HealEnemyCurrentHpPercent:
                {
                    int healAmount = Mathf.RoundToInt(enemy.CurrentHealth * (fx.magnitude / 100f));
                    enemy.Heal(healAmount);
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

    private static void ApplyEffectsToBoss(PotionPhaseSpec spec, BossHealth boss)
    {
        EnemyStatusController status = boss.GetComponent<EnemyStatusController>();
        if (status == null)
        {
            status = boss.GetComponentInParent<EnemyStatusController>();
        }

        for (int i = 0; i < spec.onEnemyHitEffects.Count; i++)
        {
            StatusEffectSpec fx = spec.onEnemyHitEffects[i];
            if (fx == null) continue;

            switch (fx.effectType)
            {
                case StatusEffectType.HealEnemyCurrentHpPercent:
                {
                    int healAmount = Mathf.RoundToInt(boss.currentHP * (fx.magnitude / 100f));
                    boss.Heal(healAmount);
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

    private static bool ReserveFrameHit(Object target)
    {
        if (target == null)
        {
            return false;
        }

        int id = target.GetInstanceID();
        int frame = Time.frameCount;

        if (LastHitFrameByTarget.TryGetValue(id, out int lastFrame) && lastFrame == frame)
        {
            return false;
        }

        if (LastHitFrameByTarget.Count > 8192)
        {
            LastHitFrameByTarget.Clear();
        }

        LastHitFrameByTarget[id] = frame;
        return true;
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
