using UnityEngine;

public class AquaRay : BossProjectile
{
    [Header("Beam Settings")]
    public float extendSpeed = 5f;
    public float maxLength = 10f;

    private float currentLength;
    private BoxCollider2D col;

    public override void Setup(ElementType element)
    {
        // Keep pooled projectile lifecycle behavior from base class.
        base.Setup(ElementType.Water);

        col = GetComponent<BoxCollider2D>();

        currentLength = 0f;
        if (col != null)
        {
            col.size = new Vector2(col.size.x, 0.1f);
            col.offset = Vector2.zero;
        }
    }

    protected override void Update()
    {
        if (col == null) return;

        if (currentLength < maxLength)
        {
            currentLength += extendSpeed * Time.deltaTime;
            currentLength = Mathf.Min(currentLength, maxLength);

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
            maxLength = currentLength;
        }
    }
}
