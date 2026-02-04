using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Rigidbody2D rb;
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
        // 1. 쿨타임 감소
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // 2. 공격 중이라면 아무것도 하지 않고 리턴 (매우 중요)
        // 공격 상태 탈출은 애니메이션 이벤트나 타이머로만 처리
        if (enemyState == EnemyState.Attacking)
        {
            // 공격 애니메이션 시간 체크 로직 (작성하신 부분)
            if (attackCooldownTimer < attackCooldown - 0.5f) // 0.1f는 너무 짧을 수 있음, 애니메이션 길이에 맞춰 조정 필요
            {
                ChangeState(EnemyState.Idle);
            }
            return; // <--- 여기서 Update를 끊어주어야 아래 로직(거리재기 등)이 실행되지 않음
        }

        // 3. 공격 중이 아닐 때만 플레이어 감지 및 이동
        CheckForPlayer();

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
            Transform targetTransform = hits[0].transform;

            // 플레이어가 죽어서 비활성화 되었다면 추적하지 않음
            if (!targetTransform.gameObject.activeInHierarchy)
            {
                player = null;
                ChangeState(EnemyState.Idle);
                return;
            }

            player = targetTransform;

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
        else
        {
            // 감지된 게 없으면 타겟을 잃어버린 것임
            player = null;
            ChangeState(EnemyState.Idle);
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

        if (Mathf.Abs(direction.x) > 0.1f)
        {
            if ((direction.x < 0 && facingDirection == 1) ||
                (direction.x > 0 && facingDirection == -1))
            {
                FlipX();
            }
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
                break;

            case EnemyState.Chasing:
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