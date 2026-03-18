using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// 이 스크립트는 아무데도 붙이지 마세요. 파일만 존재하면 됩니다.
public abstract class BossCombatBase : MonoBehaviour
{
    protected enum BossOffensiveCleanupReason
    {
        BossDead,
        BossDisabled,
        BattleReset
    }

    private sealed class TrackedOffensive
    {
        public GameObject GameObject;
        public bool IsVisualOnly;
    }

    [SerializeField, Min(0)] private int collisionContactDamage = 0;
    private const string PlayerTag = "Player";
    private readonly Dictionary<GameObject, TrackedOffensive> trackedOffensives = new();
    private bool isCleaningUpOffensives;
    private Animator[] cachedBossAnimators;

    [Header("Default Knockback Settings")]
    [SerializeField] protected float defaultKnockbackForce = 8f;
    [SerializeField] protected float defaultKnockbackStunTime = 0.2f;

    // Legacy toggle name kept for compatibility with existing boss overrides.
    // This now gates collision contact damage instead of granting free invulnerability.
    protected virtual bool UseCollisionInvulnerability => true;

    public abstract void StartBattle();

    private void OnDisable()
    {
        CleanupOffensivesOnDisable();
    }

    public void NotifyBossDefeatedCleanup()
    {
        CleanupBossOffensives(BossOffensiveCleanupReason.BossDead);
    }

    protected void CleanupOffensivesOnDisable()
    {
        CleanupBossOffensives(BossOffensiveCleanupReason.BossDisabled);
    }

    protected void CleanupOffensivesOnBattleReset()
    {
        CleanupBossOffensives(BossOffensiveCleanupReason.BattleReset);
    }

    protected void RegisterPlayerDeathBaseHandler(Action handler)
    {
        PlayerHealth.OnPlayerDeath -= handler;
        PlayerHealth.OnPlayerDeath += handler;
    }

    protected void UnregisterPlayerDeathBaseHandler(Action handler)
    {
        PlayerHealth.OnPlayerDeath -= handler;
    }

    protected void CleanupBossPresentationOnPlayerDeath()
    {
        CleanupOffensivesOnBattleReset();
        FreezeBossAnimators();
    }

    protected void ResumeBossPresentation()
    {
        Animator[] animators = GetBossAnimators();
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null) continue;
            animator.speed = 1f;
        }
    }

    protected void RegisterBossOffensive(GameObject offensive, bool isVisualOnly = false)
    {
        if (offensive == null || offensive == gameObject)
        {
            return;
        }

        if (trackedOffensives.Count >= 32 && trackedOffensives.Count % 16 == 0)
        {
            CleanupNullTrackedOffensives();
        }

        trackedOffensives[offensive] = new TrackedOffensive
        {
            GameObject = offensive,
            IsVisualOnly = isVisualOnly
        };
    }

    protected void UnregisterBossOffensive(GameObject offensive)
    {
        if (offensive == null)
        {
            return;
        }

        trackedOffensives.Remove(offensive);
    }

    protected void CleanupBossOffensives(BossOffensiveCleanupReason reason)
    {
        _ = reason;

        if (isCleaningUpOffensives || trackedOffensives.Count == 0)
        {
            return;
        }

        isCleaningUpOffensives = true;

        try
        {
            List<TrackedOffensive> snapshot = new(trackedOffensives.Values);
            foreach (TrackedOffensive offensive in snapshot)
            {
                if (offensive == null || offensive.GameObject == null)
                {
                    continue;
                }

                CleanupTrackedOffensive(offensive, reason);
            }
        }
        finally
        {
            CleanupNullTrackedOffensives();
            isCleaningUpOffensives = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(PlayerTag)) return;
        HandlePlayerCollision(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(PlayerTag)) return;
        HandlePlayerCollision(other);
    }

    private void HandlePlayerCollision(Collider2D playerCollider)
    {
        if (!UseCollisionInvulnerability) return;
        if (collisionContactDamage <= 0) return;

        BossHitResolver.TryApplyBossHit(
            playerCollider,
            collisionContactDamage,
            transform.position
        );
    }

    private void CleanupNullTrackedOffensives()
    {
        List<GameObject> keysToRemove = null;
        foreach (KeyValuePair<GameObject, TrackedOffensive> pair in trackedOffensives)
        {
            if (pair.Key != null && pair.Value != null && pair.Value.GameObject != null)
            {
                continue;
            }

            keysToRemove ??= new List<GameObject>();
            keysToRemove.Add(pair.Key);
        }

        if (keysToRemove == null)
        {
            return;
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            trackedOffensives.Remove(keysToRemove[i]);
        }
    }

    private static void CleanupTrackedOffensive(TrackedOffensive tracked, BossOffensiveCleanupReason reason)
    {
        GameObject offensive = tracked != null ? tracked.GameObject : null;
        if (offensive == null)
        {
            return;
        }

        if (tracked.IsVisualOnly)
        {
            CleanupVisualOnlyOffensive(offensive, reason);
            return;
        }

        BossProjectile projectile = offensive.GetComponent<BossProjectile>();
        if (projectile != null)
        {
            if (projectile.gameObject.activeInHierarchy)
            {
                projectile.DespawnImmediate();
            }

            return;
        }

        StainedSwordProjectile stainedSword = offensive.GetComponent<StainedSwordProjectile>();
        if (stainedSword != null)
        {
            if (stainedSword.gameObject.activeInHierarchy)
            {
                stainedSword.DespawnImmediate();
            }

            return;
        }

        LatentThornHitbox thorn = offensive.GetComponent<LatentThornHitbox>();
        if (thorn != null)
        {
            thorn.DespawnImmediate();
            return;
        }

        if (offensive.scene.IsValid())
        {
            UnityEngine.Object.Destroy(offensive);
        }
    }

    private static void CleanupVisualOnlyOffensive(GameObject offensive, BossOffensiveCleanupReason reason)
    {
        if (offensive == null)
        {
            return;
        }

        if (reason == BossOffensiveCleanupReason.BossDead)
        {
            if (offensive.scene.IsValid())
            {
                UnityEngine.Object.Destroy(offensive);
            }

            return;
        }

        PlayableDirector director = offensive.GetComponent<PlayableDirector>();
        if (director != null)
        {
            director.Stop();
            director.time = 0;
        }

        ParticleSystem[] particleSystems = offensive.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null) continue;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (offensive.activeSelf)
        {
            offensive.SetActive(false);
        }
    }

    protected void Knockback(Player player, Transform sender, float? forceOverride = null, float? stunOverride = null)
    {
        if (player == null || sender == null) return;

        PlayerStatusController status = player.GetComponent<PlayerStatusController>();
        if (status != null && status.IsKnockbackImmune) return;

        float force = forceOverride ?? defaultKnockbackForce;
        float stun = stunOverride ?? defaultKnockbackStunTime;
        player.KnockBack(sender, force, stun);
    }

    protected bool TryResolvePlayerTransform(ref Transform cachedPlayerTransform)
    {
        if (cachedPlayerTransform != null)
        {
            return true;
        }

        if (Player.Instance != null)
        {
            cachedPlayerTransform = Player.Instance.transform;
            return true;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
        if (playerObject != null)
        {
            cachedPlayerTransform = playerObject.transform;
        }

        return cachedPlayerTransform != null;
    }

    protected bool TryResolvePlayer(ref Player cachedPlayer)
    {
        if (cachedPlayer != null)
        {
            return true;
        }

        if (Player.Instance != null)
        {
            cachedPlayer = Player.Instance;
            return true;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(PlayerTag);
        if (playerObject != null)
        {
            cachedPlayer = playerObject.GetComponent<Player>();
        }

        return cachedPlayer != null;
    }

    private void FreezeBossAnimators()
    {
        Animator[] animators = GetBossAnimators();
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null) continue;
            animator.speed = 0f;
        }
    }

    private Animator[] GetBossAnimators()
    {
        if (cachedBossAnimators == null || cachedBossAnimators.Length == 0)
        {
            cachedBossAnimators = GetComponentsInChildren<Animator>(true);
        }

        return cachedBossAnimators ?? Array.Empty<Animator>();
    }

    protected IEnumerator FadeSpriteAlpha(SpriteRenderer targetRenderer, float duration, float fromAlpha = 0f, float toAlpha = 1f)
    {
        float safeDuration = Mathf.Max(0f, duration);
        float timer = 0f;

        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = safeDuration <= 0f ? 1f : timer / safeDuration;
            SetSpriteAlpha(targetRenderer, Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetSpriteAlpha(targetRenderer, toAlpha);
    }

    private static void SetSpriteAlpha(SpriteRenderer targetRenderer, float alpha)
    {
        if (targetRenderer == null)
        {
            return;
        }

        Color color = targetRenderer.color;
        color.a = alpha;
        targetRenderer.color = color;
    }
}

public interface IBossDamageModifier
{
    float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier);
}

public interface IBossPhaseHandler
{
    void OnBossHpChanged(int currentHp, int maxHp);
}

public interface IBossBattleResetNotifier
{
    event Action OnBattleReset;
}

public interface IBossStartPositioner
{
    void SetToPointAImmediate();
}
