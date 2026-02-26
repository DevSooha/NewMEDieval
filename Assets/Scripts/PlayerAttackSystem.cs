using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum WeaponType { None, Melee, PotionBomb }

[System.Serializable]
public class WeaponSlot
{
    public WeaponType type;
    //public ItemData itemData; // ������ ���� ������ �߰�
    public GameObject specificPrefab; // ���� ��ü ������

    // -1�̸� ������ (��������), ����� �Ҹ�ǰ
    public int count = -1;
}

public class PlayerAttackSystem : MonoBehaviour
{
    [Header("Settings")]
    public float tileSize = 1.0f;
    public LayerMask enemyLayer;

    [Header("Tilemaps")]
    public Tilemap floorTilemap;

    [Header("Prefabs")]
    //public GameObject defaultBombPrefab; // �⺻ ��ź ������
    public GameObject stackMarkerPrefab;

    [Header("Weapon Slots")]
    public List<WeaponSlot> slots = new();

    // ������Ʈ ĳ��
    private Player playerMovement;

    private Vector2 aimDirection = Vector2.down;

    // ���� ����
    private bool isAttack = false;
    private bool isCharging = false;

    private float chargeStartTime;
    private int currentStack = 0;
    private List<GameObject> activeMarkers = new();

    private PlayerInteraction interactionSensor;

    void Start()
    {
        playerMovement = GetComponent<Player>();

        // Ÿ�ϸ� �ڵ� ã��
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

        // �ʱ� ������ ��������� �⺻�� ���� (�׽�Ʈ��)
        if (slots.Count == 0)
        {
            slots.Add(new WeaponSlot { type = WeaponType.Melee, count = -1 });
        }

        interactionSensor = GetComponentInChildren<PlayerInteraction>();
    }

    void Update()
    {
        UpdateAimDirection();

        // NPC ��ȭ ���̸� ���� �Ұ�
        if (interactionSensor != null && interactionSensor.IsInteractable)
        {
            return;
        }

        // ���� ��ü (CŰ)
        if (!isAttack && !isCharging && Input.GetKeyDown(KeyCode.C))
        {
            RotateWeaponSlots();
        }

        // ���� ������ ���� ���
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

    // [�߰�] �κ��丮���� ������ �����ϴ� �Լ�
    //public void EquipPotionFromInventory(Item item)
    //{
    //    if (item == null || item.data == null) return;

    //    // 1. �� ���� ���� ����
    //    WeaponSlot newSlot = new WeaponSlot();
    //    newSlot.type = WeaponType.PotionBomb;
    //    newSlot.itemData = item.data;
    //    newSlot.count = item.quantity; // ���� ���� �ݿ�

    //    // ������ ���� (ItemData�� �������� �ִٰ� �����ϰų� �⺻�� ���)
    //    // ���� ItemData�� ������ �������� ���ٸ� defaultBombPrefab ���
    //    newSlot.specificPrefab = defaultBombPrefab;

    //    // 2. ���� ����(0��)�� ��ü (�Ǵ� ��Ͽ� �߰�)
    //    // ���⼭�� "0�� ������ ������ ���"���� ����
    //    if (slots.Count > 0)
    //    {
    //        slots[0] = newSlot;
    //    }
    //    else
    //    {
    //        slots.Add(newSlot);
    //    }

    //    Debug.Log($"���� ����: {item.data.name} ({item.quantity}��)");
    //}

    void UpdateAimDirection()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        if (x != 0 || y != 0)
        {
            // .normalized�� �ٿ� �밢���� �� ���̰� 1���� Ŀ���� ���� ����
            aimDirection = new Vector2(x, y).normalized;
        }
    }

    void HandleMeleeInput()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            StartCoroutine(MeleeAttackRoutine());
        }
    }

    IEnumerator MeleeAttackRoutine()
    {
        isAttack = true;

        Vector2 attackPos = (Vector2)transform.position + (aimDirection * tileSize);

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, tileSize * 0.7f, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            // �� �ǰ� ó��
            EnemyCombat enemy = hit.GetComponent<EnemyCombat>();
            if (enemy != null)
            {
                enemy.EnemyTakeDamage(50);
                continue;
            }

            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss != null)
            {
                boss.TakeDamage(3000, ElementType.None);
            }
        }

        yield return new WaitForSeconds(0.4f);
        isAttack = false;
    }

    void HandleBombInput()
    {
        // ���� ���� üũ
        if (slots[0].count == 0)
        {
            Debug.Log("������ �� ���������ϴ�!");
            return;
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            isCharging = true;
            chargeStartTime = Time.time;
            currentStack = 0;
            if (playerMovement != null) playerMovement.SetCanMove(false);
            StartCoroutine(ChargeRoutine());
        }

        if (Input.GetKeyUp(KeyCode.Z))
        {
            isCharging = false;
            StopAllCoroutines();
            if (playerMovement != null) playerMovement.SetCanMove(true);

            float duration = Time.time - chargeStartTime;

            // ª�� ������ 1ĭ, ��� ������ ���ø�ŭ
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

            // ���� �������� ���� ���� �� ����
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
            // Ÿ���� �����ؾ� ���� �� ���� (���̳� ��� ����)
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

        // 1. ������ ���� (�ν����Ϳ� ��ϵ� defaultBombPrefab Ȥ�� ���Ժ� ������)
        //GameObject prefabToUse = slots[0].specificPrefab != null ? slots[0].specificPrefab : defaultBombPrefab;

        //if (prefabToUse != null)
        //{
            //GameObject bombObj = Instantiate(prefabToUse, spawnPos, Quaternion.identity);

            // 2. ������ ������Ʈ���� Bomb ������Ʈ ��������
            //Bomb bombScript = bombObj.GetComponent<Bomb>();

            // 3. �� �ٽ�: PotionData ���� (�� �κ��� �־�� ���ο� ������ �۵��մϴ�)
            //if (bombScript != null)
            //{
            //// ���� ������ itemData�� PotionData�� ����ȯ�Ͽ� ����
            //if (slots[0].itemData is PotionData pData)
            //{
            //    bombScript.Initialize(pData);
            //}
            //else
            //{
            //    Debug.LogError("���� ������ �������� PotionData ������ �ƴմϴ�!");
            //}
            //}

        //    UseAmmo(1);
        //}
    }

    void SpawnBombsByStack()
    {
        if (currentStack == 0)
        {
            SpawnBombAt(1);
            return;
        }

        // ���� 1, 2, 3 ��ġ�� ���������� ����
        for (int i = 1; i <= currentStack; i++)
        {
            SpawnBombAt(i);
        }
    }

    // ź�� �Ҹ� ó��
    void UseAmmo(int amount)
    {
        if (slots[0].count == -1) return; // ���� źâ

        slots[0].count -= amount;

        // �κ��丮 �����Ϳ� ����ȭ (���� ����)
        //if (Inventory.Instance != null && slots[0].itemData != null)
        //{
        //    // �κ��丮������ ������ ���̷��� Inventory�� RemoveItem ������ �ʿ���
        //    // ����� PlayerAttackSystem ���� �󿡼��� �پ��
        //}

        if (slots[0].count <= 0)
        {
            slots[0].count = 0;
            //�� ���� �Ǽ�(Melee)���� ��ȯ���� ���� ����
            slots[0].type = WeaponType.Melee;
        }
    }

    void RotateWeaponSlots()
    {
        if (slots.Count <= 1) return;

        WeaponSlot first = slots[0];
        slots.RemoveAt(0);
        slots.Add(first);
        Debug.Log($"���� ��ü��: {slots[0].type}");
    }
}


