using System;
using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    public float speed = 10.0f;
    public int damage = 1;

    [Header("Hit Reaction")]
    [SerializeField] protected bool applyKnockbackOnHit = true;
    [SerializeField] protected float knockbackDistance = 1f;
    [SerializeField] protected float knockbackDuration = 0.2f;

    public ElementType projectileElement = ElementType.Fire;

    private Action<BossProjectile> returnToPool;
    private bool useCustomDirection;
    private Vector2 customDirection = Vector2.right;
    private bool isDespawning;

    public void SetPoolCallback(Action<BossProjectile> callback)
    {
        returnToPool = callback;
    }

    public virtual void Setup(ElementType element)
    {
        isDespawning = false;
        useCustomDirection = false;
        customDirection = Vector2.right;
        projectileElement = element;
        CancelInvoke(nameof(DestroyProjectile));
        Invoke(nameof(DestroyProjectile), 3.0f);
    }

    protected virtual void Update()
    {
        Vector2 moveDir = useCustomDirection ? customDirection : (Vector2)transform.right;
        transform.Translate(moveDir * speed * Time.deltaTime, Space.World);
    }

    public void SetMoveDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            useCustomDirection = false;
            customDirection = Vector2.right;
            return;
        }

        useCustomDirection = true;
        customDirection = direction.normalized;
    }

    protected virtual void DestroyProjectile()
    {
        if (isDespawning)
        {
            return;
        }

        isDespawning = true;
        CancelInvoke(nameof(DestroyProjectile));

        if (returnToPool != null)
        {
            returnToPool.Invoke(this);
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public virtual void DespawnImmediate()
    {
        // Already inactive pooled objects are already returned and should not be re-enqueued.
        if (!gameObject.activeInHierarchy && returnToPool != null)
        {
            return;
        }

        DestroyProjectile();
    }

    protected virtual void OnDisable()
    {
        CancelInvoke(nameof(DestroyProjectile));
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            Vector2 fallbackDirection = useCustomDirection ? customDirection : (Vector2)transform.right;
            BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration,
                applyKnockbackOnHit,
                fallbackDirection
            );

            DestroyProjectile();
        }
        else if (other.CompareTag("Grass"))
        {
            if (projectileElement == ElementType.Fire) Destroy(other.gameObject);
            DestroyProjectile();
        }
        else if (other.CompareTag("Obstacle"))
        {
            DestroyProjectile();
        }
    }
}
