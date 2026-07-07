using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// 이 스크립트는 아무데도 붙이지 마세요. 파일만 존재하면 됩니다.

/// <summary>
/// 모든 보스 전투 클래스의 베이스.
/// 공격 오브젝트 추적/일괄 정리, 접촉 데미지, 넉백, 플레이어 사망 시 애니메이터 정지를 담당한다.
/// 새 보스는 OnPlayerDied()를 override해 사망 리셋 로직을 넣는다 (OnEnable/OnDisable 구독은 베이스가 관리).
/// </summary>
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

    // 추적 목록이 이 개수를 넘으면 주기적으로 파괴된 항목을 청소한다 (등록마다 전수 검사하면 낭비)
    private const int TrackedCleanupThreshold = 32;
    private const int TrackedCleanupInterval = 16;

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

    protected virtual void OnEnable()
    {
        // 중복 구독 방지를 위해 해제 후 재구독
        PlayerHealth.OnPlayerDeath -= OnPlayerDied;
        PlayerHealth.OnPlayerDeath += OnPlayerDied;
    }

    protected virtual void OnDisable()
    {
        PlayerHealth.OnPlayerDeath -= OnPlayerDied;
        CleanupOffensivesOnDisable();
    }

    /// <summary>플레이어 사망 시 호출된다. 보스별 리셋 로직은 여기를 override한다.</summary>
    protected virtual void OnPlayerDied() { }

    /// <summary>보스 사망 확정 시 외부(BossHealth 등)가 호출해 추적 중인 공격을 모두 파괴한다.</summary>
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

    /// <summary>플레이어 사망 연출용: 공격 정리 + 보스 애니메이터 정지(freeze).</summary>
    protected void CleanupBossPresentationOnPlayerDeath()
    {
        CleanupOffensivesOnBattleReset();
        FreezeBossAnimators();
    }

    /// <summary>freeze했던 보스 애니메이터를 다시 재생한다 (재도전 시작 시).</summary>
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

    /// <summary>
    /// 보스가 생성한 공격 오브젝트를 추적 목록에 등록한다.
    /// 등록된 오브젝트는 보스 사망/비활성/전투 리셋 시 자동 정리된다.
    /// </summary>
    /// <param name="offensive">추적할 공격 오브젝트</param>
    /// <param name="isVisualOnly">true면 데미지 없는 연출 전용 — 리셋 시 파괴 대신 정지/비활성 처리</param>
    protected void RegisterBossOffensive(GameObject offensive, bool isVisualOnly = false)
    {
        if (offensive == null || offensive == gameObject)
        {
            return;
        }

        if (trackedOffensives.Count >= TrackedCleanupThreshold && trackedOffensives.Count % TrackedCleanupInterval == 0)
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
        // 정리 도중 Despawn 콜백이 다시 정리를 트리거하는 재진입을 차단
        if (isCleaningUpOffensives || trackedOffensives.Count == 0)
        {
            return;
        }

        isCleaningUpOffensives = true;

        try
        {
            // Despawn 과정에서 trackedOffensives가 변형될 수 있으므로 스냅샷 순회
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

        // 풀링되는 타입은 Destroy 대신 각자의 DespawnImmediate(풀 반환)를 호출해야 한다.
        // 타입별 활성 상태 검사 조건이 달라 인터페이스 통합 대신 명시적 분기를 유지한다.
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

        // 보스가 진짜로 죽은 경우에만 파괴하고, 리셋/비활성은 재사용을 위해 정지만 한다
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

    /// <summary>넉백 면역 상태를 확인한 뒤 플레이어를 밀어낸다.</summary>
    protected void Knockback(Player player, Transform sender, float? forceOverride = null, float? stunOverride = null)
    {
        if (player == null || sender == null) return;

        PlayerStatusController status = player.GetComponent<PlayerStatusController>();
        if (status != null && status.IsKnockbackImmune) return;

        float force = forceOverride ?? defaultKnockbackForce;
        float stun = stunOverride ?? defaultKnockbackStunTime;
        player.KnockBack(sender, force, stun);
    }

    /// <summary>플레이어 Transform을 캐시 우선으로 찾는다. Player.Instance -> 태그 검색 순.</summary>
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

    /// <summary>Player 컴포넌트를 캐시 우선으로 찾는다. Player.Instance -> 태그 검색 순.</summary>
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

    /// <summary>스프라이트 알파를 duration 동안 fromAlpha에서 toAlpha로 보간한다 (등장/퇴장 연출 공용).</summary>
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
