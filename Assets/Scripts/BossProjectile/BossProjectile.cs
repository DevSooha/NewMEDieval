using UnityEngine;
using System;

public class BossProjectile : MonoBehaviour
{
    public float speed = 10.0f;
    public int damage = 1;

    public ElementType projectileElement = ElementType.Fire;

    private Action<BossProjectile> returnToPool;

    public void SetPoolCallback(Action<BossProjectile> callback)
    {
        returnToPool = callback;
    }

    // 자식이 덮어쓸 수 있도록 virtual 추가
    public virtual void Setup(ElementType element)
    {
        projectileElement = element;
        CancelInvoke(nameof(DestroyProjectile));
        Invoke(nameof(DestroyProjectile), 3.0f);
    }

    // 자식이 이동 방식을 바꿀 수 있도록 protected virtual 추가
    protected virtual void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    // 자식이 풀 반환 로직을 사용할 수 있도록 protected virtual 추가 (private -> protected)
    protected virtual void DestroyProjectile()
    {
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

    // 자식이 충돌 로직(구속 미니게임)을 바꿀 수 있도록 protected virtual 추가
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = other.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }
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