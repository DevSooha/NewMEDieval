using System.Collections;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    
    // --- [요청하신 버프 관련 변수 적용] ---
    public float baseSpeed = 5f;    
    float buffTimeLeft = 0f;
    // ----------------------------------

    private bool knockedBack = false;
    private bool canMove = true;

    [Header("Animation Settings")]
    private Vector2 lastDirection; 
    private float attackDirection = 1f; 
    private bool isAttacking = false;   

    [Header("Components")]
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    public Animator animator;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        // 시작 시 현재 속도를 baseSpeed로 맞춤 (안전장치)
        moveSpeed = baseSpeed; 
    }

    void Update()
    {
        // 1. 넉백 및 이동 불가 상태 체크
        if (knockedBack) return;

        if (isAttacking)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (!canMove)
        {
            moveInput = Vector2.zero;
            animator.SetBool("IsMoving", false);
            return;
        }

        // 2. 이동 입력 처리
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(horizontal, vertical).normalized;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        // 3. 애니메이션 처리
        if (isMoving)
        {
            lastDirection = moveInput;

            if (horizontal != 0)
            {
                attackDirection = horizontal > 0 ? 1f : -1f;
                animator.SetFloat("AttackDir", attackDirection);
            }

            animator.SetFloat("InputX", moveInput.x);
            animator.SetFloat("InputY", moveInput.y);
        }
        else
        {
            animator.SetFloat("InputX", lastDirection.x);
            animator.SetFloat("InputY", lastDirection.y);
        }

        animator.SetBool("IsMoving", isMoving);

        // 4. 공격 입력
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartCoroutine(PerformAttack());
        }

        // --- [요청하신 버프 시간 체크 로직 (Update 내부)] ---
        if (buffTimeLeft > 0f)
        {
            buffTimeLeft -= Time.deltaTime;
            if (buffTimeLeft <= 0f)
            {
                buffTimeLeft = 0f;
                moveSpeed = baseSpeed;   
            }
        }
        // ------------------------------------------------
    }

    void FixedUpdate()
    {
        if (knockedBack) return; 

        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero; 
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed;
    }

    // --- [요청하신 버프 적용 함수] ---
    public void ApplySpeedBuff(float duration)
    {
        buffTimeLeft = duration;
        moveSpeed = baseSpeed * 2;
    }
    // -----------------------------

    IEnumerator PerformAttack()
    {
        isAttacking = true;
        animator.SetTrigger("IsAttack");
        yield return null; 
        animator.ResetTrigger("IsAttack"); 
        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }

    public void SetCanMove(bool value)
    {
        canMove = value;
        if (!canMove)
        {
            rb.linearVelocity = Vector2.zero;
            animator.SetBool("IsMoving", false);
        }
    }

    public bool CanMove()
    {
        return canMove;
    }

    public void KnockBack(Transform enemy, float force, float stunTime)
    {
        knockedBack = true;
        Vector2 direction = (transform.position - enemy.position).normalized;
        rb.linearVelocity = direction * force;
        StartCoroutine(KnockBackCouter(stunTime));
    }

    IEnumerator KnockBackCouter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        rb.linearVelocity = Vector2.zero;
        knockedBack = false;
    }
}