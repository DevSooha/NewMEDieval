using System.Collections;
using System.Collections.Generic;
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
    private readonly List<GameObject> activeMarkers = new();


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

        EnsureCoreSlots();

        // Weapon status UI removed.
    }

    void Update()
    {
        UpdateAimDirection();
        EnsureCoreSlots();
        SyncPotionSlotCounts();

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

        if (IsAttackPressed())
        {
            isCharging = true;
            chargeStartTime = Time.time;
            currentStack = 0;
            if (playerMovement != null) playerMovement.SetCanMove(false);
            StartCoroutine(ChargeRoutine());
        }

        if (IsAttackReleased())
        {
            isCharging = false;
            StopAllCoroutines();
            if (playerMovement != null) playerMovement.SetCanMove(true);

            float duration = Time.time - chargeStartTime;
            if (duration < 0.5f) SpawnBombAt(1);
            else SpawnBombsByStack();

            ClearMarkers();
        }
    }

    IEnumerator ChargeRoutine()
    {
        while (isCharging)
        {
            float t = Time.time - chargeStartTime;
            int targetStack = 0;
            if (t >= 1.5f) targetStack = 3;
            else if (t >= 1.0f) targetStack = 2;
            else if (t >= 0.5f) targetStack = 1;

            if (slots[0].count != -1 && targetStack > slots[0].count)
            {
                targetStack = slots[0].count;
            }

            if (targetStack > currentStack && targetStack <= 3)
            {
                Vector2 nextPos = (Vector2)transform.position + ((currentStack + 1) * tileSize * aimDirection);

                if (IsValidTile(nextPos))
                {
                    currentStack = targetStack;
                    ShowStackMarker(currentStack);
                }
            }

            yield return null;
        }
    }

    bool IsValidTile(Vector2 pos)
    {
        if (floorTilemap != null)
        {
            Vector3Int cellPos = floorTilemap.WorldToCell(pos);
            return floorTilemap.HasTile(cellPos);
        }
        return true;
    }

    void ShowStackMarker(int stackIndex)
    {
        if (stackMarkerPrefab == null) return;

        Vector2 spawnPos = (Vector2)transform.position + (stackIndex * tileSize * aimDirection);
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

    void SpawnBombAt(int distance)
    {
        if (slots.Count == 0) return;

        Vector2 spawnPos = (Vector2)transform.position + (distance * tileSize * aimDirection);
        if (!IsValidTile(spawnPos)) return;

        WeaponSlot slot = slots[0];
        GameObject prefabToUse = slot.specificPrefab != null ? slot.specificPrefab : defaultBombPrefab;
        if (prefabToUse == null) return;

        GameObject bombObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
        Bomb bomb = bombObj.GetComponent<Bomb>();
        if (bomb != null && slot.equippedPotion != null && slot.equippedPotion.data != null)
        {
            bomb.baseDamage = slot.equippedPotion.data.damage1 + slot.equippedPotion.data.damage2;
            bomb.bombElement = ConvertElement(slot.equippedPotion.data.element1);
        }

        UseAmmo(1);
    }

    void SpawnBombsByStack()
    {
        if (currentStack == 0)
        {
            SpawnBombAt(1);
            return;
        }

        for (int i = 1; i <= currentStack; i++)
        {
            SpawnBombAt(i);
        }
    }

    void UseAmmo(int amount)
    {
        if (slots.Count == 0) return;

        WeaponSlot slot = slots[0];
        if (slot.type != WeaponType.PotionBomb) return;

        if (slot.equippedPotion != null)
        {
            slot.equippedPotion.quantity -= amount;
            if (slot.equippedPotion.quantity <= 0)
            {
                RemovePotionFromInventory(slot.equippedPotion);
                slot.equippedPotion = null;
                slot.type = WeaponType.Melee;
                slot.count = -1;
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
            }

            if (slot.count <= 0)
            {
                slot.type = WeaponType.Melee;
                slot.count = -1;
            }
        }

    }

    void RotateWeaponSlots()
    {
        if (slots.Count <= 1) return;

        CompactSlots();

        int currentIndex = 0;
        int nextIndex = FindNextNonEmptyIndex(currentIndex);
        if (nextIndex <= 0) return;
        RotateListToIndex(nextIndex);
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
        if (slots.Count <= 1) return;

        List<WeaponSlot> compacted = new List<WeaponSlot>(slots.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].type != WeaponType.None)
            {
                compacted.Add(slots[i]);
            }
        }

        int emptyCount = slots.Count - compacted.Count;
        for (int i = 0; i < emptyCount; i++)
        {
            compacted.Add(new WeaponSlot { type = WeaponType.None, count = -1 });
        }

        slots.Clear();
        slots.AddRange(compacted);
    }

    bool IsAttackPressed()
    {
        return Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(1);
    }

    bool IsAttackReleased()
    {
        return Input.GetKeyUp(KeyCode.Z) || Input.GetMouseButtonUp(1);
    }

    void SyncPotionSlotCounts()
    {
        EnsureCoreSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot.type != WeaponType.PotionBomb || slot.equippedPotion == null)
                continue;

            int qty = Mathf.Max(0, slot.equippedPotion.quantity);
            if (slot.count != qty)
            {
                slot.count = qty;
            }

            if (qty == 0)
            {
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.equippedPotion = null;
            }
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

        List<Potion> list = inv.PotionItems;
        int idx = list.IndexOf(potion);
        if (idx >= 0)
        {
            list.RemoveAt(idx);
        }
    }

    void EnsureCoreSlots()
    {
        if (slots.Count == 0)
        {
            slots.Add(new WeaponSlot { type = WeaponType.Melee, count = -1 });
        }

        if (slots[0].type != WeaponType.Melee && slots[0].type != WeaponType.PotionBomb)
        {
            slots[0].type = WeaponType.Melee;
            slots[0].count = -1;
            slots[0].equippedPotion = null;
        }

        while (slots.Count < 4)
        {
            slots.Add(new WeaponSlot { type = WeaponType.None, count = -1 });
        }
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
