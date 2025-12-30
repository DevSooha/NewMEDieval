using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum WeaponType { None, Melee, PotionBomb }

[System.Serializable]
public class WeaponSlot
{
    public WeaponType type;
    public GameObject specificPrefab;
    // ★ [추가됨] 무기 개수 (-1이면 무제한)
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
    public GameObject bombPrefab1;
    public GameObject bombPrefab2;
    public GameObject stackMarkerPrefab;

    [Header("Weapon Slots")]
    public List<WeaponSlot> slots = new List<WeaponSlot>();

    // 내부 변수
    private Player playerMovement;
    private Animator anim;

    private Vector2 aimDirection = Vector2.down;

    // 공격 중복 방지용 변수
    private bool isAttacking = false;
    private bool isCharging = false;

    private float chargeStartTime;
    private int currentStack = 0;
    private List<GameObject> activeMarkers = new List<GameObject>();

    void Start()
    {
        playerMovement = GetComponent<Player>();
        anim = GetComponent<Animator>();

        // 타일맵 찾기 (기존 코드 유지)
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

        // 슬롯 초기화
        if (slots.Count == 0)
        {
            // ★ [수정됨] 테스트용 초기 개수 설정 (근접은 -1, 폭탄은 5개씩)
            slots.Add(new WeaponSlot { type = WeaponType.Melee, count = -1 });
            slots.Add(new WeaponSlot { type = WeaponType.PotionBomb, specificPrefab = bombPrefab1, count = 5 });
            slots.Add(new WeaponSlot { type = WeaponType.PotionBomb, specificPrefab = bombPrefab2, count = 5 });
            slots.Add(new WeaponSlot { type = WeaponType.None });
        }
    }

    void Update()
    {
        UpdateAimDirection();

        if (!isAttacking && !isCharging && Input.GetKeyDown(KeyCode.C))
        {
            RotateWeaponSlots();
        }

        // 현재 슬롯(0번)이 비어있지 않은지 확인 (안전장치)
        if (slots.Count > 0 && slots[0].type != WeaponType.None)
        {
            if (slots[0].type == WeaponType.Melee)
            {
                if (!isAttacking) HandleMeleeInput();
            }
            else if (slots[0].type == WeaponType.PotionBomb)
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
            if (Mathf.Abs(x) >= Mathf.Abs(y)) aimDirection = new Vector2(x > 0 ? 1 : -1, 0);
            else aimDirection = new Vector2(0, y > 0 ? 1 : -1);
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
        isAttacking = true;

        if (anim != null) anim.SetTrigger("IsAttack");
        yield return null;
        if (anim != null) anim.ResetTrigger("IsAttack");

        Vector2 attackPos = (Vector2)transform.position + (aimDirection * tileSize);
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, tileSize * 0.5f, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss != null) boss.TakeDamage(50, ElementType.None);
        }

        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }

    void HandleBombInput()
    {
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

            // ★ [수정됨] 폭탄 투척 시 무기 소모 로직 적용
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

            // ★ [추가됨] 현재 남은 폭탄 개수보다 더 많이 차징할 수는 없음
            if (slots[0].count != -1 && targetStack > slots[0].count)
            {
                targetStack = slots[0].count;
            }

            if (targetStack > currentStack && targetStack <= 3)
            {
                Vector2 nextPos = (Vector2)transform.position + (aimDirection * tileSize * (currentStack + 1));

                if (!IsValidTile(nextPos))
                {
                    // 설치 불가
                }
                else
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
            if (!floorTilemap.HasTile(cellPos)) return false;
        }

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(pos, tileSize * 0.3f);
        foreach (var col in hitColliders)
        {
            if (col.CompareTag("Obstacle")) return false;
            if (col.CompareTag("Stairs")) return false;
        }

        return true;
    }

    GameObject GetCurrentBombPrefab()
    {
        if (slots.Count > 0 && slots[0].specificPrefab != null)
        {
            return slots[0].specificPrefab;
        }
        return null;
    }

    // ★ [추가됨] 무기 소모 및 재정렬 함수 (기획서 P.19)
    void ConsumeWeapon(int amount)
    {
        // 근접 무기(-1)거나 빈손이면 소모 안 함
        if (slots[0].count == -1 || slots[0].type == WeaponType.None) return;

        slots[0].count -= amount;

        // 다 썼으면?
        if (slots[0].count <= 0)
        {
            Debug.Log("무기 소모됨! 다음 무기 장착");

            // 1. 현재 슬롯을 비움
            slots[0] = new WeaponSlot { type = WeaponType.None };

            // 2. 남은 무기들을 앞으로 당김 (순환 아님, 정렬임)
            ReorganizeSlots();
        }
    }

    void SpawnBombAt(int distance)
    {
        Vector2 pos = (Vector2)transform.position + (aimDirection * tileSize * distance);
        GameObject bombToSpawn = GetCurrentBombPrefab();

        if (IsValidTile(pos) && bombToSpawn != null)
        {
            Instantiate(bombToSpawn, pos, Quaternion.identity);

            // ★ [추가됨] 1개 소모
            ConsumeWeapon(1);
        }
    }

    void SpawnBombsByStack()
    {
        GameObject bombToSpawn = GetCurrentBombPrefab();
        if (bombToSpawn == null) return;

        // ★ [추가됨] 실제 던질 개수 계산 (남은 개수보다 많이 던질 수 없음)
        int throwCount = currentStack;
        if (slots[0].count != -1 && throwCount > slots[0].count)
        {
            throwCount = slots[0].count;
        }

        for (int i = 1; i <= throwCount; i++)
        {
            Vector2 pos = (Vector2)transform.position + (aimDirection * tileSize * i);
            if (!IsValidTile(pos)) break;
            Instantiate(bombToSpawn, pos, Quaternion.identity);
        }

        // ★ [추가됨] 던진 만큼 소모
        ConsumeWeapon(throwCount);
    }

    void ShowStackMarker(int index)
    {
        Vector2 pos = (Vector2)transform.position + (aimDirection * tileSize * index);
        if (!IsValidTile(pos)) return;

        if (stackMarkerPrefab != null)
        {
            GameObject marker = Instantiate(stackMarkerPrefab, pos, Quaternion.identity);
            activeMarkers.Add(marker);
        }
    }

    void ClearMarkers()
    {
        foreach (var m in activeMarkers) if (m) Destroy(m);
        activeMarkers.Clear();
    }

    // ★ [추가됨] 무기 소모 시 빈칸을 메꾸는 정렬 (C키 회전과 다름)
    // 기획서 P.19: 슬롯 1 소모 시 -> 슬롯 3이 슬롯 1로 소모 즉시 장착됨 (빈칸 건너뜀)
    void ReorganizeSlots()
    {
        List<WeaponSlot> valid = new List<WeaponSlot>();

        // 유효한 무기만 수집
        foreach (var s in slots)
        {
            if (s.type != WeaponType.None) valid.Add(s);
        }

        // 슬롯 4개를 다시 채움 (앞에서부터 채우고 나머지는 None)
        for (int i = 0; i < 4; i++)
        {
            if (i < valid.Count) slots[i] = valid[i];
            else slots[i] = new WeaponSlot { type = WeaponType.None };
        }

        Debug.Log("무기 자동 정렬 완료: " + slots[0].type);
    }

    // C키 입력 시 회전 (기존 유지)
    void RotateWeaponSlots()
    {
        List<WeaponSlot> valid = new List<WeaponSlot>();
        foreach (var s in slots) if (s.type != WeaponType.None) valid.Add(s);

        if (valid.Count <= 1) return;

        // 맨 앞 무기를 맨 뒤로 보냄 (반시계 회전)
        WeaponSlot first = valid[0];
        valid.RemoveAt(0);
        valid.Add(first);

        for (int i = 0; i < 4; i++)
        {
            if (i < valid.Count) slots[i] = valid[i];
            else slots[i] = new WeaponSlot { type = WeaponType.None };
        }

        Debug.Log($"무기 교체됨(C키): {slots[0].type}");
    }
}