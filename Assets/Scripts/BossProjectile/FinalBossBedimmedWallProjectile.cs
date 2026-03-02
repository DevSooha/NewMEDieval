using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class FinalBossBedimmedWallProjectile : BossProjectile
{
    private BoxCollider2D hitCollider;
    private bool isLaunched;
    private Camera cachedCamera;

    private void Awake()
    {
        hitCollider = GetComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
    }

    public void Launch(Vector2 moveDirection, float moveSpeed, Vector2 colliderSize, int hitDamage, ElementType elementType)
    {
        Vector2 direction = moveDirection.sqrMagnitude < 0.0001f ? Vector2.right : moveDirection.normalized;
        SetMoveDirection(direction);
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(1, hitDamage);
        hitCollider.size = new Vector2(Mathf.Max(0.01f, colliderSize.x), Mathf.Max(0.01f, colliderSize.y));
        Setup(elementType);

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }
    }

    public override void Setup(ElementType element)
    {
        projectileElement = element;
        isLaunched = true;
        CancelInvoke(nameof(DestroyProjectile));
    }

    protected override void Update()
    {
        if (!isLaunched) return;

        base.Update();

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null) return;

        Vector3 viewport = cachedCamera.WorldToViewportPoint(transform.position);
        bool outsideViewport = viewport.x < -0.3f || viewport.x > 1.3f || viewport.y < -0.3f || viewport.y > 1.3f;
        if (outsideViewport)
        {
            DestroyProjectile();
        }
    }

    protected override void DestroyProjectile()
    {
        CancelInvoke(nameof(DestroyProjectile));
        Destroy(gameObject);
    }

    public override void DespawnImmediate()
    {
        isLaunched = false;
        CancelInvoke(nameof(DestroyProjectile));
        Destroy(gameObject);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLaunched) return;
        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration
            );
            return;
        }

        if (other.CompareTag("Grass") && projectileElement == ElementType.Fire)
        {
            Destroy(other.gameObject);
        }
    }
}
