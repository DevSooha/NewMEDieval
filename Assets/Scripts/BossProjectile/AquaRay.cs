using UnityEngine;

public class AquaRay : BossProjectile
{
    [Header("Beam Settings")]
    public float extendSpeed = 5f;
    public float maxLength = 10f;

    private float currentLength;
    // 벽 명중 시 길이를 줄일 때 직렬화 필드(maxLength) 대신 이 값을 쓴다 —
    // 풀 재사용 인스턴스가 이전 발사에서 짧아진 길이를 물려받는 오염 방지 (ElectricLaserRay와 동일 패턴).
    private float runtimeMaxLength;
    private BoxCollider2D col;

    public override void Setup(ElementType element)
    {
        // Keep pooled projectile lifecycle behavior from base class.
        base.Setup(ElementType.Water);

        col = GetComponent<BoxCollider2D>();

        currentLength = 0f;
        runtimeMaxLength = maxLength;
        if (col != null)
        {
            col.size = new Vector2(col.size.x, 0.1f);
            col.offset = Vector2.zero;
        }
    }

    protected override void Update()
    {
        if (col == null) return;

        if (currentLength < runtimeMaxLength)
        {
            currentLength += extendSpeed * Time.deltaTime;
            currentLength = Mathf.Min(currentLength, runtimeMaxLength);

            col.size = new Vector2(col.size.x, currentLength);
            col.offset = new Vector2(0f, -currentLength / 2f);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // This beam should not be destroyed on hit.
        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration,
                applyKnockbackOnHit,
                -(Vector2)transform.up
            );
        }
        else if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
        {
            runtimeMaxLength = currentLength;
        }
    }
}
