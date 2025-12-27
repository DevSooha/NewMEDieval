using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum WeaponType { None, Melee, PotionBomb }

[System.Serializable]
public class WeaponSlot
{
    public WeaponType type;
    public GameObject specificPrefab; // ★ 이 슬롯이 사용할 전용 폭탄 프리팹
}

public class PlayerAttackSystem : MonoBehaviour
{
    [Header("Settings")]
    public float tileSize = 1.0f;
    public LayerMask enemyLayer;

    [Header("Tilemaps")]
    public Tilemap floorTilemap;

    [Header("Prefabs (인스펙터에서 넣어주세요)")]
    // ★ 종류별 폭탄 프리팹을 여기에 등록해둡니다.
    public GameObject bombPrefab1; // 빨간 폭탄?
    public GameObject bombPrefab2; // 파란 폭탄?
    public GameObject stackMarkerPrefab;

    [Header("Weapon Slots")]
    public List<WeaponSlot> slots = new List<WeaponSlot>();

    // 내부 변수
    private Player playerMovement;
    private Animator anim;

    private Vector2 aimDirection = Vector2.down;
    private bool isCharging = false;
    private float chargeStartTime;
    private int currentStack = 0;
    private List<GameObject> activeMarkers = new List<GameObject>();

    void Start()
    {
        playerMovement = GetComponent<Player>();
        anim = GetComponent<Animator>();

        // 타일맵 자동 찾기 (기존 코드 유지)
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

        // ★ 슬롯 초기화 부분 수정
        // 슬롯마다 서로 다른 폭탄 프리팹을 넣어줍니다.
        if (slots.Count == 0)
        {
            // 0번 슬롯: 근접 무기
            slots.Add(new WeaponSlot { type = WeaponType.Melee });

            // 1번 슬롯: 폭탄 1번 (예: 일반 폭탄)
            slots.Add(new WeaponSlot { type = WeaponType.PotionBomb, specificPrefab = bombPrefab1 });

            // 2번 슬롯: 폭탄 2번 (예: 얼음 폭탄) - 테스트를 위해 추가해봄
            slots.Add(new WeaponSlot { type = WeaponType.PotionBomb, specificPrefab = bombPrefab2 });

            // 3번 슬롯: 비움
            slots.Add(new WeaponSlot { type = WeaponType.None });
        }
    }

    void Update()
    {
        UpdateAimDirection();

        if (Input.GetKeyDown(KeyCode.C)) RotateWeaponSlots();

        // 현재(0번) 슬롯의 타입에 따라 행동 결정
        if (slots[0].type == WeaponType.Melee) HandleMeleeInput();
        else if (slots[0].type == WeaponType.PotionBomb) HandleBombInput();
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
            if (anim != null) anim.SetTrigger("Attack");

            Vector2 attackPos = (Vector2)transform.position + (aimDirection * tileSize);
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPos, tileSize * 0.5f, enemyLayer);

            foreach (Collider2D hit in hits)
            {
                BossHealth boss = hit.GetComponent<BossHealth>();
                if (boss != null) boss.TakeDamage(50, ElementType.None);
            }
        }
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

            if (targetStack > currentStack && targetStack <= 3)
            {
                Vector2 nextPos = (Vector2)transform.position + (aimDirection * tileSize * (currentStack + 1));

                if (!IsValidTile(nextPos))
                {
                    Debug.Log("설치 불가 지역");
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

    // ★ 현재 장착된 슬롯(slots[0])의 프리팹을 가져오는 헬퍼 함수
    GameObject GetCurrentBombPrefab()
    {
        if (slots.Count > 0 && slots[0].specificPrefab != null)
        {
            return slots[0].specificPrefab;
        }
        return null; // 프리팹이 없으면 null 반환
    }

    void SpawnBombAt(int distance)
    {
        Vector2 pos = (Vector2)transform.position + (aimDirection * tileSize * distance);

        // ★ 현재 슬롯에 등록된 폭탄 프리팹 가져오기
        GameObject bombToSpawn = GetCurrentBombPrefab();

        if (IsValidTile(pos) && bombToSpawn != null)
        {
            Instantiate(bombToSpawn, pos, Quaternion.identity);
        }
    }

    void SpawnBombsByStack()
    {
        // ★ 현재 슬롯에 등록된 폭탄 프리팹 가져오기
        GameObject bombToSpawn = GetCurrentBombPrefab();

        if (bombToSpawn == null) return;

        for (int i = 1; i <= currentStack; i++)
        {
            Vector2 pos = (Vector2)transform.position + (aimDirection * tileSize * i);

            if (!IsValidTile(pos)) break;

            Instantiate(bombToSpawn, pos, Quaternion.identity);
        }
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

    void RotateWeaponSlots()
    {
        // 빈 슬롯은 제외하고 회전
        List<WeaponSlot> valid = new List<WeaponSlot>();
        foreach (var s in slots) if (s.type != WeaponType.None) valid.Add(s);

        if (valid.Count <= 1) return;

        WeaponSlot first = valid[0];
        valid.RemoveAt(0);
        valid.Add(first);

        // 다시 슬롯 배열에 적용
        for (int i = 0; i < 4; i++)
        {
            if (i < valid.Count) slots[i] = valid[i];
            else slots[i] = new WeaponSlot { type = WeaponType.None };
        }

        Debug.Log($"무기 교체됨: {slots[0].type} / 프리팹: {slots[0].specificPrefab?.name}");
    }
}