using UnityEditor;
using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    public float attackRange = 0.8f;
    public float knockbackForce = 20f;
    public float stunTime = 0.2f;
    public int damageAmount = 1;
    private LayerMask playerLayer;
    void Start()
    {
        playerLayer = LayerMask.GetMask("Player");
        
        Debug.Log($"EnemyCombat initialized. Player layer mask: {playerLayer.value}");
    }

    void Update()
    {

    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            collision.gameObject.GetComponent<PlayerHealth>().TakeDamage(damageAmount);
            Attack();
<<<<<<< HEAD
            collision.gameObject.GetComponent<PlayerHealth>().TakeDamage(damageAmount);
        }
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
}
