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

        if (enemyCombat != null)
        {
            enemyCombat.SetAttackDirection(new Vector2(facingDirection, 0f));
        }

        ChangeState(EnemyState.Idle);

        Debug.Log($"EnemyMovement initialized. Player layer mask: {playerLayer.value}");
    }

    void Update()
    {
        if (enemyCombat != null && enemyCombat.IsAttacking)
        {
            ChangeState(EnemyState.Attacking);
            Stop();
            return;
        }

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

            if (!targetTransform.gameObject.activeInHierarchy)
            {
                player = null;
                ChangeState(EnemyState.Idle);
                return;
            }

            player = targetTransform;

            float distance = Vector2.Distance(detectionPoint.position, player.position);
            Vector2 dirToPlayer = (player.position - transform.position);

            if (enemyCombat != null)
            {
                enemyCombat.SetAttackDirection(dirToPlayer);
            }

            if (distance <= attackRange)
            {
                bool started = enemyCombat != null && enemyCombat.TryAttack();
                if (started || (enemyCombat != null && enemyCombat.IsAttacking))
                {
                    ChangeState(EnemyState.Attacking);
                }
                else
                {
                    ChangeState(EnemyState.Idle);
                }
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
            player = null;
            ChangeState(EnemyState.Idle);
        }
    }

    private void Chase()
    {
        if (player == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance > detectRange)
        {
            ChangeState(EnemyState.Idle);
            Stop();
            return;
        }

        Vector2 direction = (player.position - transform.position).normalized;

        if (enemyCombat != null)
        {
            enemyCombat.SetAttackDirection(direction);
        }

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
