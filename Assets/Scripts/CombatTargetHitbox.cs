using UnityEngine;

public class CombatTargetHitbox : MonoBehaviour
{
    private const string HitboxChildName = "CombatHitbox";
    private const float DefaultSizeScale = 0.6f;

    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private EnemyCombat enemyCombat;

    public PlayerHealth PlayerHealth => playerHealth;
    public EnemyCombat EnemyCombat => enemyCombat;

    public static CombatTargetHitbox EnsureForPlayer(PlayerHealth owner)
    {
        return Ensure(owner != null ? owner.transform : null, owner, null);
    }

    public static CombatTargetHitbox EnsureForEnemy(EnemyCombat owner)
    {
        return Ensure(owner != null ? owner.transform : null, null, owner);
    }

    public static bool TryGetPlayerHealth(Collider2D other, out PlayerHealth health)
    {
        health = null;
        if (other == null) return false;

        CombatTargetHitbox hitbox = other.GetComponent<CombatTargetHitbox>();
        if (hitbox != null && hitbox.playerHealth != null)
        {
            health = hitbox.playerHealth;
            return true;
        }

        // Fallback: 투사체가 Player 본체 콜라이더와 직접 충돌한 경우
        if (other.CompareTag("Player"))
        {
            health = other.GetComponent<PlayerHealth>();
            if (health == null) health = other.GetComponentInParent<PlayerHealth>();
            return health != null;
        }

        return false;
    }

    // ── BUG-5: 정밀 히트박스 우선 판정 복구 ─────────────────────────────────
    // CombatHitbox 자식은 Untagged라 CompareTag("Player") 게이트를 통과하지 못해,
    // 보스 투사체가 항상 본체 캡슐(더 큰 판정)에 맞고 있었다.
    // 아래 두 헬퍼로 "히트박스가 있으면 히트박스만, 없으면 본체 폴백"을 복구한다.

    /// <summary>플레이어 명중 후보 콜라이더인지 (정밀 히트박스 또는 본체).</summary>
    public static bool IsPlayerHitCandidate(Collider2D other)
    {
        if (other == null) return false;

        CombatTargetHitbox hitbox = other.GetComponent<CombatTargetHitbox>();
        if (hitbox != null) return hitbox.playerHealth != null;

        return other.CompareTag("Player");
    }

    /// <summary>
    /// 본체 콜라이더 명중을 정밀 히트박스에 양보해야 하는지.
    /// 활성 CombatHitbox 자식이 있으면 true (본체 명중 무시).
    /// 히트박스가 없거나 비활성이면 false (본체 판정 폴백).
    /// </summary>
    public static bool ShouldDeferToPreciseHitbox(Collider2D other)
    {
        if (other == null) return false;
        if (other.GetComponent<CombatTargetHitbox>() != null) return false; // 이미 히트박스

        Transform precise = other.transform.Find(HitboxChildName);
        return precise != null && precise.gameObject.activeInHierarchy;
    }

    public static bool TryGetEnemyCombat(Collider2D other, out EnemyCombat enemy)
    {
        enemy = null;
        CombatTargetHitbox hitbox = other != null ? other.GetComponent<CombatTargetHitbox>() : null;
        if (hitbox == null || hitbox.enemyCombat == null)
        {
            return false;
        }

        enemy = hitbox.enemyCombat;
        return true;
    }

    private static CombatTargetHitbox Ensure(Transform owner, PlayerHealth player, EnemyCombat enemy)
    {
        if (owner == null)
        {
            return null;
        }

        Transform existing = owner.Find(HitboxChildName);
        GameObject hitboxObject;
        if (existing != null)
        {
            hitboxObject = existing.gameObject;
        }
        else
        {
            hitboxObject = new GameObject(HitboxChildName);
            hitboxObject.transform.SetParent(owner, false);
        }

        hitboxObject.layer = owner.gameObject.layer;

        CombatTargetHitbox hitbox = hitboxObject.GetComponent<CombatTargetHitbox>();
        if (hitbox == null)
        {
            hitbox = hitboxObject.AddComponent<CombatTargetHitbox>();
        }

        hitbox.playerHealth = player;
        hitbox.enemyCombat = enemy;
        hitbox.ConfigureCollider(owner);
        return hitbox;
    }

    private void ConfigureCollider(Transform owner)
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider2D>();
        }

        box.isTrigger = true;

        Collider2D source = FindSourceCollider(owner);
        if (source is CapsuleCollider2D capsule)
        {
            box.offset = capsule.offset;
            box.size = ClampSize(capsule.size * DefaultSizeScale);
            return;
        }

        if (source is BoxCollider2D sourceBox)
        {
            box.offset = sourceBox.offset;
            box.size = ClampSize(sourceBox.size * DefaultSizeScale);
            return;
        }

        if (source is CircleCollider2D circle)
        {
            box.offset = circle.offset;
            float diameter = circle.radius * 2f * DefaultSizeScale;
            box.size = ClampSize(new Vector2(diameter, diameter));
            return;
        }

        box.offset = Vector2.zero;
        box.size = new Vector2(0.4f, 0.4f);
    }

    private static Collider2D FindSourceCollider(Transform owner)
    {
        Collider2D[] colliders = owner.GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null || !col.enabled)
            {
                continue;
            }

            return col;
        }

        return null;
    }

    private static Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(
            Mathf.Max(0.1f, size.x),
            Mathf.Max(0.1f, size.y));
    }
}
