using UnityEngine;

// BossProjectile을 상속받습니다.
public class AquaRay : BossProjectile
{
    [Header("광선 설정")]
    public float extendSpeed = 5f;
    public float maxLength = 10f;
    private float currentLength = 0f;

    private BoxCollider2D col;

    // 1. Setup 덮어쓰기: 초기화 및 물 속성 부여
    public override void Setup(ElementType element)
    {
        // 부모의 Setup을 호출하여 풀링 소멸 타이머(3초) 등을 그대로 작동시킵니다.
        // 이때 속성을 강제로 Water로 넘겨줍니다 (ElementType.Water가 있다고 가정).
        base.Setup(ElementType.Water);

        col = GetComponent<BoxCollider2D>();

        // 쏠 때마다 콜라이더 길이를 초기화
        currentLength = 0f;
        col.size = new Vector2(col.size.x, 0.1f);
        col.offset = Vector2.zero;

        // (필요하다면 여기서 파티클 시스템 Play)
    }

    // 2. Update 덮어쓰기: 이동 대신 콜라이더 늘리기
    protected override void Update()
    {
        // 주의: base.Update()를 호출하지 않습니다! (오브젝트가 앞으로 날아가는 것을 방지)

        if (currentLength < maxLength)
        {
            currentLength += extendSpeed * Time.deltaTime;
            currentLength = Mathf.Min(currentLength, maxLength);

            col.size = new Vector2(col.size.x, currentLength);
            col.offset = new Vector2(0, -currentLength / 2f); // 아래 방향으로 뻗음
        }
    }

    // 3. 충돌 로직 덮어쓰기: 닿아도 사라지지 않게 처리
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // 주의: base.OnTriggerEnter2D()를 호출하지 않습니다!

        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = other.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            // 핵심: 일반 투사체처럼 DestroyProjectile()을 호출하지 않으므로
            // 광선이 플레이어를 뚫고 계속 유지됩니다.
        }
        else if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
        {
            // 벽에 닿으면 뻗어나가는 것만 멈춤 (사라지진 않음)
            maxLength = currentLength;
        }
        else if (other.CompareTag("Grass"))
        {
            // 물 속성이므로 풀을 태우지 않거나, 물에 젖게 하는 로직을 넣을 수 있습니다.
        }
    }
}