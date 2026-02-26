using UnityEngine;
using UnityEngine.Tilemaps;

public class FateSeverSpearEmitterController : MonoBehaviour
{
    [Header("Ground Tilemap")]
    [SerializeField] private Tilemap groundTilemap;

    [Header("FixedY (Horizontal) Emitters - 14")]
    [SerializeField] private Transform[] fixedYEmitters = new Transform[14];

    [Header("FixedX (Vertical) Emitters - 18")]
    [SerializeField] private Transform[] fixedXEmitters = new Transform[18];

    [Header("FixedY Layout (2x2 blocks)")]
    [SerializeField] private int fixedYStartLeftX = -14;
    [SerializeField] private int fixedYStepX = 2;
    [SerializeField] private int fixedYCount = 14;
    [SerializeField] private int fixedYTopY = 8;
    [SerializeField] private int fixedYBottomY = 7;

    [Header("FixedX Layout (3x1 blocks)")]
    [SerializeField] private int fixedXLeftX = 11;     // 11,12,13
    [SerializeField] private int fixedXWidth = 3;      // 3칸(설명용, Anchor 계산에서 실제로 3칸 평균냄)
    [SerializeField] private int fixedXStartY = 8;     // 위에서 시작
    [SerializeField] private int fixedXEndY = -9;      // 아래 끝
    [SerializeField] private int fixedXCount = 18;     // 8..-9 inclusive = 18

    private void Awake()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null)
                groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }
    }

    // =========================================
    // ✅ 플레이어 셀 -> 인덱스 변환
    // =========================================

    /// <summary>
    /// FixedY(가로 14칸): playerCellX 기준 2칸 단위 블록 인덱스
    /// px=-14,-13 -> 0 / -12,-11 -> 1 / ... / 12,13 -> 13
    /// </summary>
    public int GetFixedYIndexFromPlayerCellX(int playerCellX)
    {
        int count = (fixedYEmitters != null && fixedYEmitters.Length > 0) ? fixedYEmitters.Length : fixedYCount;

        int idx = Mathf.FloorToInt((playerCellX - fixedYStartLeftX) / (float)fixedYStepX);
        return Mathf.Clamp(idx, 0, count - 1);
    }

    /// <summary>
    /// FixedX(세로 18칸): playerCellY를 8..-9로 클램프 후
    /// y=8 -> 0, y=7 -> 1, ... y=-9 -> 17
    /// </summary>
    public int GetFixedXIndexFromPlayerCellY(int playerCellY)
    {
        int count = (fixedXEmitters != null && fixedXEmitters.Length > 0) ? fixedXEmitters.Length : fixedXCount;

        int clampedY = Mathf.Clamp(playerCellY, fixedXEndY, fixedXStartY);
        int idx = fixedXStartY - clampedY;
        return Mathf.Clamp(idx, 0, count - 1);
    }

    // =========================================
    // ✅ 에미터 Transform 얻기 (인스펙터 기반)
    // =========================================

    public Transform GetFixedYEmitter(int index)
    {
        if (fixedYEmitters == null || fixedYEmitters.Length == 0) return null;
        index = Mathf.Clamp(index, 0, fixedYEmitters.Length - 1);
        return fixedYEmitters[index];
    }

    public Transform GetFixedXEmitter(int index)
    {
        if (fixedXEmitters == null || fixedXEmitters.Length == 0) return null;
        index = Mathf.Clamp(index, 0, fixedXEmitters.Length - 1);
        return fixedXEmitters[index];
    }

    // =========================================
    // ✅ 좌표 기반 앵커 (에미터 없이도 가능)
    // =========================================

    public Vector3 GetFixedYAnchorWorld_ByIndex(int index)
    {
        if (groundTilemap == null) return Vector3.zero;

        int count = (fixedYEmitters != null && fixedYEmitters.Length > 0) ? fixedYEmitters.Length : fixedYCount;
        index = Mathf.Clamp(index, 0, count - 1);

        int leftX = fixedYStartLeftX + index * fixedYStepX;

        Vector3 c1 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX,     fixedYTopY, 0));
        Vector3 c2 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX + 1, fixedYTopY, 0));
        Vector3 c3 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX,     fixedYBottomY, 0));
        Vector3 c4 = groundTilemap.GetCellCenterWorld(new Vector3Int(leftX + 1, fixedYBottomY, 0));

        return (c1 + c2 + c3 + c4) * 0.25f;
    }

    public Vector3 GetFixedXAnchorWorld_ByIndex(int index)
    {
        if (groundTilemap == null) return Vector3.zero;

        int count = (fixedXEmitters != null && fixedXEmitters.Length > 0) ? fixedXEmitters.Length : fixedXCount;
        index = Mathf.Clamp(index, 0, count - 1);

        int y = fixedXStartY - index;
        int width = Mathf.Max(1, fixedXWidth);
        Vector3 sum = Vector3.zero;

        for (int i = 0; i < width; i++)
        {
            sum += groundTilemap.GetCellCenterWorld(new Vector3Int(fixedXLeftX + i, y, 0));
        }

        return sum / width;
    }
}
