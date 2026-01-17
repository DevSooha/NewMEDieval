using UnityEngine;

public class BossProjectile : MonoBehaviour
{
    public float speed = 10.0f;
    public int damage = 1;

    public ElementType projectileElement = ElementType.Fire;

    public void Setup(ElementType element)
    {
        projectileElement = element;
        Destroy(gameObject, 3.0f);
    }

    void Update()
    {
        transform.Translate(Vector2.right * speed * Time.deltaTime);
    }

    // BossProjectile.cs

    // BossProjectile.cs

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) return;

        // 1. 플레이어 충돌 시
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[충돌 감지] 부딪힌 녀석: {other.name}"); // 1. 충돌은 되는지 확인

            // [핵심 수정] 부모 오브젝트까지 뒤져서 스크립트 찾기!
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

            if (playerHealth == null)
            {
                playerHealth = other.GetComponentInParent<PlayerHealth>();
            }

            // 찾았는지 검사
            if (playerHealth != null)
            {
                Debug.Log(">> 체력 깎기 함수 호출!");
                playerHealth.TakeDamage(damage);
            }
            else
            {
                Debug.LogError(">> 비상! Player 태그는 있는데 PlayerHealth 스크립트를 못 찾겠음!!");
            }

            Destroy(gameObject); // 맞았으니 사라짐
        }
        // 2. 풀(Grass) 충돌 시 
        else if (other.CompareTag("Grass"))
        {
            if (projectileElement == ElementType.Fire)
            {
                Destroy(other.gameObject);
            }
            Destroy(gameObject);
        }
        // 3. 벽 충돌 시
        else if (other.CompareTag("Obstacle"))
        {
            Destroy(gameObject);
        }
    }
}
