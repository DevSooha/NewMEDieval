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
        else if (other.CompareTag("Bullets"))
        {
            BossProjectile projectile = other.GetComponent<BossProjectile>();
            if (projectile != null && projectile.projectileElement == ElementType.Water)
            {
                Vector3 contactPoint = other.bounds.center;
                Vector3Int tilePos = poisonTilemap.WorldToCell(contactPoint);
                
                poisonTilemap.SetTile(tilePos, null);
                poisonTilemap.RefreshAllTiles();
                Debug.Log($"독지대 타일 제거됨: {tilePos}");

                Destroy(other.gameObject);
            }
        }
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

        if (playerInside)
        {
        if (timer >= damageDelay)
        {
            playerHealth.TakeDamage(damageAmount);
            playerSprite.color = Color.darkRed;
            playerSprite.color = Color.magenta;
            timer = 0f;
        }
        }
    }
}
