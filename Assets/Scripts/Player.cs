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

    // Î≤ÑÌîÑ Í¥Ä??Î≥Ä??
    public float baseSpeed = 5f;
    float buffTimeLeft = 0f;

    // ?ÑÏπò ?Ä??Î≥Ä??
    private Vector3 savedPosition;
    private bool hasSavedPosition = false;

    public bool HasSavedPosition => hasSavedPosition;

    [Header("Animation Settings")]
    private Vector2 lastDirection;

    public Vector2 LastMoveDirection => lastDirection;

    [Header("Components")]
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    public Animator animator;
    private PlayerInteraction playerInteraction;
    private bool isKnockedBack = false;

    void Awake()
    {
        // ?±Í????®ÌÑ¥ ?ÅÏö©
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


    // ?ÅÌÉú Î≥ÄÍ≤??®Ïàò(PlayerState Í¥ÄÎ¶?

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

    // ?Ä??Ï§ëÏóê??'Í≥µÍ≤©'?Ä ???òÍ≥† '?ÅÌò∏?ëÏö©(?§Ïùå ?Ä??'Îß?Ï≤¥ÌÅ¨?òÎäî ?®Ïàò
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


    // ?¥Îèô Î∞??†ÎãàÎ©îÏù¥??Ï≤òÎ¶¨ ?®Ïàò

    void HandleMovement()
    {
        // 2. ?¥Îèô ?ÖÎ†• Ï≤òÎ¶¨
        if (UIManager.DialogueActive || UIManager.SelectionActive)
        {
            moveInput = Vector2.zero;
            ChangeState(PlayerState.Idle);
            animator.SetBool("IsMoving", false);
            return;
        }

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

        // 3. ?†ÎãàÎ©îÏù¥??Ï≤òÎ¶¨
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
            // Î≤ÑÌîÑ ?úÍ∞Ñ Ï≤¥ÌÅ¨
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


    // Í≥µÍ≤© Í¥Ä??Ï≤òÎ¶¨ ?®Ïàò

    void HandleAttack()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            // 1. ?ÅÌò∏?ëÏö© Î®ºÏ? ?úÎèÑ
            if (playerInteraction != null && playerInteraction.TryInteract())
            {
                return;
            }

            Debug.Log($"Logic Dir: {lastDirection} / Animator X: {animator.GetFloat("InputX")} / FlipX: {spriteRenderer.flipX}");
            // 2. ?ÅÌò∏?ëÏö©??Í≤??ÜÏúºÎ©?Í≥µÍ≤©
            StartCoroutine(PerformAttack());
        }
    }

    void FixedUpdate()
    {
        if (isKnockedBack) return; // ?âÎ∞± Ï§ëÏóî ?§Î≥¥???ÖÎ†• Î¨¥Ïãú

        switch (currentState)
        {
            case PlayerState.Stunned:
            case PlayerState.Attack:
                // ?¥Îèô??0?ºÎ°ú ?§Ï†ï (?âÎ∞±?Ä KnockBack ?®Ïàò?êÏÑú ?òÏùÑ Í∞Ä?òÎ?Î°??¨Í∏∞??0)
                rb.linearVelocity = Vector2.zero;
                break;

            case PlayerState.Move:
                // ?¥Îèô ?ÅÌÉú???åÎßå Î¨ºÎ¶¨ ??Í∞Ä?òÍ∏∞
                rb.linearVelocity = moveInput * moveSpeed;
                break;

            case PlayerState.Idle:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    IEnumerator PerformAttack()
    {
        // 1. Í≥µÍ≤© ?ÅÌÉúÎ°?ÏßÑÏûÖ
        ChangeState(PlayerState.Attack);

        // 2. ?†ÎãàÎ©îÏù¥???§Ìñâ
        animator.SetTrigger("IsAttack");
        yield return null; // ???ÑÎ†à???ÄÍ∏?(?†ÎãàÎ©îÏù¥??Í∞±Ïã† ?ÑÌï®)
        animator.ResetTrigger("IsAttack");

        // 3. ?úÎ†à???ÄÍ∏?
        yield return new WaitForSeconds(0.4f);

        // 4. ?§Ïãú ?ÄÍ∏??ÅÌÉúÎ°?Î≥µÍ? (Ï§ëÏöî!)
        ChangeState(PlayerState.Idle);
    }


    // ?¥Îèô Í∞Ä???¨Î? ?§Ï†ï Î∞??ïÏù∏ ?®Ïàò

    public void SetCanMove(bool value)
    {
        if (value)
            currentState = PlayerState.Idle; // ?¥Îèô Í∞Ä?•ÌïòÎ©?Idle
        else
            currentState = PlayerState.Stunned; // ?¥Îèô Î∂àÍ?Î©?Stunned (?πÏ? Interact)
    }

    public bool CanMove()
    {
        // Idle?¥ÎÇò Move ?ÅÌÉú???åÎßå true Î∞òÌôò
        return currentState == PlayerState.Idle || currentState == PlayerState.Move;
    }

    public void KnockBack(Transform sender, float force, float stunTime)
    {
        if (!gameObject.activeInHierarchy) return;
        isKnockedBack = true; // 1. Ï°∞Ïûë Î∂àÎä• ?ÅÌÉúÎ°??ÑÌôò

        // 2. ?âÎ∞± Î∞©Ìñ• Í≥ÑÏÇ∞: (??- ?? Î≤°ÌÑ∞??Î∞©Ìñ•
        Vector2 direction = (transform.position - sender.position).normalized;

        // 3. Í∏∞Ï°¥ ?çÎèÑÎ•?0?ºÎ°ú ÎßåÎì§Í≥??òÏùÑ Í∞Ä??(Í¥Ä??Ï¥àÍ∏∞??
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        // 4. Í∏∞Ï†à ?úÍ∞Ñ Ïπ¥Ïö¥???úÏûë
        StartCoroutine(ResetKnockBackRoutine(stunTime));
    }

    private IEnumerator ResetKnockBackRoutine(float duration)
    {
        yield return new WaitForSeconds(duration); // ÏßÄ?ïÎêú ?úÍ∞Ñ ?ÄÍ∏?

        isKnockedBack = false; // 5. Ï°∞Ïûë Í∞Ä???ÅÌÉúÎ°?Î≥µÍµ¨
        rb.linearVelocity = Vector2.zero; // Î∞Ä?§ÎÇò?????úÍ±∞
    }

    public void OnInteractionFinished()
    {
        ChangeState(PlayerState.Idle);
    }


    // ?ÑÏù¥??Ï£ºÏö∏ ??Í≥µÍ≤© Î™®ÏÖò Ï∫îÏä¨

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


    // --- ???¥Îèô Î∞??ÑÏπò ?Ä??Í¥Ä??---

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    // ??Î°úÎìú ???ÑÏπò Î≥µÍµ¨ Î∞?Ïπ¥Î©î???∞Í≤∞

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (UIManager.Instance != null)
        {
            StartCoroutine(UIManager.Instance.FadeIn(0.3f));
        }

        // 2. ?ÑÏπò ?°Í∏∞
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
            // ÎØ∏ÎãàÍ≤åÏûÑ ?¨Ïóê?úÎäî (0,0)?¥ÎÇò ÏßÄ?ïÎêú ?§Ìè∞ ?¨Ïù∏?∏Î°ú Í∞ïÏ†ú ?¥Îèô
            transform.position = new Vector3(0, 0, 0);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            SetCanMove(true);
        }
    }

    IEnumerator ForceCameraSync()
    {
        // ???µÏã¨ ?òÏ†ï: 0.1Ï¥àÎ? ?ïÏã§??Í∏∞Îã§?§ÏÑú ?§Î•∏ Îß§Îãà?Ä?§Ïùò Ï¥àÍ∏∞??Ïπ¥Î©î??Î¶¨ÏÖã ??Í∞Ä ?ùÎÇú ?§Ïóê ?§Ìñâ
        yield return new WaitForSeconds(0.1f);

        // Î£∏Îß§?àÏ? Ï∞æÍ∏∞ (?¨Ïù¥ Î∞îÎÄåÏóà?ºÎ?Î°??àÎ°ú Ï∞æÏïÑ????
        RoomManager roomManager = FindFirstObjectByType<RoomManager>();

        if (roomManager != null)
        {
            roomManager.SyncCameraToPlayer();
        }
        else
        {
            // Î£∏Îß§?àÏ?Í∞Ä ?ÜÎäî Í≤ΩÏö∞ ÎπÑÏÉÅ ?ÄÏ±? ÏßÅÏ†ë Î©îÏù∏ Ïπ¥Î©î????∏∞Í∏?
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(transform.position.x, transform.position.y, -10f);
            }
        }
    }


    // ?ÑÏπò ?Ä???®Ïàò
    public void SaveCurrentPosition()
    {
        savedPosition = transform.position;
        hasSavedPosition = true;
        Debug.Log($"Ï¢åÌëú ?Ä?•Îê®: {savedPosition}");
    }
}