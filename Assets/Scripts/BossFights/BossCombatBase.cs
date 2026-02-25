using System;
using UnityEngine;

// 이 스크립트는 아무데도 붙이지 마세요. 그냥 파일만 있으면 됩니다.
public abstract class BossCombatBase : MonoBehaviour
{
    [SerializeField, Min(0)] private int collisionContactDamage = 0;

    // 근접 보스는 공격 타이밍 피격만 사용하고 싶을 때 override로 false 설정.
    // Legacy toggle name kept for compatibility with existing boss overrides.
    // This now gates collision contact damage instead of granting free invulnerability.
    protected virtual bool UseCollisionInvulnerability => true;

    public abstract void StartBattle();

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Player")) return;
        HandlePlayerCollision(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
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

