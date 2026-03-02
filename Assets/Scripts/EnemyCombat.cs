using System.Collections;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    public float attackRange = 0.8f;
    public float knockbackTileSize = 1f;
    public float knockbackTiles = 1f;
    public float knockbackDuration = 0.2f;
    public int damageAmount = 1;
    private LayerMask playerLayer;

    [Header("Melee Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);
    [SerializeField] private float windupTime = 0f;
    [SerializeField] private float activeTime = 0f;
    [SerializeField] private float recoveryTime = 0f;
    [SerializeField] private float attackOffset = 1f;

    public int maxHealth = 200;
    private int currentHealth;

    private float lastDamageTime;

    private Rigidbody2D rb;

    public GameObject worldItemPrefab;
    public ItemData pastelbloomItemData;
    public int dropAmount = 1;
    public float dropChance = 1f;

    private bool isDead = false;

    private float lastAttackTime;
    public float combatCooldown = 1f;
    private bool isAttacking = false;

    private Vector2 attackDirection = Vector2.left;

    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;

    void Start()
    {
        playerLayer = LayerMask.GetMask("Player");
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;

        if (attackPoint == null)
        {
            attackPoint = transform.Find("AttackPoint");
        }

        if (attackPoint != null)
        {
            Vector2 local = attackPoint.localPosition;
            if (local.sqrMagnitude > 0.001f)
            {
                attackDirection = GetCardinal(local);
                if (attackOffset <= 0f) attackOffset = local.magnitude;
            }
        }

        if (attackOffset <= 0f) attackOffset = 1f;

        Debug.Log($"EnemyCombat initialized. Player layer mask: {playerLayer.value}");
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }
        Attack();
    }

    public void Attack()
    {
        TryAttack();
    }

    public bool TryAttack()
    {
        if (isAttacking) return false;
        if (Time.time < lastAttackTime + combatCooldown) return false;
        StartCoroutine(AttackRoutine());
        return true;
    }

    public bool IsAttacking => isAttacking;

    public void SetAttackDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f) return;
        attackDirection = GetCardinal(direction);
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;

        if (windupTime > 0f)
        {
            yield return new WaitForSeconds(windupTime);
        }

        ApplyAttackHit();

        if (activeTime > 0f)
        {
            yield return new WaitForSeconds(activeTime);
        }

        if (recoveryTime > 0f)
        {
            yield return new WaitForSeconds(recoveryTime);
        }

        lastAttackTime = Time.time;
        isAttacking = false;
    }

    private void ApplyAttackHit()
    {
        Vector2 center = (Vector2)transform.position + (attackDirection * attackOffset);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, attackBoxSize, 0f, playerLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger) continue;

            Player playerScript = hit.GetComponent<Player>();

            if (playerScript != null && hit.gameObject.activeInHierarchy)
            {
                hit.GetComponent<PlayerHealth>()?.TakeDamage(damageAmount);
                playerScript.KnockBackByDistance(((Vector2)(hit.transform.position - transform.position)).normalized, knockbackTileSize * knockbackTiles, knockbackDuration);
                return;
            }
        }
    }

    private Vector2 GetCardinal(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            return new Vector2(Mathf.Sign(direction.x), 0f);
        }
        return new Vector2(0f, Mathf.Sign(direction.y));
    }

    private void OnDrawGizmos()
    {
        if (attackPoint == null)
        {
            Transform found = transform.Find("AttackPoint");
            if (found != null) attackPoint = found;
        }

        Vector2 center = (Vector2)transform.position + (attackDirection * attackOffset);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, attackBoxSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, 0.1f);
    }

    public void EnemyTakeDamage(int damage)
    {
        if (isDead) return;

        if (Time.time - lastDamageTime < 0.1f)
        {
            return;
        }
        lastDamageTime = Time.time;
        currentHealth -= damage;

        Debug.Log($"Enemy took {damage} damage. Current health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
    }

    private void Die()
    {
        Debug.Log("Enemy died.");
        isDead = true;
        DropItem();
        Destroy(gameObject);
    }

    void DropItem()
    {
        if (Random.value > dropChance) return;

        Vector3 dropPos =
            transform.position + (Vector3)Random.insideUnitCircle.normalized * 0.5f;

        GameObject item = Instantiate(
            worldItemPrefab,
            dropPos,
            Quaternion.identity
        );

        WorldItem wi = item.GetComponent<WorldItem>();

        if (wi != null)
        {
            wi.Init(pastelbloomItemData, dropAmount);
            item.GetComponent<SpriteRenderer>().sprite = pastelbloomItemData.icon;
        }
    }
}
