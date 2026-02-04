using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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

    private float lastAttackTime; // 마지막 공격 시점 기록
    public float combatCooldown = 1f; // 공격 간격 (이 시간이 지나야만 다시 때림)

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

        if (Time.time < lastAttackTime + combatCooldown) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange, playerLayer);

        foreach (Collider2D hit in hits)
        {
            // [핵심 2] 거대 콜라이더 무시: Trigger(감지 영역)라면 건너뜀
            if (hit.isTrigger) continue;

            Player playerScript = hit.GetComponent<Player>();

            // [핵심 3] 유효성 검사: 스크립트가 있고 & 오브젝트가 활성화 상태(살아있음)인지
            if (playerScript != null && hit.gameObject.activeInHierarchy)
            {
                // 데미지 처리
                hit.GetComponent<PlayerHealth>()?.TakeDamage(damageAmount);

                // 넉백 처리 (살아있는 대상에게만 코루틴 실행)
                playerScript.KnockBack(transform, knockbackForce, stunTime);

                // [핵심 4] 공격 성공 시점 기록 & 루프 종료
                lastAttackTime = Time.time;
                return; // 한 명만(혹은 한 부위만) 때리고 즉시 종료 (중복 타격 방지)
            }
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
            wi.Init(pastelbloomItemData, dropAmount);
            item.GetComponent<SpriteRenderer>().sprite = pastelbloomItemData.icon;
        }
    }
}
