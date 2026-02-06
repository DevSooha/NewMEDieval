using UnityEngine;

public class PoisonZone : MonoBehaviour
{
    public float damageDelay = 2f;
    public int damageAmount = 1;

    private bool playerInside = false;
    private float timer = 0f;

    private PlayerHealth playerHealth;
    private SpriteRenderer playerSprite;   
    private Color originalColor;       

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
