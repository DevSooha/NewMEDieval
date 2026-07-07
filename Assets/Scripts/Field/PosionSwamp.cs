using UnityEngine;
using UnityEngine.Tilemaps;

public class PoisonZone : MonoBehaviour
{
    public float damageDelay = 2f;
    public int damageAmount = 1;

    public Tilemap poisonTilemap;
    private bool playerInside = false;
    private float timer = 0f;

    private PlayerHealth playerHealth;
    private SpriteRenderer playerSprite;   
    private Color originalColor;       

    void Start()
    {
        if (poisonTilemap == null)
        {
            poisonTilemap = GetComponent<Tilemap>();
        }
    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
            timer = 0f;

            playerHealth = other.GetComponent<PlayerHealth>();

            playerSprite = other.GetComponent<SpriteRenderer>();
            if (playerSprite != null)
            {
                originalColor = playerSprite.color;              
                playerSprite.color = Color.magenta;             
            }
        }
        else
        {
            // QS-79: 물 탄막 프리팹이 Untagged라 태그 게이트로는 영원히 도달 불가 —
            // 태그 대신 BossProjectile 컴포넌트로 직접 판별한다.
            BossProjectile projectile = other.GetComponent<BossProjectile>();
            if (projectile != null && projectile.projectileElement == ElementType.Water)
            {
                // v0.4 §7-7: 물 속성 기술 1대에 독지대 전체 파괴 + 닿은 탄막 소멸.
                // 소멸은 풀 반납 경로(DespawnImmediate) 사용 — 직접 Destroy는 풀링과 충돌.
                projectile.DespawnImmediate();
                DestroySwamp();
            }
        }
    }

    private void DestroySwamp()
    {
        // 콜라이더가 함께 꺼지면 OnTriggerExit2D가 보장되지 않으므로
        // 플레이어 변색/상태를 여기서 직접 복구한다.
        if (playerSprite != null)
        {
            playerSprite.color = originalColor;
            playerSprite = null;
        }
        playerInside = false;
        playerHealth = null;
        timer = 0f;

        // poisonTilemap이 다른 오브젝트에 배선된 경우도 타일이 남지 않게 정리
        if (poisonTilemap != null && poisonTilemap.gameObject != gameObject)
        {
            poisonTilemap.ClearAllTiles();
        }

        Debug.Log("독지대 파괴됨 (물 속성 피격)");
        gameObject.SetActive(false);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
            timer = 0f;
            playerHealth = null;

            if (playerSprite != null)
            {
                playerSprite.color = originalColor;
                playerSprite = null;
            }
        }
    }

    void Update()
    {
        if (!playerInside || playerHealth == null) return;

        timer += Time.deltaTime;

        if (timer >= damageDelay)
        {
            playerHealth.TakeDamage(damageAmount);
            if (playerSprite != null)
            {
                playerSprite.color = Color.magenta;
            }
            timer = 0f;
        }
    }
}
