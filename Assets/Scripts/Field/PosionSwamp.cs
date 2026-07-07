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
                // QS-89: 전체 삭제는 플레이감 부적절(오너 판정) — 접촉 위치의 독 타일 1칸만 제거.
                // 탄막 위치는 소멸 전에 읽고, 소멸은 풀 반납 경로(DespawnImmediate) 유지.
                Vector3 contactPoint = other.bounds.center;
                projectile.DespawnImmediate();
                RemovePoisonTileAt(contactPoint);
            }
        }
    }

    private void RemovePoisonTileAt(Vector3 contactPoint)
    {
        if (poisonTilemap == null) return;

        Vector3Int tilePos = poisonTilemap.WorldToCell(contactPoint);
        poisonTilemap.SetTile(tilePos, null);
        Debug.Log($"독지대 타일 제거됨: {tilePos}");
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
