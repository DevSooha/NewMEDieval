using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.Progress;

public class EnemyCombat : MonoBehaviour
{
    public float attackRange = 0.8f;
    public float knockbackForce = 20f;
    public float stunTime = 0.2f;
    public int damageAmount = 1;
    private LayerMask playerLayer;

    public int maxHealth = 200;
    private int currentHealth;

    private float lastDamageTime;

    private Rigidbody2D rb;

    public GameObject worldItemPrefab;
    public ItemData pastelbloomItemData;
    public int dropAmount = 1;
    public float dropChance = 1f;

    private bool isDead = false;



    void Start()
    {
        playerLayer = LayerMask.GetMask("Player");
        rb= GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;

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
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, playerLayer);

        if (hits.Length > 0)
        {
            hits[0].GetComponent<PlayerHealth>().TakeDamage(damageAmount);
            hits[0].GetComponent<Player>().KnockBack(transform, knockbackForce, stunTime);
        }
        if (hits.Length == 0)
        {
            Debug.LogWarning("No hits detected! Check layer settings and attack range.");
        }
    }
    private void OnDrawGizmos()
    {
        Vector2 attackPos = transform.position;
        
        // 공격 범위를 빨간 원으로 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPos, attackRange);
        
        // 중심점 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackPos, 0.1f);
    }

    public void EnemyTakeDamage(int damage)
    {
        if (isDead) return;

        if(Time.time - lastDamageTime < 0.1f)
        {
            return; // 최근에 데미지를 받았다면 무시
        }
        lastDamageTime = Time.time;
        currentHealth -= damage;

        Debug.Log($"Enemy took {damage} damage. Current health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
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

        //WorldItem이 확실히 있는지 체크 (안전장치)
        if (wi != null)
        {
            wi.Init(dropAmount);

            SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = pastelbloomItemData.icon;
                sr.sortingLayerName = "Item";
            }
        }
    }
}
