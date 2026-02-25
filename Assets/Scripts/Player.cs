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
    [SerializeField] private LayerMask knockbackObstacleLayers;

    // è¸°ê¾ªë´??¿Â€??è¹‚Â€??
    public float baseSpeed = 5f;
    float buffTimeLeft = 0f;

    // ?? ìŒ??????è¹‚Â€??
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
    private InventoryUI inventoryUI;
    private bool isKnockedBack = false;
    private RigidbodyType2D defaultBodyType;
    private Coroutine knockbackRoutine;
    private Coroutine blinkRoutine;

    void Awake()
    {
        // ?? ì™?????? ì?ê½??? ìŒ??
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

        defaultBodyType = rb.bodyType;

        playerInteraction = GetComponentInChildren<PlayerInteraction>();
        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        moveSpeed = baseSpeed;

        int obstacleMask = LayerMask.GetMask("Obstacle");
        if (knockbackObstacleLayers.value == 0)
        {
            knockbackObstacleLayers = obstacleMask;
        }
        else if (obstacleMask != 0 && (knockbackObstacleLayers.value & obstacleMask) == 0)
        {
            knockbackObstacleLayers |= obstacleMask;
        }
    }


    // ?? ì?ê¹?è¹‚Â€???? ìŒ??PlayerState ?¿Â€??

    void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;

        currentState = newState;

        animator.ResetTrigger("IsAttack"); 
    }

    void Update()
    {
        HandleInventoryToggle();

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
    void HandleInventoryToggle()
    {
        // Inventory is controlled by Crafting UI only.
        return;
    }

    void HandleInteractionOnly()
    {
        if (IsInteractOrAttackPressed())
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


    // ?? ìˆë£????? ìˆ?²ï§ë¶¿ì” ??ï§£ì„???? ìŒ??

    void HandleMovement()
    {
        // 2. ?? ìˆë£??? ìˆ??ï§£ì„??
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

        // 3. ?? ìˆ?²ï§ë¶¿ì” ??ï§£ì„??
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
            // è¸°ê¾ªë´??? ì„ì»?ï§£ëŒ„ê²?
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


    // ?¨ë“¦êº??¿Â€??ï§£ì„???? ìŒ??

    void HandleAttack()
    {
        if (IsInteractOrAttackPressed())
        {
            // 1. ?? ì???? ìŒ???’ì‡½?? ?? ìˆë£?
            if (playerInteraction != null && playerInteraction.TryInteract())
            {
                return;
            }

            Debug.Log($"Logic Dir: {lastDirection} / Animator X: {animator.GetFloat("InputX")} / FlipX: {spriteRenderer.flipX}");
            // 2. ?? ì???? ìŒ??????? ìŒ?å ??¨ë“¦êº?
            StartCoroutine(PerformAttack());
        }
    }
    bool IsInteractOrAttackPressed()
    {
        return Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(1);
    }


    void FixedUpdate()
    {
        if (isKnockedBack) return; // ?? ìˆê°?ä»¥ë¬’ë¿??? ìˆ????? ìˆ???¾ëŒ??

        switch (currentState)
        {
            case PlayerState.Stunned:
            case PlayerState.Attack:
                // ?? ìˆë£??0?? ìˆì¤??? ìŒ??(?? ìˆê°???KnockBack ?? ìŒ??? ìŒê½??? ìŒ??åª›Â€?? ì™?????? ì„ë¦??0)
                rb.linearVelocity = Vector2.zero;
                break;

            case PlayerState.Move:
                // ?? ìˆë£??? ì?ê¹???? ìˆì­??¾ì‡°????åª›Â€?? ì„ë¦?
                rb.linearVelocity = moveInput * moveSpeed;
                break;

            case PlayerState.Idle:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    IEnumerator PerformAttack()
    {
        // 1. ?¨ë“¦êº??? ì?ê¹? ?ï§ê¾©??
        ChangeState(PlayerState.Attack);

        // 2. ?? ìˆ?²ï§ë¶¿ì” ???? ì?ë»?
        animator.SetTrigger("IsAttack");
        yield return null; // ???? ìˆ????? ì™??(?? ìˆ?²ï§ë¶¿ì” ??åª›ê¹†???? ì?ë¸?
        animator.ResetTrigger("IsAttack");

        // 3. ?? ìˆ????? ì™??
        yield return new WaitForSeconds(0.4f);

        // 4. ?? ìŒ???? ì™???? ì?ê¹? ?è¹‚ë“¸?? (ä»¥ë¬’??)
        ChangeState(PlayerState.Idle);
    }


    // ?? ìˆë£?åª›Â€???? ì™?? ?? ìŒ?????? ìŒ???? ìŒ??

    public void SetCanMove(bool value)
    {
        if (value)
            currentState = PlayerState.Idle; // ?? ìˆë£?åª›Â€?? ì?ë¸? ?Idle
        else
            currentState = PlayerState.Stunned; // ?? ìˆë£??ºëŒ????Stunned (?? ì™?? Interact)
    }

    public bool CanMove()
    {
        // Idle?? ìˆêµ?Move ?? ì?ê¹???? ìˆì­?true è«›ì„‘??
        return currentState == PlayerState.Idle || currentState == PlayerState.Move;
    }
    public void KnockBackByDistance(Vector2 direction, float distance, float duration)
    {
        if (!gameObject.activeInHierarchy) return;
        if (distance <= 0f || duration <= 0f) return;

        if (knockbackRoutine != null)
        {
            StopKnockbackImmediately();
        }

        knockbackRoutine = StartCoroutine(KnockBackDistanceRoutine(direction, distance, duration));
    }

    private void StopKnockbackImmediately()
    {
        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

        isKnockedBack = false;
        if (rb != null)
        {
            rb.bodyType = defaultBodyType;
            rb.linearVelocity = Vector2.zero;
        }
    }

    private IEnumerator KnockBackDistanceRoutine(Vector2 direction, float distance, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f) yield break;

        isKnockedBack = true;
        SetCanMove(false);

        Vector2 dir = direction.normalized;
        float speed = distance / duration;
        float remaining = distance;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = knockbackObstacleLayers,
            useTriggers = false
        };

        RaycastHit2D[] hits = new RaycastHit2D[4];

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        while (remaining > 0f)
        {
            float step = speed * Time.fixedDeltaTime;
            if (step > remaining) step = remaining;

            float moveDistance = step;
            bool blocked = false;

            if (rb != null && knockbackObstacleLayers.value != 0)
            {
                int hitCount = rb.Cast(dir, filter, hits, step);
                if (hitCount > 0)
                {
                    float minDistance = float.MaxValue;
                    for (int i = 0; i < hitCount; i++)
                    {
                        if (hits[i].distance < minDistance)
                        {
                            minDistance = hits[i].distance;
                        }
                    }

                    if (minDistance <= 0.001f)
                    {
                        moveDistance = 0f;
                        blocked = true;
                    }
                    else
                    {
                        moveDistance = Mathf.Min(step, Mathf.Max(0f, minDistance - 0.01f));
                        blocked = moveDistance < step;
                    }
                }
            }

            if (rb != null)
            {
                rb.MovePosition(rb.position + dir * moveDistance);
            }
            else
            {
                transform.position += (Vector3)(dir * moveDistance);
            }

            remaining -= moveDistance;

            if (blocked)
            {
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        isKnockedBack = false;
        if (rb != null)
        {
            rb.bodyType = defaultBodyType;
            rb.linearVelocity = Vector2.zero;
        }
        SetCanMove(true);

        knockbackRoutine = null;
    }
    public void StartBlink(float duration, float interval)
    {
        if (spriteRenderer == null) return;

        // If a previous blink stopped while invisible, force visible before restarting.
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        spriteRenderer.enabled = true;
        float safeInterval = Mathf.Max(0.02f, interval);
        blinkRoutine = StartCoroutine(BlinkRoutine(duration, safeInterval));
    }

    private IEnumerator BlinkRoutine(float duration, float interval)
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            visible = !visible;
            spriteRenderer.enabled = visible;
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        spriteRenderer.enabled = true;
        blinkRoutine = null;
    }
    public void OnInteractionFinished()
    {
        ChangeState(PlayerState.Idle);
    }


    // ?? ìŒ???äºŒì‡±?????¨ë“¦êº?ï§â‘¥??ï§?¶¿??

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


    // --- ???? ìˆë£????? ìŒ???????¿Â€??---

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Coroutines stop on disable, so knockback state must be restored manually.
        StopKnockbackImmediately();
        SetCanMove(true);

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
    }


    // ??æ¿¡ì’•ë±????? ìŒ??è¹‚ë“¦????ç§»ë?ì°???? ì„ê»?

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (UIManager.Instance != null)
        {
            StartCoroutine(UIManager.Instance.FadeIn(0.3f));
        }

        // 2. ?? ìŒ???? ì„ë¦?
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
            // èª˜ëªƒ?²å¯ƒ??—« ?? ìŒë¿?? ìˆ??(0,0)?? ìˆêµ?ï§Â€?? ìˆë§??? ì?ë£??? ìŒ??? ìˆì¤?åª›ëº¤???? ìˆë£?
            transform.position = new Vector3(0, 0, 0);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            SetCanMove(true);
        }
    }

    IEnumerator ForceCameraSync()
    {
        // ???? ìŒ???? ìŒ?? 0.1?¥ëŒ?? ?? ìŒ???æ¹²ê³•??? ìŒê½??? ìˆ??ï§ã…»????? ìŒ???¥ë‡ë¦??ç§»ë?ì°???±ÑŠë€???åª›Â€ ?? ìˆê¶??? ìŒë¿??? ì?ë»?
        yield return new WaitForSeconds(0.1f);

        // ?·ëªƒ??? ì™?? ï§¡ì–˜ë¦?(?? ìŒ??è«›ë¶¾?????? ì™?????? ìˆì¤?ï§¡ì– ë¸????
        RoomManager roomManager = FindFirstObjectByType<RoomManager>();

        if (roomManager != null)
        {
            roomManager.SyncCameraToPlayer();
        }
        else
        {
            // ?·ëªƒ??? ì™??åª›Â€ ?? ìˆ??å¯ƒìŒ????¾©ê¸??? ì™?? ï§ê³¸??ï§ë¶¿??ç§»ë?ì°????? ì™?™å ?
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(transform.position.x, transform.position.y, -10f);
            }
        }
    }


    // ?? ìŒ???????? ìŒ??
    public void SaveCurrentPosition()
    {
        savedPosition = transform.position;
        hasSavedPosition = true;
        Debug.Log($"?«ëš°ëª????? ìˆë§? {savedPosition}");
    }
}


