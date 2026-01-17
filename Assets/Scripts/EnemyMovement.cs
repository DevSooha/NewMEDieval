using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator anim;
    private EnemyCombat enemyCombat;
    private Transform player;
    private EnemyState enemyState;
    private float facingDirection = -1;
    
    [SerializeField] private Transform detectionPoint;
    public float movespeed = 2f;
    public float attackRange = 1f;
    public float detectRange = 5f;
    public LayerMask playerLayer;
    public float attackCooldown = 2f;
    public float attackCooldownTimer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        enemyCombat = GetComponent<EnemyCombat>();
        
        if (detectionPoint == null)
        {
            detectionPoint = transform.Find("DetectionPoint");
        }
        
        ChangeState(EnemyState.Idle);
        
        Debug.Log($"EnemyMovement initialized. Player layer mask: {playerLayer.value}");
    }

    void Update()
    {
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if(enemyState == EnemyState.Attacking&&attackCooldownTimer<attackCooldown - 0.1f)
        {
            ChangeState(EnemyState.Idle);
        }

        if (enemyState != EnemyState.Attacking)
        {
            CheckForPlayer();
        }

        switch (enemyState)
        {
            case EnemyState.Idle:
                Stop();
                break;

            case EnemyState.Chasing:
                Chase();
                break;

            case EnemyState.Attacking:
                Stop();
                break;
        }
    }

    private void Stop()
    {
        rb.linearVelocity = Vector2.zero;
    }

    private void CheckForPlayer()
    {
        if (detectionPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(detectionPoint.position, detectRange, playerLayer);

        if (hits.Length > 0)
        {
            player = hits[0].transform;

            float distance = Vector2.Distance(detectionPoint.position, player.position);

            if (distance <= attackRange && attackCooldownTimer <= 0)
            {
                attackCooldownTimer = attackCooldown;
                ChangeState(EnemyState.Attacking);
            }
            else if (distance <= detectRange && distance > attackRange)
            {
                ChangeState(EnemyState.Chasing);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
    }

    private void Chase()
    {
        if(player == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        // 감지 범위를 벗어남
        if (distance > detectRange)
        {
            ChangeState(EnemyState.Idle);
            Stop();
            return;
        }
        
        Vector2 direction = (player.position - transform.position).normalized;

        if ((direction.x < 0 && facingDirection == 1) || 
            (direction.x > 0 && facingDirection == -1))
        {
            FlipX();
        }
        
        rb.linearVelocity = direction * movespeed;
    }

    private void FlipX()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    private void ChangeState(EnemyState newState)
    {
        if (enemyState == newState) return;

        enemyState = newState;

        switch (enemyState)
        {
            case EnemyState.Idle:
                anim.SetBool("isMoving", false);
                break;

            case EnemyState.Chasing:
                anim.SetTrigger("Chase");
                anim.SetBool("isMoving", true);
                break;
        }
    }

    private void OnDrawGizmos()
    {
        if (detectionPoint != null)
        {
            // 감지 범위 (노란색)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(detectionPoint.position, detectRange);
        }
    }
}

public enum EnemyState
{
    Idle,
    Chasing,
    Attacking,
}