using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HandOfTime : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private FateSeverSpearEmitterController emitterCtrl;

    [Header("Projectile Prefabs")]
    [SerializeField] private HandOfTimeProjectile prefab1x3;
    [SerializeField] private HandOfTimeProjectile prefab2x2;

    [Header("Timing")]
    [SerializeField] private float castTime = 0.5f;

    [Header("Speed")]
    [SerializeField] private float speedWorldPerSec = 5f;

    private void Awake()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null)
                groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }

        if (emitterCtrl == null)
        {
            emitterCtrl = FindObjectOfType<FateSeverSpearEmitterController>();
        }
    }

    public IEnumerator PlayOnce(Transform playerTF)
    {
        if (playerTF == null || groundTilemap == null)
            yield break;

        // 시전 모션
        yield return new WaitForSeconds(castTime);

        // 플레이어 셀 좌표
        Vector3Int playerCell = groundTilemap.WorldToCell(playerTF.position);

        // =========================
        // 2x2 위에서 내려오는 탄
        // =========================

        int idxY = emitterCtrl.GetFixedYIndexFromPlayerCellX(playerCell.x);

        Transform emitterY = emitterCtrl.GetFixedYEmitter(idxY);

        Vector3 spawn2x2 = emitterY.position;

        // =========================
        // 1x3 오른쪽에서 들어오는 탄
        // =========================

        int idxX = emitterCtrl.GetFixedXIndexFromPlayerCellY(playerCell.y);

        Transform emitterX = emitterCtrl.GetFixedXEmitter(idxX);

        Vector3 spawn1x3 = emitterX.position;

        Debug.Log($"[HandOfTime] spawn2x2 = {spawn2x2}");
        Debug.Log($"[HandOfTime] spawn1x3 = {spawn1x3}");

        // 생성
        HandOfTimeProjectile p2 =
            Instantiate(prefab2x2, spawn2x2, Quaternion.identity);

        HandOfTimeProjectile p1 =
            Instantiate(prefab1x3, spawn1x3, Quaternion.identity);

        // 발사
        p2.BeginFire(
            p2.transform.position + Vector3.down,
            speedWorldPerSec,
            HandOfTimeProjectile.Axis.Vertical
        );

        p1.BeginFire(
            p1.transform.position + Vector3.left,
            speedWorldPerSec,
            HandOfTimeProjectile.Axis.Horizontal
        );
    }
}