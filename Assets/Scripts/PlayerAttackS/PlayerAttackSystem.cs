using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class PlayerAttackSystem : MonoBehaviour
{
    [Header("Settings")]
    public float tileSize = 1.0f;
    public LayerMask enemyLayer;
    [SerializeField] private float meleeForwardOffset = -0.5f;
    [SerializeField] private bool debugDrawMeleeGizmo = true;

    [Header("Tilemaps")]
    public Tilemap floorTilemap;

    [Header("Prefabs")]
    public GameObject defaultBombPrefab;
    public GameObject stackMarkerPrefab;
    [SerializeField] private float bombShortPressThreshold = 0.5f;
    [SerializeField] private float bombSecondStackThreshold = 1.05f;
    [SerializeField] private float bombThirdStackThreshold = 1.5f;
    [SerializeField] private LayerMask bombBlockLayer;
    [SerializeField] private float bombBlockCheckRadius = 0.2f;
    [Header("Marker Visual")]
    [SerializeField] private bool forceStackMarkerSorting = true;
    [SerializeField] private string stackMarkerSortingLayerName = "Objects";
    [SerializeField] private int stackMarkerSortingOrder = 30;
    [Header("Diagnostics")]
    [SerializeField] private bool enableAttackDiagnostics = true;

    [Header("Weapon Slots")]
    public List<WeaponSlot> slots = new();

    private Player playerMovement;
    private PlayerStatusController statusController;
    private PlayerInteraction interactionSensor;
    private InventoryUI inventoryUI;

    private Vector2 aimDirection = Vector2.down;
    private bool isAttack = false;
    private bool isCharging = false;

    private float chargeStartTime;
    private int currentStack = 0;
    private Coroutine chargeRoutine;
    private readonly List<GameObject> activeMarkers = new();
    private readonly List<int> activeMarkerStacks = new();

    void Start()
    {
        playerMovement = GetComponent<Player>();
        statusController = GetComponent<PlayerStatusController>();
        interactionSensor = GetComponentInChildren<PlayerInteraction>();
        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        ResolveFloorTilemap();

        if (bombBlockLayer.value == 0)
        {
            bombBlockLayer = LayerMask.GetMask("Obstacle");
        }

        EnsureCoreSlots();
        ValidateAttackVisualSetup();

        // Weapon status UI removed.
    }

    void Update()
    {
        if (interactionSensor != null && interactionSensor.IsCraftingUiOpen)
        {
            CancelTransientInputState();
            return;
        }

        ResolveFloorTilemap();
        UpdateAimDirection();
        EnsureCoreSlots();
        SyncPotionSlotCounts();
        bool currentSlotIsPotionBomb = IsCurrentSlotPotion();
        RecoverFromStaleBombChargeState(currentSlotIsPotionBomb);
        bool attackPressedThisFrame = IsAttackPressed();

        bool rotatePressed = statusController != null
            ? statusController.ProcessActionButtonDown("rotate_slot", Input.GetKeyDown(KeyCode.C))
            : Input.GetKeyDown(KeyCode.C);

        if (!isAttack && !isCharging && rotatePressed)
        {
            RotateWeaponSlots();
        }

        if (interactionSensor != null
            && interactionSensor.IsInteractable
            && !isCharging
            && !attackPressedThisFrame
            && !currentSlotIsPotionBomb)
        {
            return;
        }

        if (slots.Count > 0 && slots[0].type != WeaponType.None)
        {
            WeaponSlot currentSlot = slots[0];
            bool stalePotionSlotWithoutAmmo = currentSlot != null
                                              && currentSlot.type == WeaponType.PotionBomb
                                              && GetCurrentBombAmmoCount() <= 0;
            if (stalePotionSlotWithoutAmmo)
            {
                NormalizeWeaponSlots(compactSlots: true);
                EnsureCoreSlots();
                currentSlot = slots[0];
            }

            if (currentSlot != null && currentSlot.type == WeaponType.Melee)
            {
                if (!isAttack) HandleMeleeInput();
            }
            else if (currentSlot != null && currentSlot.type == WeaponType.PotionBomb)
            {
                HandleBombInput();
            }
        }
    }

    void UpdateAimDirection()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        if (x != 0 || y != 0)
        {
            aimDirection = new Vector2(x, y).normalized;
        }
    }

    bool IsAttackPressed()
    {
        return CombatInputHelper.IsAttackPressed(statusController);
    }

    bool IsAttackReleased()
    {
        return CombatInputHelper.IsAttackReleased(statusController);
    }

    private void RecoverFromStaleBombChargeState(bool currentSlotIsPotionBomb)
    {
        if (currentSlotIsPotionBomb || !isCharging)
        {
            return;
        }

        isCharging = false;
        currentStack = 0;
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }

        ClearMarkers();
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
        }

        if (enableAttackDiagnostics)
        {
            Debug.LogWarning("[AttackSystem] Cleared stale bomb charge state while non-potion slot is active.", this);
        }
    }

    private void ValidateAttackVisualSetup()
    {
        if (!enableAttackDiagnostics)
        {
            return;
        }

        if (defaultBombPrefab == null)
        {
            Debug.LogWarning("[AttackSystem] defaultBombPrefab is missing. Bomb spawn will fail.");
        }

        if (stackMarkerPrefab == null)
        {
            Debug.LogWarning("[AttackSystem] stackMarkerPrefab is missing. Charge stack marker will not render.");
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        WarnIfLayerExcludedFromCamera(mainCamera, defaultBombPrefab, "Bomb");
        WarnIfLayerExcludedFromCamera(mainCamera, stackMarkerPrefab, "StackMarker");
    }

    private void WarnIfLayerExcludedFromCamera(Camera cam, GameObject prefab, string label)
    {
        if (cam == null || prefab == null)
        {
            return;
        }

        int layer = prefab.layer;
        bool included = (cam.cullingMask & (1 << layer)) != 0;
        if (!included)
        {
            Debug.LogWarning($"[AttackSystem] {label} prefab layer '{LayerMask.LayerToName(layer)}' is excluded from camera culling mask.");
        }
    }

    private void ResolveFloorTilemap()
    {
        // 캐시된 타일맵이 유효하고, 현재 플레이어 위치에 타일이 있으면 유지
        if (floorTilemap != null && floorTilemap.gameObject.activeInHierarchy)
        {
            Vector3Int cell = floorTilemap.WorldToCell(transform.position);
            if (floorTilemap.HasTile(cell))
                return;
        }

        // 활성 타일맵 중 플레이어 위치에 타일이 있는 Ground 타일맵을 찾음
        floorTilemap = null;
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Tilemap fallback = null;

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (!IsGroundTilemap(tilemap))
                continue;

            Vector3Int cell = tilemap.WorldToCell(transform.position);
            if (tilemap.HasTile(cell))
            {
                floorTilemap = tilemap;
                return;
            }

            if (fallback == null)
                fallback = tilemap;
        }

        floorTilemap = fallback;
    }

    public void CancelTransientInputState()
    {
        isAttack = false;
        isCharging = false;
        currentStack = 0;

        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }

        ClearMarkers();

        if (playerMovement != null)
        {
            playerMovement.CancelAttack();
            playerMovement.SetCanMove(true);
        }
    }
}
