using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStatusController : MonoBehaviour
{
    private EnemyCombat enemyCombat;
    private EnemyMovement enemyMovement;
    private BossHealth bossHealth;
    private BossCombatBase bossCombat;
    private Rigidbody2D rb;
    private Animator[] animators = System.Array.Empty<Animator>();

    private readonly Dictionary<StatusEffectType, Coroutine> running = new();
    private readonly Dictionary<StatusEffectType, float> effectEndTimes = new();
    private readonly Dictionary<StatusEffectType, float> effectMagnitudes = new();
    private readonly Dictionary<StatusEffectType, float> effectIntervals = new();

    private bool stunned;
    private float speedMultiplier = 1f;
    private bool bossCombatWasEnabledBeforeStun;

    public bool IsStunned => stunned;
    public float SpeedMultiplier => speedMultiplier;

    private void Awake()
    {
        enemyCombat = GetComponent<EnemyCombat>();
        if (enemyCombat == null) enemyCombat = GetComponentInParent<EnemyCombat>();

        enemyMovement = GetComponent<EnemyMovement>();
        if (enemyMovement == null) enemyMovement = GetComponentInParent<EnemyMovement>();

        bossHealth = GetComponent<BossHealth>();
        if (bossHealth == null) bossHealth = GetComponentInParent<BossHealth>();

        bossCombat = GetComponent<BossCombatBase>();
        if (bossCombat == null) bossCombat = GetComponentInParent<BossCombatBase>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();

        animators = GetComponentsInChildren<Animator>(true);
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<StatusEffectType, Coroutine> pair in running)
        {
            if (pair.Value != null)
            {
                StopCoroutine(pair.Value);
            }
        }

        running.Clear();
        effectEndTimes.Clear();
        effectMagnitudes.Clear();
        effectIntervals.Clear();
        stunned = false;
        speedMultiplier = 1f;
        RestoreBossCombatState();
        SetAnimatorSpeed(1f);
    }

    public void ApplyEffect(StatusEffectSpec effect)
    {
        if (effect == null) return;

        switch (effect.effectType)
        {
            case StatusEffectType.EnemyStun:
                RefreshEffect(effect.effectType, effect.duration);
                StartIfNeeded(effect.effectType, StunRoutine());
                break;
            case StatusEffectType.EnemyMoveSpeedMultiplier:
                RefreshEffect(effect.effectType, effect.duration, effect.magnitude <= 0f ? 1f : effect.magnitude);
                StartIfNeeded(effect.effectType, SpeedRoutine());
                break;
            case StatusEffectType.EnemyKnockback:
                ApplyKnockback(effect.magnitude);
                break;
            case StatusEffectType.PoisonDot:
                RefreshEffect(effect.effectType, effect.duration, effect.magnitude, effect.interval);
                StartIfNeeded(effect.effectType, PoisonRoutine());
                break;
        }
    }

    public void ApplyHitKnockback(Vector2 sourcePosition, float distanceUnits, float durationSeconds)
    {
        if (distanceUnits <= 0f)
        {
            return;
        }

        Vector2 direction = ((Vector2)transform.position - sourcePosition);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.right;
        }

        ApplyKnockback(distanceUnits * 64f, direction.normalized);
    }

    private void RefreshEffect(StatusEffectType type, float duration, float magnitude = 0f, float interval = 0f)
    {
        effectEndTimes[type] = Time.time + Mathf.Max(0.05f, duration);
        effectMagnitudes[type] = magnitude;
        effectIntervals[type] = interval;
    }

    private void StartIfNeeded(StatusEffectType type, IEnumerator routine)
    {
        if (running.TryGetValue(type, out Coroutine existing) && existing != null)
        {
            return;
        }

        running[type] = StartCoroutine(routine);
    }

    private bool IsEffectActive(StatusEffectType type)
    {
        return effectEndTimes.TryGetValue(type, out float endTime) && Time.time < endTime;
    }

    private float GetEffectMagnitude(StatusEffectType type, float fallback = 0f)
    {
        return effectMagnitudes.TryGetValue(type, out float magnitude) ? magnitude : fallback;
    }

    private float GetEffectInterval(StatusEffectType type, float fallback = 0f)
    {
        return effectIntervals.TryGetValue(type, out float interval) ? interval : fallback;
    }

    private void ClearEffect(StatusEffectType type)
    {
        running.Remove(type);
        effectEndTimes.Remove(type);
        effectMagnitudes.Remove(type);
        effectIntervals.Remove(type);
    }

    private IEnumerator StunRoutine()
    {
        stunned = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (bossCombat != null)
        {
            bossCombatWasEnabledBeforeStun = bossCombat.enabled;
            bossCombat.enabled = false;
        }

        while (IsEffectActive(StatusEffectType.EnemyStun))
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            yield return null;
        }

        stunned = false;
        RestoreBossCombatState();
        ClearEffect(StatusEffectType.EnemyStun);
    }

    private IEnumerator SpeedRoutine()
    {
        while (IsEffectActive(StatusEffectType.EnemyMoveSpeedMultiplier))
        {
            speedMultiplier = Mathf.Max(0.1f, GetEffectMagnitude(StatusEffectType.EnemyMoveSpeedMultiplier, 1f));
            SetAnimatorSpeed(speedMultiplier);
            yield return null;
        }

        speedMultiplier = 1f;
        SetAnimatorSpeed(1f);
        ClearEffect(StatusEffectType.EnemyMoveSpeedMultiplier);
    }

    private IEnumerator PoisonRoutine()
    {
        while (IsEffectActive(StatusEffectType.PoisonDot))
        {
            float tick = Mathf.Max(0.1f, GetEffectInterval(StatusEffectType.PoisonDot, 2f));
            float percent = Mathf.Max(0f, GetEffectMagnitude(StatusEffectType.PoisonDot));

            if (enemyCombat != null && !enemyCombat.IsDead)
            {
                int damage = Mathf.RoundToInt(enemyCombat.CurrentHealth * (percent / 100f));
                if (damage > 0)
                {
                    enemyCombat.EnemyTakeDamage(damage);
                }
            }
            else if (bossHealth != null && bossHealth.CurrentHP > 0)
            {
                int damage = Mathf.RoundToInt(bossHealth.CurrentHP * (percent / 100f));
                if (damage > 0)
                {
                    bossHealth.TakeDamage(damage, ElementType.Poison);
                }
            }
            else
            {
                break;
            }

            yield return new WaitForSeconds(tick);
        }

        ClearEffect(StatusEffectType.PoisonDot);
    }

    private void ApplyKnockback(float distancePixels, Vector2? overrideDirection = null)
    {
        float distanceUnits = distancePixels / 64f;
        if (distanceUnits <= 0f)
        {
            return;
        }

        Vector2 dir = overrideDirection ?? Vector2.right;
        if (!overrideDirection.HasValue && Player.Instance != null)
        {
            dir = ((Vector2)transform.position - (Vector2)Player.Instance.transform.position).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        }

        if (rb != null)
        {
            rb.MovePosition(ResolveKnockbackDestination(rb, rb.position, dir, distanceUnits));
            return;
        }

        transform.position = ResolveKnockbackDestination(null, transform.position, dir, distanceUnits);
    }

    // ── BUG-4: 넉백 이동 클램프 ──────────────────────────────────────────────
    // 기존 넉백은 rb.MovePosition으로 목표 지점까지 한 번에 이동해 경로상 충돌을
    // 해소하지 않았고, 문/벽 근처에서 몬스터가 방 밖으로 빠져나갔다.
    // Player.KnockBackDistanceRoutine의 rb.Cast 패턴을 준용해, 몸이 실제로
    // 부딪히는 지점 직전까지만 이동 거리를 줄인다 (방향/거리 체감은 유지).
    // EnemyCombat.ApplySelfKnockback도 이 헬퍼를 공유한다.

    private const float knockbackSkinWidth = 0.02f;
    private const float roomEdgeMargin = 0.4f;
    private static readonly RaycastHit2D[] knockbackCastHits = new RaycastHit2D[8];

    public static Vector2 ResolveKnockbackDestination(Rigidbody2D body, Vector2 origin, Vector2 direction, float distanceUnits)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        Vector2 dir = direction.normalized;
        float distance = Mathf.Max(0f, distanceUnits);

        if (body != null && distance > 0f)
        {
            // 충돌 매트릭스 기준으로 몸이 실제 부딪히는 대상만 검사 (트리거 제외)
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Physics2D.GetLayerCollisionMask(body.gameObject.layer),
                useTriggers = false,
            };

            int hitCount = body.Cast(dir, filter, knockbackCastHits, distance);
            for (int i = 0; i < hitCount; i++)
            {
                distance = Mathf.Min(distance, Mathf.Max(0f, knockbackCastHits[i].distance - knockbackSkinWidth));
            }
        }

        return ClampToRoomCell(origin, origin + dir * distance);
    }

    // 넉백 목적지를 출발 지점이 속한 방(그리드 셀)의 플레이 가능 영역 안으로 제한한다.
    // 문 통로는 트리거 콜라이더라 Cast에 걸리지 않으므로, 이 경계 클램프가
    // 문틈으로 방 밖까지 밀려나는 것을 막는다. RoomManager가 없는 씬은 그대로 통과.
    // BUG-3: EnemyMovement의 걷기 이탈 방지에서도 같은 규칙을 재사용한다.
    public static Vector2 ClampToRoomCell(Vector2 origin, Vector2 destination)
    {
        RoomManager rm = RoomManager.Instance;
        if (rm == null || rm.gridWidth <= 0f || rm.gridHeight <= 0f) return destination;

        float cellCenterX = Mathf.Round(origin.x / rm.gridWidth) * rm.gridWidth;
        float cellCenterY = Mathf.Round(origin.y / rm.gridHeight) * rm.gridHeight;
        float halfWidth = Mathf.Max(0f, rm.playableWidth * 0.5f - roomEdgeMargin);
        float halfHeight = Mathf.Max(0f, rm.playableHeight * 0.5f - roomEdgeMargin);

        destination.x = Mathf.Clamp(destination.x, cellCenterX - halfWidth, cellCenterX + halfWidth);
        destination.y = Mathf.Clamp(destination.y, cellCenterY - halfHeight, cellCenterY + halfHeight);
        return destination;
    }

    private void SetAnimatorSpeed(float multiplier)
    {
        if (animators == null)
        {
            return;
        }

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
            {
                continue;
            }

            animator.speed = multiplier;
        }
    }

    private void RestoreBossCombatState()
    {
        if (bossCombat != null && bossCombatWasEnabledBeforeStun)
        {
            bossCombat.enabled = true;
        }

        bossCombatWasEnabledBeforeStun = false;
    }
}
