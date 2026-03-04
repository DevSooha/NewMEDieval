using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HandOfTime : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private FateSeverSpearEmitterController emitterController; // 인덱스 계산만 써도 되고, 없어도 됨

    [Header("Projectile Prefabs (2 types)")]
    [SerializeField] private HandOfTimeProjectile prefab1x3;
    [SerializeField] private HandOfTimeProjectile prefab2x2;

    public enum ShotType { Long1x3, Square2x2 }
    [SerializeField] private ShotType shotType = ShotType.Long1x3;

    [Header("Timing")]
    [SerializeField] private float castTime = 0.5f;
    [SerializeField] private float fadeInTime = 0.5f;

    [Header("Speed")]
    [SerializeField] private float speedWorldPerSec = 5f;

    // =========================
    // ✅ 너가 준 “좌표 규칙” 고정값
    // =========================

    [Header("Horizontal (2x2) Layout")]
    [SerializeField] private int hStartLeftX = -14;   // -14, -12, ... 12
    [SerializeField] private int hStepX = 2;
    [SerializeField] private int hCount = 14;         // 14 blocks
    [SerializeField] private int hTopY = 8;           // 2x2의 윗줄
    [SerializeField] private int hBottomY = 7;        // 2x2의 아랫줄

    [Header("Vertical (3-wide) Layout")]
    [SerializeField] private int vLeftX = 11;         // 11,12,13
    [SerializeField] private int vWidth = 3;          // 3칸 폭
    [SerializeField] private int vMinY = -8;          // -8..8
    [SerializeField] private int vMaxY = 8;

    private void Awake()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null) groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }

        if (emitterController == null)
            emitterController = FindAnyObjectByType<FateSeverSpearEmitterController>();
    }

    private HandOfTimeProjectile GetPrefab()
    {
        return shotType == ShotType.Long1x3 ? prefab1x3 : prefab2x2;
    }

    public IEnumerator PlayOnce(Transform playerTF)
    {
        Debug.Log($"[HandOfTime] groundTilemap.name={groundTilemap.name}");
        Debug.Log($"[HandOfTime] groundTilemap path={GetPath(groundTilemap.transform)}");
        Debug.Log($"[HandOfTime] grid={groundTilemap.layoutGrid.name} gridPos={groundTilemap.layoutGrid.transform.position}");
        
        Debug.Log("[HandOfTime] PlayOnce start");

        if (playerTF == null)
        {
            Debug.LogWarning("[HandOfTime] playerTF is NULL");
            yield break;
        }

        if (groundTilemap == null)
        {
            Debug.LogWarning("[HandOfTime] groundTilemap is NULL");
            yield break;
        }

        var prefab = GetPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[HandOfTime] Projectile prefab is NULL");
            yield break;
        }

        Debug.Log("[HandOfTime] Casting start");
        yield return new WaitForSeconds(castTime);

        // 1) 플레이어 셀 락
        Vector3Int lockedCell = groundTilemap.WorldToCell(playerTF.position);
        Debug.Log($"[HandOfTime] Player cell locked = {lockedCell}");

        // 2) ✅ 네 규칙대로 인덱스/셀 계산
        // 가로(2x2): playerCellX -> 2칸 단위 block index(0..13) -> leftX
        int hIdx = GetHorizontalIndexFromPlayerCellX(lockedCell.x);
        int laneLeftX = hStartLeftX + hIdx * hStepX;

        // 세로(3-wide): playerCellY -> yCell (-8..8)
        int yCell = Mathf.Clamp(lockedCell.y, vMinY, vMaxY);

        // 3) 스폰 앵커 월드좌표
        Vector3 spawnPosHorizontal = GetHorizontalAnchorWorld_2x2(laneLeftX);
        Vector3 spawnPosVertical   = GetVerticalAnchorWorld_3wide(yCell);

        Debug.Log($"[HandOfTime] hIdx={hIdx} laneLeftX={laneLeftX} -> spawnH={spawnPosHorizontal}");
        Debug.Log($"[HandOfTime] yCell={yCell} -> spawnV={spawnPosVertical}");

        // 4) 생성 + 페이드인
        HandOfTimeProjectile pH = Instantiate(prefab, spawnPosHorizontal, Quaternion.identity);
        HandOfTimeProjectile pV = Instantiate(prefab, spawnPosVertical, Quaternion.identity);

        // ✅ (선택) 판정 크기를 셀 규칙대로 맞추고 싶으면 여기서 세팅 (아래 ConfigureCollider 참고)
        // pH.ConfigureCollider(groundTilemap, 2, 2);  // 2x2 판정
        // pV.ConfigureCollider(groundTilemap, 3, 1);  // 3x1 판정(가로 3칸, 세로 1칸)

        pH.BeginFadeIn(fadeInTime);
        pV.BeginFadeIn(fadeInTime);

        Debug.Log("[HandOfTime] FadeIn started");
        yield return new WaitForSeconds(fadeInTime);

        // 5) 발사 직전 플레이어 월드 좌표 재획득
        Vector3 fireLockWorld = playerTF.position;
        Debug.Log($"[HandOfTime] Fire target world = {fireLockWorld}");

        // 6) 축 고정 발사
        pH.BeginFire(fireLockWorld, speedWorldPerSec, HandOfTimeProjectile.Axis.Horizontal);
        pV.BeginFire(fireLockWorld, speedWorldPerSec, HandOfTimeProjectile.Axis.Vertical);

        Debug.Log("[HandOfTime] BeginFire called");
    }

    // =========================
    // ✅ 인덱스 / 앵커 계산 (네 규칙 그대로)
    // =========================

    private int GetHorizontalIndexFromPlayerCellX(int playerCellX)
    {
        int idx = Mathf.FloorToInt((playerCellX - hStartLeftX) / (float)hStepX);
        return Mathf.Clamp(idx, 0, hCount - 1);
    }

    // 2x2 블록 중앙: [leftX,8][leftX+1,8][leftX,7][leftX+1,7] 평균
    private Vector3 GetHorizontalAnchorWorld_2x2(int leftX)
    {
        Vector3 c1 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX,     hTopY, 0));
        Vector3 c2 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX + 1, hTopY, 0));
        Vector3 c3 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX,     hBottomY, 0));
        Vector3 c4 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX + 1, hBottomY, 0));
        return (c1 + c2 + c3 + c4) * 0.25f;
    }

    // 3칸 폭 중앙: [11,y][12,y][13,y] 평균
    private Vector3 GetVerticalAnchorWorld_3wide(int yCell)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < vWidth; i++)
            sum += groundTilemap.GetCellCenterWorld(new Vector3Int(vLeftX + i, yCell, 0));
        return sum / Mathf.Max(1, vWidth);
    }
    
    private string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}