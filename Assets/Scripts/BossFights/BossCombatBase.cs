using System;
using System.Collections;
using UnityEngine;

// 이 스크립트는 아무데도 붙이지 마세요. 그냥 파일만 있으면 됩니다.
public abstract class BossCombatBase : MonoBehaviour
{
    [SerializeField, Min(0)] private int collisionContactDamage = 0;
    private const string PlayerTag = "Player";

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

