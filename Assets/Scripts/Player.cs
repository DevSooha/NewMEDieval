using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

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

    // ?�곌?�遊???�???�궰???
    public float baseSpeed = 5f;
    float buffTimeLeft = 0f;

    // ??좎럩???????�궰???
    private Vector3 savedPosition;
    private bool hasSavedPosition = false;

    public bool HasSavedPosition => hasSavedPosition;

    [Header("Animation Settings")]
    private Vector2 lastDirection = Vector2.down;
    private float lastHorizontalAttackSign = 1f;

    public Vector2 LastMoveDirection => lastDirection;

    [Header("Components")]
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput;
    public Animator animator;
    private PlayerInteraction playerInteraction;
    private InventoryUI inventoryUI;
    private PlayerStatusController statusController;
    private PlayerAttackSystem attackSystem;
    private Collider2D bodyCollider;
    private bool isKnockedBack = false;
    private RigidbodyType2D defaultBodyType;
    private Coroutine knockbackRoutine;
    private Coroutine blinkRoutine;

    void Awake()
    {
        // ??좎룞??????�?????좎럩??
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
        bodyCollider = GetComponent<Collider2D>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        defaultBodyType = rb.bodyType;

        playerInteraction = GetComponentInChildren<PlayerInteraction>();
        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        statusController = GetComponent<PlayerStatusController>();
        attackSystem = GetComponent<PlayerAttackSystem>();

        moveSpeed = baseSpeed;

        int obstacleMask = LayerMask.GetMask("Obstacle");
        int wallMask = LayerMask.GetMask("Wall");
        if (knockbackObstacleLayers.value == 0)
        {
            knockbackObstacleLayers = obstacleMask | wallMask;
        }

        if (obstacleMask != 0 && (knockbackObstacleLayers.value & obstacleMask) == 0)
        {
            knockbackObstacleLayers |= obstacleMask;
        }

        if (wallMask != 0 && (knockbackObstacleLayers.value & wallMask) == 0)
        {
            knockbackObstacleLayers |= wallMask;
        }
    }


    // ??�?�??�궰?????좎럩??PlayerState ??�??

    void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return;

        currentState = newState;

        if (CanUseAnimator()) animator.ResetTrigger("IsAttack");
    }

    bool CanUseAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
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
        if (playerInteraction != null && playerInteraction.IsCraftingUiOpen)
        {
            moveInput = Vector2.zero;
            ChangeState(PlayerState.Idle);
            return;
        }

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


    // ??좎럥??????좎럥??�쭖?�우�??筌ｌ�????좎럩??

    void HandleMovement()
    {
        if (UIManager.DialogueActive
            || UIManager.SelectionActive
            || (playerInteraction != null && playerInteraction.IsCraftingUiOpen))
        {
            moveInput = Vector2.zero;
            ChangeState(PlayerState.Idle);
            if (CanUseAnimator()) animator.SetBool("IsMoving", false);
            return;
        }

        if (statusController == null)
        {
            statusController = GetComponent<PlayerStatusController>();
        }

        if (statusController != null && statusController.IsStunned)
        {
            moveInput = Vector2.zero;
            ChangeState(PlayerState.Stunned);
            if (CanUseAnimator()) animator.SetBool("IsMoving", false);
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector2 rawInput = new Vector2(horizontal, vertical).normalized;
        moveInput = statusController != null ? statusController.ProcessMovementInput(rawInput) : rawInput;

        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            ChangeState(PlayerState.Move);
        }
        else
        {
            ChangeState(PlayerState.Idle);
        }

        if (isMoving)
        {
            Vector2 quantizedDirection = QuantizeToEightDirections(moveInput);
            if (quantizedDirection.sqrMagnitude > 0.0001f)
            {
                lastDirection = quantizedDirection;
                if (Mathf.Abs(quantizedDirection.x) > 0.0001f)
                {
                    lastHorizontalAttackSign = Mathf.Sign(quantizedDirection.x);
                }
            }

            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }

        if (CanUseAnimator()) animator.SetFloat("InputX", lastDirection.x);
        if (CanUseAnimator()) animator.SetFloat("InputY", lastDirection.y);

        if (CanUseAnimator()) animator.SetBool("IsMoving", isMoving);
    }

    private static Vector2 QuantizeToEightDirections(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f) return Vector2.zero;

        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        int sector = Mathf.RoundToInt(angle / 45f) % 8;
        return sector switch
        {
            0 => Vector2.right,
            1 => new Vector2(1f, 1f).normalized,
            2 => Vector2.up,
            3 => new Vector2(-1f, 1f).normalized,
            4 => Vector2.left,
            5 => new Vector2(-1f, -1f).normalized,
            6 => Vector2.down,
            _ => new Vector2(1f, -1f).normalized
        };
    }

    void CheckBuffStatus()
        {
            // ?�곌?�遊???좎럡??筌ｋ?�寃?
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


    // ??��?????�??筌ｌ�????좎럩??

    void HandleAttack()
    {
        if (playerInteraction != null && playerInteraction.IsCraftingUiOpen)
        {
            return;
        }

        if (IsInteractOrAttackPressed())
        {
            bool hasImmediateInteractionTarget = playerInteraction != null
                                                 && playerInteraction.HasImmediateInteractionTarget;
            if (hasImmediateInteractionTarget && playerInteraction.TryInteract())
            {
                return;
            }

            // 1. ??�????좎럩???믪눦?? ??좎럥??
            // Potion usage is handled by PlayerAttackSystem; do not play melee attack motion.
            if (IsPotionWeaponSelected())
            {
                return;
            }

            bool isFlipX = spriteRenderer != null && spriteRenderer.flipX;
            Debug.Log($"Logic Dir: {lastDirection} / Animator X: {(CanUseAnimator() ? animator.GetFloat("InputX") : 0f)} / FlipX: {isFlipX}");
            // 2. ??�????좎럩???????좎럩???��???��???
            StartCoroutine(PerformAttack());
        }
    }

    private bool IsPotionWeaponSelected()
    {
        if (attackSystem == null)
        {
            attackSystem = GetComponent<PlayerAttackSystem>();
        }

        if (attackSystem == null)
        {
            return false;
        }

        if (attackSystem.IsCurrentSlotPotion())
        {
            return true;
        }

        if (attackSystem.slots == null || attackSystem.slots.Count == 0)
        {
            return false;
        }

        WeaponSlot currentSlot = attackSystem.slots[0];
        return currentSlot != null
            && currentSlot.type == WeaponType.PotionBomb
            && currentSlot.equippedPotion != null
            && currentSlot.equippedPotion.quantity > 0;
    }
    bool IsInteractOrAttackPressed()
    {
        return CombatInputHelper.IsAttackPressed(statusController);
    }


    void FixedUpdate()
    {
        if (isKnockedBack) return; // ??좎럥�?餓λ쵐????좎럥?????좎럥????�똻??

        switch (currentState)
        {
            case PlayerState.Stunned:
            case PlayerState.Attack:
                // ??좎럥???0??좎럥�???좎럩??(??좎럥�???KnockBack ??좎럩???좎럩????좎럩???�쎛???좎룞??????좎럡???0)
                rb.linearVelocity = Vector2.zero;
                break;

            case PlayerState.Move:
                // ??좎럥????�?�????좎럥�???�눖?????�쎛???좎럡??
                rb.linearVelocity = moveInput * moveSpeed;
                break;

            case PlayerState.Idle:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    IEnumerator PerformAttack()
    {
        if (IsPotionWeaponSelected())
        {
            ChangeState(PlayerState.Idle);
            yield break;
        }
        // 1. ??��?????�?�???筌욊???
        ChangeState(PlayerState.Attack);

        // 2. ??좎럥??�쭖?�우�????�?�?
        if (CanUseAnimator())
        {
            ApplyAttackDirectionForAnimator();
            animator.SetTrigger("IsAttack");
        }
        yield return null; // ????좎럥?????좎룞??(??좎럥??�쭖?�우�???�쏄?????�???
        if (CanUseAnimator()) animator.ResetTrigger("IsAttack");

        // 3. ??좎럥?????좎룞??
        yield return new WaitForSeconds(0.4f);

        // 4. ??좎럩????좎룞????�?�????�귣�?? (餓λ쵐??)
        ChangeState(PlayerState.Idle);
    }

    private void ApplyAttackDirectionForAnimator()
    {
        if (!CanUseAnimator())
        {
            return;
        }

        Vector2 attackDirection = lastDirection;

        // There is no dedicated Attack_S clip.
        // For south attacks, use side attack based on latest horizontal intent.
        if (attackDirection.y < -0.5f)
        {
            float sideX = Mathf.Abs(attackDirection.x) > 0.0001f
                ? Mathf.Sign(attackDirection.x)
                : Mathf.Sign(lastHorizontalAttackSign);

            if (Mathf.Abs(sideX) < 0.0001f)
            {
                sideX = 1f;
            }

            animator.SetFloat("InputX", sideX);
            animator.SetFloat("InputY", 0f);
            return;
        }

        animator.SetFloat("InputX", attackDirection.x);
        animator.SetFloat("InputY", attackDirection.y);
    }


    // ??좎럥???�쎛?????좎룞?? ??좎럩??????좎럩????좎럩??

    public void SetCanMove(bool value)
    {
        if (value)
            currentState = PlayerState.Idle; // ??좎럥???�쎛???�?????Idle
        else
            currentState = PlayerState.Stunned; // ??좎럥???븍뜉????Stunned (??좎룞?? Interact)
    }

    public bool CanMove()
    {
        // Idle??좎럥??Move ??�?�????좎럥�?true ?�쏆�??
        return currentState == PlayerState.Idle || currentState == PlayerState.Move;
    }
    public void KnockBack(Transform sender, float force, float stunTime)
    {
        if (sender == null) return;

        Vector2 direction = (Vector2)(transform.position - sender.position);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = lastDirection.sqrMagnitude > 0.0001f ? lastDirection : Vector2.up;
        }

        float duration = Mathf.Max(0.01f, stunTime);
        float distance = Mathf.Max(0f, force) * duration;
        KnockBackByDistance(direction, distance, duration);
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
            Vector2 currentPosition = rb != null ? rb.position : (Vector2)transform.position;

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

            Vector3 desiredPosition = currentPosition + dir * moveDistance;
            Vector3 constrainedPosition = ConstrainToWalkablePosition(desiredPosition);
            float constrainedDistance = Vector2.Distance(currentPosition, constrainedPosition);

            if (constrainedDistance + 0.0001f < moveDistance)
            {
                moveDistance = constrainedDistance;
                blocked = true;
            }

            if (rb != null)
            {
                rb.MovePosition(constrainedPosition);
            }
            else
            {
                transform.position = constrainedPosition;
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

    public Vector3 ConstrainToWalkablePosition(Vector3 desiredPosition)
    {
        Tilemap groundTilemap = ResolveGroundTilemap(desiredPosition);
        if (groundTilemap == null)
        {
            return desiredPosition;
        }

        if (IsWalkablePosition(groundTilemap, desiredPosition))
        {
            return desiredPosition;
        }

        Vector3Int originCell = groundTilemap.WorldToCell(desiredPosition);

        for (int radius = 0; radius <= 8; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (radius > 0 && Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector3Int cell = originCell + new Vector3Int(x, y, 0);
                    if (!groundTilemap.HasTile(cell))
                    {
                        continue;
                    }

                    Vector3 candidate = ClampIntoCell(groundTilemap, cell, desiredPosition);
                    if (IsWalkablePosition(groundTilemap, candidate))
                    {
                        return candidate;
                    }

                    Vector3 center = groundTilemap.GetCellCenterWorld(cell);
                    if (IsWalkablePosition(groundTilemap, center))
                    {
                        return center;
                    }
                }
            }
        }

        return transform.position;
    }

    public void SnapToWalkablePosition()
    {
        Vector3 constrainedPosition = ConstrainToWalkablePosition(transform.position);
        transform.position = constrainedPosition;

        if (rb != null)
        {
            rb.position = constrainedPosition;
            rb.linearVelocity = Vector2.zero;
        }
    }

    private Tilemap ResolveGroundTilemap(Vector3 worldPosition)
    {
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Tilemap fallback = null;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (!IsGroundTilemap(tilemap))
            {
                continue;
            }

            Vector3Int cell = tilemap.WorldToCell(worldPosition);
            if (tilemap.HasTile(cell))
            {
                return tilemap;
            }

            if (fallback == null)
            {
                fallback = tilemap;
            }
        }

        return fallback;
    }

    private static bool IsGroundTilemap(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return false;
        }

        GameObject owner = tilemap.gameObject;
        if (owner == null)
        {
            return false;
        }

        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int wallLayer = LayerMask.NameToLayer("Wall");
        int ownerLayer = owner.layer;
        if ((obstacleLayer >= 0 && ownerLayer == obstacleLayer)
            || (wallLayer >= 0 && ownerLayer == wallLayer))
        {
            return false;
        }

        if (owner.CompareTag("Ground"))
        {
            return true;
        }

        return HasIdentityInHierarchy(tilemap.transform, "Ground", "ground")
            || HasIdentityInHierarchy(tilemap.transform, "Floor", "floor");
    }

    private bool IsWalkablePosition(Tilemap groundTilemap, Vector3 worldPosition)
    {
        if (groundTilemap == null)
        {
            return true;
        }

        Vector3Int cell = groundTilemap.WorldToCell(worldPosition);
        if (!groundTilemap.HasTile(cell))
        {
            return false;
        }

        return !OverlapsBlockingCollider(worldPosition);
    }

    private bool OverlapsBlockingCollider(Vector3 worldPosition)
    {
        if (knockbackObstacleLayers.value == 0)
        {
            return false;
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        Vector2 point = worldPosition;
        Vector2 size = Vector2.one * 0.2f;
        float angle = 0f;

        if (bodyCollider is BoxCollider2D box)
        {
            size = Vector2.Scale(box.size, transform.lossyScale) * 0.9f;
            point += Vector2.Scale(box.offset, transform.lossyScale);
            angle = transform.eulerAngles.z;
            return HasBlockingHit(Physics2D.OverlapBoxAll(point, size, angle, knockbackObstacleLayers));
        }

        if (bodyCollider is CapsuleCollider2D capsule)
        {
            size = Vector2.Scale(capsule.size, transform.lossyScale) * 0.9f;
            point += Vector2.Scale(capsule.offset, transform.lossyScale);
            return HasBlockingHit(Physics2D.OverlapCapsuleAll(point, size, capsule.direction, angle, knockbackObstacleLayers));
        }

        if (bodyCollider is CircleCollider2D circle)
        {
            float radius = circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)) * 0.9f;
            point += Vector2.Scale(circle.offset, transform.lossyScale);
            return HasBlockingHit(Physics2D.OverlapCircleAll(point, radius, knockbackObstacleLayers));
        }

        Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one * 0.2f);
        size = new Vector2(bounds.size.x, bounds.size.y) * 0.9f;
        return HasBlockingHit(Physics2D.OverlapBoxAll(point, size, angle, knockbackObstacleLayers));
    }

    private bool HasBlockingHit(Collider2D[] hits)
    {
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == bodyCollider || hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.isTrigger)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static Vector3 ClampIntoCell(Tilemap tilemap, Vector3Int cell, Vector3 desiredPosition)
    {
        Vector3 cellOrigin = tilemap.CellToWorld(cell);
        Vector3 cellSize = tilemap.cellSize;
        const float padding = 0.05f;

        float minX = cellOrigin.x + padding;
        float maxX = cellOrigin.x + cellSize.x - padding;
        float minY = cellOrigin.y + padding;
        float maxY = cellOrigin.y + cellSize.y - padding;

        return new Vector3(
            Mathf.Clamp(desiredPosition.x, minX, maxX),
            Mathf.Clamp(desiredPosition.y, minY, maxY),
            desiredPosition.z);
    }

    private static bool HasIdentityInHierarchy(Transform start, string exactName, string containsName)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.name == exactName
                || current.name.IndexOf(containsName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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


    // ??좎럩????�뚯???????��???筌뤴�??�????

    public void CancelAttack()
    {
        StopCoroutine("PerformAttack");
        ChangeState(PlayerState.Idle);
        if (CanUseAnimator()) animator.ResetTrigger("IsAttack");
    }

    public void StopMoving()
    {
        if (CanUseAnimator()) animator.SetBool("IsMoving", false);
    }


    // --- ????좎럥??????좎럩????????�??---

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


    // ???�≪뮆諭?????좎럩???�귣벀?????�삳?�????좎럡??

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (UIManager.Instance != null)
        {
            StartCoroutine(UIManager.Instance.FadeIn(0.3f));
        }

        // 2. ??좎럩????좎럡??
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
            // 沃섎�?꿨칰??�???좎럩???좎럥??(0,0)??좎럥??筌왖???좎럥�???�?????좎럩???좎럥�??�쏅�????좎럥??
            transform.position = new Vector3(0, 0, 0);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            SetCanMove(true);
        }
    }

    IEnumerator ForceCameraSync()
    {
        // ????좎럩????좎럩?? 0.1?λ??? ??좎럩????�꿸????좎럩????좎럥??筌띲??????좎럩???λ?�由???�삳?�???귐딅?????�쎛? ??좎럥�???좎럩????�?�?
        yield return new WaitForSeconds(0.1f);

        // ?룸챶???좎룞?? 筌≪뼐由?(??좎럩???�쏅?�??????좎룞??????좎럥�?筌≪뼚釉????
        RoomManager roomManager = FindFirstObjectByType<RoomManager>();

        if (roomManager != null)
        {
            roomManager.SyncCameraToPlayer();
        }
        else
        {
            // ?룸챶???좎룞???�쎛? ??좎럥???�껋??????�湲???좎룞?? 筌욊???筌롫????�삳?�?????좎룞??�뜝?
            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(transform.position.x, transform.position.y, -10f);
            }
        }
    }


    // ??좎럩????????좎럩??
    public void SaveCurrentPosition()
    {
        savedPosition = transform.position;
        hasSavedPosition = true;
        Debug.Log($"??�슦�?????좎럥�? {savedPosition}");
    }
}
