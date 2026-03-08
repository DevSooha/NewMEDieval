using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HandOfTime : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;

    [Header("Projectile Prefabs")]
    [SerializeField] private HandOfTimeProjectile prefab1x3;
    [SerializeField] private HandOfTimeProjectile prefab2x2;

    [Header("Timing")]
    [SerializeField] private float castTime = 0.5f;

    [Header("Speed")]
    [SerializeField] private float speedWorldPerSec = 5f;

    // Tilemap bounds
    private const int MIN_X = -14;
    private const int MAX_X = 13;
    private const int MIN_Y = -8;
    private const int MAX_Y = 8;

    private void Awake()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null)
                groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }
    }

    public IEnumerator PlayOnce(Transform playerTF)
    {
        Debug.Log("HandOfTime PlayOnce START");

        if (playerTF == null || groundTilemap == null)
        {
            Debug.LogError("HandOfTime: playerTF or groundTilemap NULL");
            yield break;
        }

        yield return new WaitForSeconds(castTime);

        // =========================
        // 플레이어 타일 좌표
        // =========================

        Vector3Int playerCell = groundTilemap.WorldToCell(playerTF.position);

        int px = Mathf.Clamp(playerCell.x, MIN_X, MAX_X);
        int py = Mathf.Clamp(playerCell.y, MIN_Y, MAX_Y);

        // =========================
        // 2x2 탄 (위에서 내려오기)
        // =========================

        int laneLeftX = Mathf.FloorToInt(px / 2f) * 2;

        int topY = MAX_Y;
        int bottomY = MAX_Y - 1;

        // 2x2 중심 계산
        Vector3 center2x2 =
        (
            groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX, topY, 0)) +
            groundTilemap.GetCellCenterWorld(new Vector3Int(laneLeftX + 1, bottomY, 0))
        ) * 0.5f;

        // spawn 위치 (맵 바로 위)
        float spawnY =
            groundTilemap.GetCellCenterWorld(new Vector3Int(0, MAX_Y + 1, 0)).y;

        Vector3 spawn2x2 = new Vector3(center2x2.x, spawnY, 0f);

        // =========================
        // 1x3 탄 (오른쪽에서 들어오기)
        // =========================

        Vector3 center1x3 =
            groundTilemap.GetCellCenterWorld(new Vector3Int(px, py, 0));

        float spawnX =
            groundTilemap.GetCellCenterWorld(new Vector3Int(MAX_X + 1, 0, 0)).x;

        Vector3 spawn1x3 = new Vector3(spawnX, center1x3.y, 0f);

        // =========================
        // Debug
        // =========================

        Debug.Log("spawn2x2 = " + spawn2x2);
        Debug.Log("spawn1x3 = " + spawn1x3);

        Debug.DrawLine(spawn2x2, spawn2x2 + Vector3.down * 5f, Color.red, 2f);
        Debug.DrawLine(spawn1x3, spawn1x3 + Vector3.left * 5f, Color.green, 2f);

        // =========================
        // 생성
        // =========================

        HandOfTimeProjectile p2x2 =
            Instantiate(prefab2x2, spawn2x2, Quaternion.identity);

        HandOfTimeProjectile p1x3 =
            Instantiate(prefab1x3, spawn1x3, Quaternion.identity);

        // =========================
        // 발사
        // =========================

        p2x2.BeginFire(
            p2x2.transform.position + Vector3.down,
            speedWorldPerSec,
            HandOfTimeProjectile.Axis.Vertical
        );

        p1x3.BeginFire(
            p1x3.transform.position + Vector3.left,
            speedWorldPerSec,
            HandOfTimeProjectile.Axis.Horizontal
        );
    }
}