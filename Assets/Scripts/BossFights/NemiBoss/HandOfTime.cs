using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HandOfTime : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private FateSeverSpearEmitterController emitterController;

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
        if (playerTF == null || groundTilemap == null || emitterController == null)
            yield break;

        var prefab = GetPrefab();
        if (prefab == null) yield break;

        // 1) 캐스팅 (이 동안 좌표 확정 X)
        yield return new WaitForSeconds(castTime);

        // 2) 등장 시점 플레이어 셀 락 (타일맵 기준)
        Vector3Int spawnLockCell = groundTilemap.WorldToCell(playerTF.position);

        int idxY = emitterController.GetFixedYIndexFromPlayerCellX(spawnLockCell.x);
        Transform emitY = emitterController.GetFixedYEmitter(idxY);

        int idxX = emitterController.GetFixedXIndexFromPlayerCellY(spawnLockCell.y);
        Transform emitX = emitterController.GetFixedXEmitter(idxX);

        if (emitY == null || emitX == null) yield break;

        // 3) 2발 생성 + 페이드인(충돌 OFF)
        HandOfTimeProjectile pHorizontal = Instantiate(prefab, emitY.position, Quaternion.identity);
        HandOfTimeProjectile pVertical   = Instantiate(prefab, emitX.position, Quaternion.identity);

        pHorizontal.BeginFadeIn(fadeInTime);
        pVertical.BeginFadeIn(fadeInTime);

        // 4) 페이드인 끝나는 순간까지 대기 (딱 1번)
        yield return new WaitForSeconds(fadeInTime);

        // 5) 발사 직전 플레이어 월드 좌표 재획득
        Vector3 fireLockWorld = playerTF.position;

        // 6) 축 고정 발사 + 충돌 ON (Projectile 내부)
        pHorizontal.BeginFire(fireLockWorld, speedWorldPerSec, HandOfTimeProjectile.Axis.Horizontal);
        pVertical.BeginFire(fireLockWorld, speedWorldPerSec, HandOfTimeProjectile.Axis.Vertical);
    }
}