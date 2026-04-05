using UnityEngine;

/// <summary>
/// Pearl Beam 개별 빔의 데미지 판정을 담당하는 컴포넌트.
/// NemiPearlBeam에 의해 런타임에 추가된다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class PearlBeamHitbox : MonoBehaviour
{
    private int damage = 1;
    private float hitInterval = 0.25f;
    private bool isDamageActive;
    private float nextHitTime;
    private BoxCollider2D col;

    public void Initialize(int damagePerHit, float interval, Vector2 colliderSize)
    {
        damage = damagePerHit;
        hitInterval = interval;

        col = GetComponent<BoxCollider2D>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider2D>();

        col.isTrigger = true;
        col.size = colliderSize;

        // 콜라이더 오프셋: 빔이 아래에서 위로 올라가므로 중심을 위쪽으로 이동
        col.offset = new Vector2(0f, colliderSize.y / 2f);

        col.enabled = false;
    }

    public void SetDamageActive(bool active)
    {
        isDamageActive = active;

        if (col != null)
            col.enabled = active;

        if (active)
            nextHitTime = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other);

    private void TryHit(Collider2D other)
    {
        if (!isDamageActive) return;
        if (!other.CompareTag("Player")) return;
        if (Time.time < nextHitTime) return;

        nextHitTime = Time.time + hitInterval;
        BossHitResolver.TryApplyBossHit(other, damage, transform.position);
    }
}
