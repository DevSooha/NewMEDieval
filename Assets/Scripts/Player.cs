using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    public enum PlayerState
    {
        Idle,
        Move,
        Attack,
        Interact,
        Stunned
    }

    private PlayerState currentState;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    // 버프 관련 변수
    public float baseSpeed = 5f;
    float buffTimeLeft = 0f;

    // 위치 저장 변수
    private Vector3 savedPosition;
    private bool hasSavedPosition = false;

    public bool HasSavedPosition => hasSavedPosition;

    [Header("Animation Settings")]
    private Vector2 lastDirection;

    [Header("Components")]
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    public Animator animator;
    private PlayerInteraction playerInteraction;
    private bool isKnockedBack = false;

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

        playerInteraction = GetComponentInChildren<PlayerInteraction>();

        moveSpeed = baseSpeed;
    }


    // 상태 변경 함수(PlayerState 관리)

    void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;

        currentState = newState;

        animator.ResetTrigger("IsAttack"); 
    }

    void Update()
    {
        switch (currentState)
        {
            case PlayerState.Stunned:
            case PlayerState.Attack:
                break;

            case PlayerState.Interact:
                HandleInteractionOnly();
                break;

            case PlayerState.Idle:
            case PlayerState.Move:
                HandleMovement();
                HandleAttack();
                break;
        }
        CheckBuffStatus();
    }

    // 대화 중에는 '공격'은 안 하고 '상호작용(다음 대사)'만 체크하는 함수
    void HandleInteractionOnly()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (playerInteraction != null)
            {
                bool success = playerInteraction.TryInteract();

                if (!success)
                {
                    ChangeState(PlayerState.Idle);
                }
            }
        }
    }


    // 이동 및 애니메이션 처리 함수

    void HandleMovement()
    {
        // 2. 이동 입력 처리
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(horizontal, vertical).normalized;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            ChangeState(PlayerState.Move);
        }
        else
        {
            ChangeState(PlayerState.Idle);
        }

        // 3. 애니메이션 처리
        if (isMoving)
        {
            if(vertical > 0.01f)
            {
                lastDirection = Vector2.up;
            }

            else if (horizontal != 0)
            {
                lastDirection = new Vector2(horizontal, 0).normalized;
            }

            if (spriteRenderer != null) spriteRenderer.flipX = false;

            animator.SetFloat("InputX", lastDirection.x);
            animator.SetFloat("InputY", lastDirection.y);
        }
        else
        {
            animator.SetFloat("InputX", lastDirection.x);
            animator.SetFloat("InputY", lastDirection.y);
        }

        animator.SetBool("IsMoving", isMoving);
    }

    void CheckBuffStatus()
        {
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

    public void ApplySpeedBuff(float duration)
    {
        buffTimeLeft = duration;
        moveSpeed = baseSpeed * 2;
    }


    // 공격 관련 처리 함수

    void HandleAttack()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            // 1. 상호작용 먼저 시도
            if (playerInteraction != null && playerInteraction.TryInteract())
            {
                return;
            }

            Debug.Log($"Logic Dir: {lastDirection} / Animator X: {animator.GetFloat("InputX")} / FlipX: {spriteRenderer.flipX}");
            // 2. 상호작용할 게 없으면 공격
            StartCoroutine(PerformAttack());
        }
    }

    void FixedUpdate()
    {
        if (isKnockedBack) return; // 넉백 중엔 키보드 입력 무시

        switch (currentState)
        {
            case PlayerState.Stunned:
            case PlayerState.Attack:
                // 이동력 0으로 설정 (넉백은 KnockBack 함수에서 힘을 가하므로 여기선 0)
                rb.linearVelocity = Vector2.zero;
                break;

            case PlayerState.Move:
                // 이동 상태일 때만 물리 힘 가하기
                rb.linearVelocity = moveInput * moveSpeed;
                break;

            case PlayerState.Idle:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    IEnumerator PerformAttack()
    {
        // 1. 공격 상태로 진입
        ChangeState(PlayerState.Attack);

        // 2. 애니메이션 실행
        animator.SetTrigger("IsAttack");
        yield return null; // 한 프레임 대기 (애니메이터 갱신 위함)
        animator.ResetTrigger("IsAttack");

        // 3. 딜레이 대기
        yield return new WaitForSeconds(0.4f);

        // 4. 다시 대기 상태로 복귀 (중요!)
        ChangeState(PlayerState.Idle);
    }


    // 이동 가능 여부 설정 및 확인 함수

    public void SetCanMove(bool value)
    {
        if (value)
            currentState = PlayerState.Idle; // 이동 가능하면 Idle
        else
            currentState = PlayerState.Stunned; // 이동 불가면 Stunned (혹은 Interact)
    }

    public bool CanMove()
    {
        // Idle이나 Move 상태일 때만 true 반환
        return currentState == PlayerState.Idle || currentState == PlayerState.Move;
    }

    public void KnockBack(Transform sender, float force, float stunTime)
    {
        if (!gameObject.activeInHierarchy) return;
        isKnockedBack = true; // 1. 조작 불능 상태로 전환

        // 2. 넉백 방향 계산: (나 - 적) 벡터의 방향
        Vector2 direction = (transform.position - sender.position).normalized;

        // 3. 기존 속도를 0으로 만들고 힘을 가함 (관성 초기화)
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        // 4. 기절 시간 카운트 시작
        StartCoroutine(ResetKnockBackRoutine(stunTime));
    }

    private IEnumerator ResetKnockBackRoutine(float duration)
    {
        yield return new WaitForSeconds(duration); // 지정된 시간 대기

        isKnockedBack = false; // 5. 조작 가능 상태로 복구
        rb.linearVelocity = Vector2.zero; // 밀려나는 힘 제거
    }

    public void OnInteractionFinished()
    {
        ChangeState(PlayerState.Idle);
    }


    // 아이템 주울 때 공격 모션 캔슬

    public void CancelAttack()
    {
        StopCoroutine("PerformAttack");
        ChangeState(PlayerState.Idle);
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