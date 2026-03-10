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
    private PlayerInteraction interactionSensor;
    private InventoryUI inventoryUI;

    private Vector2 aimDirection = Vector2.down;
    private bool isAttack = false;
    private bool isCharging = false;

    private float chargeStartTime;
    private int currentStack = 0;
    private Coroutine chargeRoutine;
    private readonly List<GameObject> activeMarkers = new();
    private readonly List<Tilemap> cachedGroundTilemaps = new();
    private bool groundTilemapsCached;

    void Start()
    {
        playerMovement = GetComponent<Player>();
        interactionSensor = GetComponentInChildren<PlayerInteraction>();
        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        if (floorTilemap == null)
        {
            GameObject groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null) floorTilemap = groundObj.GetComponent<Tilemap>();
            else
            {
                GameObject floorObj = GameObject.Find("Floor");
                if (floorObj != null) floorTilemap = floorObj.GetComponent<Tilemap>();
            }
        }

        if (bombBlockLayer.value == 0)
        {
            bombBlockLayer = LayerMask.GetMask("Obstacle");
        }

        CacheGroundTilemaps();
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

        UpdateAimDirection();
        EnsureCoreSlots();
        SyncPotionSlotCounts();
        bool currentSlotIsPotionBomb = IsCurrentSlotPotion();
        RecoverFromStaleBombChargeState(currentSlotIsPotionBomb);
        bool attackPressedThisFrame = IsAttackPressed();

        if (!isAttack && !isCharging && Input.GetKeyDown(KeyCode.C))
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
        return CombatInputHelper.IsAttackPressed();
    }

    bool IsAttackReleased()
    {
        return CombatInputHelper.IsAttackReleased();
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
            Debug.LogWarning("[AttackSystem] Main Camera not found. Spawned visuals may be off-screen.");
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
