using System.Collections;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public enum WeaponType { None, Melee, PotionBomb }

[System.Serializable]
public class WeaponSlot
{
    public WeaponType type;
    public GameObject specificPrefab;
    public int count = -1;
    public Potion equippedPotion;
}

public class PlayerAttackSystem : MonoBehaviour
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
    [SerializeField] private LayerMask bombBlockLayer;
    [SerializeField] private float bombBlockCheckRadius = 0.2f;

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

        // Weapon status UI removed.
    }

    void Update()
    {
        UpdateAimDirection();
        EnsureCoreSlots();
        SyncPotionSlotCounts();

        if (IsAttackPressed() && interactionSensor != null && interactionSensor.TryInteract())
        {
            return;
        }

        if (!isAttack && !isCharging && Input.GetKeyDown(KeyCode.C))
        {
            RotateWeaponSlots();
        }

        if (interactionSensor != null && interactionSensor.IsInteractable)
        {
            return;
        }

        if (slots.Count > 0 && slots[0].type != WeaponType.None)
        {
            if (slots[0].type == WeaponType.Melee)
            {
                if (!isAttack) HandleMeleeInput();
            }
            else if (slots[0].type == WeaponType.PotionBomb)
            {
                HandleBombInput();
            }
        }
    }

    public void EquipPotionFromInventory(Potion potion)
    {
        if (potion == null || potion.data == null)
            return;

        EnsureCoreSlots();
        int potionSlotIndex = 1;

        WeaponSlot slot = slots[potionSlotIndex];
        slot.type = WeaponType.PotionBomb;
        slot.equippedPotion = potion;
        slot.count = potion.quantity;
        slot.specificPrefab = null;

        slots[potionSlotIndex] = slot;
        NotifyWeaponSlotsChanged(compactSlots: false);

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

    void HandleMeleeInput()
    {
        if (IsAttackPressed())
        {
            StartCoroutine(MeleeAttackRoutine());
        }
    }

    IEnumerator MeleeAttackRoutine()
    {
        isAttack = true;

        Vector2 forward = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.down;
        Vector2 attackPos = (Vector2)transform.position + (forward * (tileSize + meleeForwardOffset));
        Vector2 attackBoxSize = new Vector2(tileSize * 2f, tileSize * 2f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackPos, attackBoxSize, 0f, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            EnemyCombat enemy = hit.GetComponent<EnemyCombat>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(50);
                continue;
            }

            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss != null)
            {
                boss.TakeDamage(50, ElementType.None);
            }
        }

        yield return new WaitForSeconds(0.4f);
        isAttack = false;
    }

    void HandleBombInput()
    {
        if (slots[0].count == 0)
        {
            return;
        }

        if (IsAttackPressed() && !isCharging)
        {
            isCharging = true;
            chargeStartTime = Time.time;
            currentStack = 0;
            ClearMarkers();
            if (playerMovement != null) playerMovement.SetCanMove(false);
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
            }
            chargeRoutine = StartCoroutine(ChargeRoutine());
        }

        if (IsAttackReleased() && isCharging)
        {
            isCharging = false;
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }
            if (playerMovement != null) playerMovement.SetCanMove(true);

            float duration = Time.time - chargeStartTime;
            int spawnedCount = duration < 0.5f
                ? (SpawnBombAt(1) ? 1 : 0)
                : SpawnBombsByStack();

            if (spawnedCount > 0)
            {
                UseAmmo(1);
                RotateWeaponSlots();
            }

            ClearMarkers();
        }
    }

    IEnumerator ChargeRoutine()
    {
        while (isCharging)
        {
            float t = Time.time - chargeStartTime;
            int targetByTime = GetChargeStackByTime(t);
            int reachableCap = GetReachableStackLimit();
            int nextTargetStack = Mathf.Min(targetByTime, reachableCap);

            while (currentStack < nextTargetStack)
            {
                currentStack++;
                ShowStackMarker(currentStack);
            }

            yield return null;
        }
    }

    int GetChargeStackByTime(float elapsed)
    {
        if (elapsed >= 1.5f) return 3;
        if (elapsed >= 1.0f) return 2;
        if (elapsed >= 0.5f) return 1;
        return 0;
    }

    int GetReachableStackLimit()
    {
        int reachable = 0;
        for (int i = 1; i <= 3; i++)
        {
            if (CanPlaceBombAtDistance(i))
            {
                reachable = i;
            }
            else
            {
                break;
            }
        }
        return reachable;
    }

    bool IsValidTile(Vector2 pos)
    {
        return HasGroundTileAtPosition(pos);
    }

    void ShowStackMarker(int stackIndex)
    {
        if (stackMarkerPrefab == null) return;

        Vector2 spawnPos = (Vector2)transform.position + (stackIndex * tileSize * GetAimDirection());
        GameObject marker = Instantiate(stackMarkerPrefab, spawnPos, Quaternion.identity);
        activeMarkers.Add(marker);
    }

    void ClearMarkers()
    {
        foreach (GameObject marker in activeMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeMarkers.Clear();
    }

    bool SpawnBombAt(int distance)
    {
        if (slots.Count == 0) return false;
        if (slots[0].type != WeaponType.PotionBomb) return false;

        Vector2 spawnPos = (Vector2)transform.position + (distance * tileSize * GetAimDirection());
        if (!CanPlaceBombAtDistance(distance)) return false;

        WeaponSlot slot = slots[0];
        GameObject prefabToUse = slot.specificPrefab != null ? slot.specificPrefab : defaultBombPrefab;
        if (prefabToUse == null) return false;

        GameObject bombObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
        Bomb bomb = bombObj.GetComponent<Bomb>();
        if (bomb != null && slot.equippedPotion != null && slot.equippedPotion.data != null)
        {
            bomb.ConfigureFromPotionData(slot.equippedPotion.data);
        }

        return true;
    }

    int SpawnBombsByStack()
    {
        if (currentStack == 0)
        {
            return SpawnBombAt(1) ? 1 : 0;
        }

        int spawnedCount = 0;
        for (int i = 1; i <= currentStack; i++)
        {
            if (!SpawnBombAt(i))
            {
                break;
            }

            spawnedCount++;
        }

        return spawnedCount;
    }

    bool CanPlaceBombAtDistance(int distance)
    {
        Vector2 spawnPos = (Vector2)transform.position + (distance * tileSize * GetAimDirection());
        if (!IsValidTile(spawnPos))
        {
            return false;
        }

        return !IsBombPlacementBlocked(spawnPos);
    }

    bool IsBombPlacementBlocked(Vector2 pos)
    {
        if (bombBlockLayer.value == 0)
        {
            return false;
        }

        float radius = Mathf.Max(0.05f, bombBlockCheckRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius, bombBlockLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;
            if (hit.isTrigger) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (IsEnemyMonsterBossHit(hit)) continue;
            return true;
        }

        return false;
    }

    private bool HasGroundTileAtPosition(Vector2 worldPos)
    {
        if (!groundTilemapsCached || cachedGroundTilemaps.Count == 0)
        {
            CacheGroundTilemaps();
        }

        for (int i = 0; i < cachedGroundTilemaps.Count; i++)
        {
            Tilemap tilemap = cachedGroundTilemaps[i];
            if (tilemap == null) continue;

            Vector3Int cellPos = tilemap.WorldToCell(worldPos);
            if (tilemap.HasTile(cellPos))
            {
                return true;
            }
        }

        return false;
    }

    private void CacheGroundTilemaps()
    {
        groundTilemapsCached = true;
        cachedGroundTilemaps.Clear();

        if (floorTilemap != null && IsGroundTilemap(floorTilemap))
        {
            cachedGroundTilemaps.Add(floorTilemap);
        }

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null || !IsGroundTilemap(tilemap))
            {
                continue;
            }

            if (!cachedGroundTilemaps.Contains(tilemap))
            {
                cachedGroundTilemaps.Add(tilemap);
            }
        }

    }

    private static bool IsGroundTilemap(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return false;
        }

        return HasIdentityInHierarchy(tilemap.transform, "Ground", "ground")
            || HasIdentityInHierarchy(tilemap.transform, "Floor", "floor");
    }

    private static bool IsEnemyMonsterBossHit(Collider2D hit)
    {
        if (hit == null)
        {
            return false;
        }

        if (hit.GetComponentInParent<EnemyCombat>() != null)
        {
            return true;
        }

        if (hit.GetComponentInParent<BossHealth>() != null)
        {
            return true;
        }

        Transform t = hit.transform;
        return HasIdentityInHierarchy(t, "Enemy", "enemy")
            || HasIdentityInHierarchy(t, "Monster", "monster")
            || HasIdentityInHierarchy(t, "Boss", "boss");
    }

    private static bool HasIdentityInHierarchy(Transform transformNode, string tagKeyword, string nameKeyword)
    {
        Transform current = transformNode;
        while (current != null)
        {
            if (!string.IsNullOrEmpty(tagKeyword))
            {
                string tagName = current.tag;
                if (!string.IsNullOrEmpty(tagName)
                    && tagName.IndexOf(tagKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(nameKeyword))
            {
                string objectName = current.name;
                if (!string.IsNullOrEmpty(objectName)
                    && objectName.IndexOf(nameKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }

    Vector2 GetAimDirection()
    {
        return aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.down;
    }

    void UseAmmo(int amount)
    {
        if (slots.Count == 0) return;

        WeaponSlot slot = slots[0];
        if (slot.type != WeaponType.PotionBomb) return;
        bool consumedAny = false;
        bool shouldNormalizeAfterUse = false;

        if (slot.equippedPotion != null)
        {
            slot.equippedPotion.quantity -= amount;
            consumedAny = true;
            if (slot.equippedPotion.quantity <= 0)
            {
                RemovePotionFromInventory(slot.equippedPotion);
                slot.equippedPotion = null;
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.specificPrefab = null;
                shouldNormalizeAfterUse = true;
            }
            else
            {
                slot.count = slot.equippedPotion.quantity;
            }
        }
        else
        {
            if (slot.count != -1)
            {
                slot.count -= amount;
                consumedAny = true;
            }

            if (slot.count <= 0)
            {
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.specificPrefab = null;
                shouldNormalizeAfterUse = true;
            }
        }

        slots[0] = slot;

        if (consumedAny)
        {
            if (shouldNormalizeAfterUse)
            {
                // Keep slot positions after depletion so consumed slot can remain visually empty.
                NormalizeWeaponSlots(compactSlots: false);
            }
            RefreshWeaponAndInventoryUI();
        }

    }

    void RotateWeaponSlots()
    {
        EnsureCoreSlots();
        NormalizeWeaponSlots(compactSlots: true);
        if (CountUsableSlots() <= 1) return;

        int currentIndex = 0;
        int nextIndex = FindNextNonEmptyIndex(currentIndex);
        if (nextIndex <= 0) return;
        RotateListToIndex(nextIndex);
        RefreshWeaponAndInventoryUI();
    }

    int FindNextNonEmptyIndex(int startIndex)
    {
        int count = slots.Count;
        for (int i = 1; i < count; i++)
        {
            int idx = (startIndex + i) % count;
            if (slots[idx].type != WeaponType.None)
            {
                return idx;
            }
        }
        return -1;
    }

    void RotateListToIndex(int targetIndex)
    {
        if (targetIndex <= 0) return;
        for (int i = 0; i < targetIndex; i++)
        {
            WeaponSlot first = slots[0];
            slots.RemoveAt(0);
            slots.Add(first);
        }
    }

    void CompactSlots()
    {
        NormalizeWeaponSlots(compactSlots: true);
    }

    bool IsAttackPressed()
    {
        return CombatInputHelper.IsAttackPressed();
    }

    bool IsAttackReleased()
    {
        return CombatInputHelper.IsAttackReleased();
    }

    void SyncPotionSlotCounts()
    {
        EnsureCoreSlots();
        bool slotChanged = false;
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot.type != WeaponType.PotionBomb || slot.equippedPotion == null)
                continue;

            int qty = Mathf.Max(0, slot.equippedPotion.quantity);
            if (slot.count != qty)
            {
                slot.count = qty;
                slotChanged = true;
            }

            if (qty == 0)
            {
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.equippedPotion = null;
                slot.specificPrefab = null;
                slotChanged = true;
            }
        }

        if (slotChanged)
        {
            NormalizeWeaponSlots(compactSlots: true);
            RefreshWeaponAndInventoryUI();
        }

        // Weapon status UI removed.
    }

    ElementType ConvertElement(Element element)
    {
        switch (element)
        {
            case Element.Fire:
                return ElementType.Fire;
            case Element.Lightning:
                return ElementType.Electric;
            default:
                return ElementType.Water;
        }
    }

    void RemovePotionFromInventory(Potion potion)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null || potion == null) return;

        inv.RemovePotionCompletely(potion);
    }

    void RefreshWeaponSlotUI()
    {
        WeaponSlotUI slotUI = FindFirstObjectByType<WeaponSlotUI>(FindObjectsInactive.Include);
        if (slotUI != null)
        {
            slotUI.ForceRefresh();
        }
    }

    void RefreshWeaponAndInventoryUI()
    {
        RefreshWeaponSlotUI();

        if (inventoryUI == null)
        {
            inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        }

        if (inventoryUI != null)
        {
            inventoryUI.RefreshUI();
        }
    }

    public void NotifyWeaponSlotsChanged(bool compactSlots = true)
    {
        NormalizeWeaponSlots(compactSlots);
        RefreshWeaponAndInventoryUI();
    }

    void EnsureCoreSlots()
    {
        if (slots.Count == 0)
        {
            slots.Add(new WeaponSlot { type = WeaponType.Melee, count = -1 });
        }

        while (slots.Count < 4)
        {
            slots.Add(new WeaponSlot { type = WeaponType.None, count = -1 });
        }
    }

    public bool IsCurrentSlotPotion()
    {
        EnsureCoreSlots();
        if (slots == null || slots.Count == 0)
        {
            return false;
        }

        WeaponSlot slot = slots[0];
        if (slot == null)
        {
            return false;
        }

        return slot.type == WeaponType.PotionBomb
            && slot.equippedPotion != null
            && slot.equippedPotion.quantity > 0;
    }

    void NormalizeWeaponSlots(bool compactSlots = true)
    {
        EnsureCoreSlots();
        int slotCount = slots.Count;

        for (int i = 0; i < slotCount; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null)
            {
                slot = new WeaponSlot();
            }

            SanitizeSlot(slot);
            slots[i] = slot;
        }

        if (!compactSlots)
        {
            return;
        }

        List<WeaponSlot> compacted = new List<WeaponSlot>(slotCount);
        bool meleeIncluded = false;
        for (int i = 0; i < slotCount; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null || slot.type == WeaponType.None)
            {
                continue;
            }

            if (slot.type == WeaponType.Melee)
            {
                if (meleeIncluded)
                {
                    continue;
                }

                meleeIncluded = true;
            }

            compacted.Add(slot);
        }

        if (compacted.Count == 0)
        {
            compacted.Add(new WeaponSlot { type = WeaponType.Melee, count = -1 });
        }

        while (compacted.Count < slotCount)
        {
            compacted.Add(new WeaponSlot { type = WeaponType.None, count = -1 });
        }

        slots.Clear();
        slots.AddRange(compacted);
    }

    private static void SanitizeSlot(WeaponSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        switch (slot.type)
        {
            case WeaponType.Melee:
                slot.count = -1;
                slot.equippedPotion = null;
                return;
            case WeaponType.PotionBomb:
            {
                if (slot.equippedPotion == null)
                {
                    ClearSlot(slot);
                    return;
                }

                int qty = Mathf.Max(0, slot.equippedPotion.quantity);
                if (qty <= 0)
                {
                    ClearSlot(slot);
                    return;
                }

                slot.count = qty;
                return;
            }
            default:
                ClearSlot(slot);
                return;
        }
    }

    private static void ClearSlot(WeaponSlot slot)
    {
        slot.equippedPotion = null;
        slot.specificPrefab = null;
        slot.count = -1;
        slot.type = WeaponType.None;
    }

    private int CountUsableSlots()
    {
        int count = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null || slot.type == WeaponType.None)
            {
                continue;
            }

            if (slot.type == WeaponType.PotionBomb && (slot.equippedPotion == null || slot.equippedPotion.quantity <= 0))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawMeleeGizmo) return;
        if (tileSize <= 0f) return;

        Vector2 dir = aimDirection;
        if (dir == Vector2.zero)
        {
            dir = Vector2.down;
        }

        Vector2 forward = dir.normalized;
        Vector3 attackPos = transform.position + (Vector3)(forward * (tileSize + meleeForwardOffset));
        Vector3 attackBoxSize = new Vector3(tileSize * 2f, tileSize * 2f, 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(attackPos, attackBoxSize);
    }
}

internal static class CombatInputHelper
{
    private const KeyCode AttackKey = KeyCode.Z;
    private const int AttackMouseButton = 1;
    private static int consumedAttackInputFrame = -1;

    internal static void ConsumeAttackInputThisFrame()
    {
        consumedAttackInputFrame = Time.frameCount;
    }

    internal static bool IsAttackPressed()
    {
        if (consumedAttackInputFrame == Time.frameCount)
        {
            return false;
        }

        return Input.GetKeyDown(AttackKey) || Input.GetMouseButtonDown(AttackMouseButton);
    }

    internal static bool IsAttackReleased()
    {
        if (consumedAttackInputFrame == Time.frameCount)
        {
            return false;
        }

        return Input.GetKeyUp(AttackKey) || Input.GetMouseButtonUp(AttackMouseButton);
    }
}
