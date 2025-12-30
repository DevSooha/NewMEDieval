using UnityEngine;

public class FireBall : MonoBehaviour
{
    public float speed = 5.0f; // [cite: 30] 160px/sec = 5 tiles/sec (매칭됨)
    public int damage = 1;

    public ElementType bulletElement = ElementType.Fire;

    private Vector2 moveDir;

    // Setup 할 때 속성도 같이 받도록 수정
    public void Setup(Vector2 dir, ElementType element)
    {
        moveDir = dir.normalized;
        bulletElement = element;

        // 기획서상 화염벽 이동속도는 160px/sec(5칸) [cite: 30]
        // 화면 밖 이탈 시 삭제 (혹은 넉넉하게 5초) [cite: 33]
        Destroy(gameObject, 5.0f);
    }

    void Update()
    {
        transform.Translate(moveDir * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) return;

        // 1. 플레이어 충돌 시
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
        // 2. 풀(Grass) 충돌 시 
        else if (other.CompareTag("Grass"))
        {
            // (1) 불 속성이면 풀을 태움
            if (bulletElement == ElementType.Fire)
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