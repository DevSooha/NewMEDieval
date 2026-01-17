using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    // 버프 관련 변수
    public float baseSpeed = 5f;
    float buffTimeLeft = 0f;

    private bool knockedBack = false;
    private bool canMove = true;

    // 위치 저장 변수
    private Vector3 savedPosition;
    private bool hasSavedPosition = false;

    public bool HasSavedPosition => hasSavedPosition;

    [Header("Animation Settings")]
    private Vector2 lastDirection;
    private float attackDirection = 1f;
    private bool isAttacking = false;

    [Header("Components")]
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    public Animator animator;
    private PlayerInteraction playerInteraction;

    void Awake()
    {
        // 싱글톤 패턴 적용
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

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

        playerInteraction = GetComponent<PlayerInteraction>();

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
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (playerInteraction != null && playerInteraction.IsInteractable)
            {
                return;
            }

            StartCoroutine(PerformAttack());
        }

        // 버프 시간 체크
        if (buffTimeLeft > 0f)
        {
            buffTimeLeft -= Time.deltaTime;
            if (buffTimeLeft <= 0f)
            {
                buffTimeLeft = 0f;
                moveSpeed = baseSpeed;
            }
        }
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

    // 버프 적용 함수
    public void ApplySpeedBuff(float duration)
    {
        buffTimeLeft = duration;
        moveSpeed = baseSpeed * 2;
    }

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
        StartCoroutine(KnockBackCounter(stunTime));
    }

    IEnumerator KnockBackCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        rb.linearVelocity = Vector2.zero;
        knockedBack = false;
    }

    // 아이템 주울 때 공격 모션 캔슬
    public void CancelAttack()
    {
        StopCoroutine("PerformAttack");
        isAttacking = false;
        animator.ResetTrigger("IsAttack");
    }

    public void StopMoving()
    {
        animator.SetBool("IsMoving", false);
    }

    // --- 씬 이동 및 위치 저장 관련 ---

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬 로드 시 위치 복구 및 카메라 연결
    // Player.cs 안의 OnSceneLoaded 함수 수정

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (UIManager.Instance != null)
        {
            StartCoroutine(UIManager.Instance.FadeIn(0.3f));
        }

        // 2. 위치 잡기
        if (scene.name == "Field")
        {
            if (hasSavedPosition)
            {
                transform.position = savedPosition;
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
            SetCanMove(true);
            StartCoroutine(ForceCameraSync());
        }
        else
        {
            // 미니게임 씬에서는 (0,0)이나 지정된 스폰 포인트로 강제 이동
            transform.position = new Vector3(0, 0, 0);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            SetCanMove(true);
        }
    }

    IEnumerator ForceCameraSync()
    {
        // ★ 핵심 수정: 0.1초를 확실히 기다려서 다른 매니저들의 초기화(카메라 리셋 등)가 끝난 뒤에 실행
        yield return new WaitForSeconds(0.1f);

        // 룸매니저 찾기 (씬이 바뀌었으므로 새로 찾아야 함)
        RoomManager roomManager = FindFirstObjectByType<RoomManager>();

        if (roomManager != null)
        {
            // "룸매니저님, 저 여기(savedPosition)에 있으니까 카메라 맞춰주세요"
            roomManager.SyncCameraToPlayer();
        }
        else
        {
            // 룸매니저가 없는 경우 비상 대책: 직접 메인 카메라 옮기기
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(transform.position.x, transform.position.y, -10f);
            }
        }
    }

    // 위치 저장 함수
    public void SaveCurrentPosition()
    {
        savedPosition = transform.position;
        hasSavedPosition = true;
        Debug.Log($"좌표 저장됨: {savedPosition}");
    }
}