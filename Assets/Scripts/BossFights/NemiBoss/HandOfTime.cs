using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HandOfTime : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private FateSeverSpearEmitterController emitterController;

    [Header("Projectile")]
    [SerializeField] private HandOfTimeProjectile projectilePrefab;

    [Header("Timing")]
    [SerializeField] private float castTime = 0.5f;     // 시전 모션
    [SerializeField] private float fadeInTime = 0.5f;   // 등장(페이드인) 시간

    [Header("Speed")]
    [SerializeField] private float speedWorldPerSec = 5f; // 일단 월드 속도(원하면 타일 기준으로 바꿔도 됨)

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

    /// <summary>
    /// ✅ 요구사항 반영:
    /// - "철커덩 등장 시점"의 플레이어 셀 좌표로 스폰 위치(에미터) 결정
    /// - 페이드인 0.5초 동안엔 충돌 OFF (겹쳐도 무시)
    /// - 페이드인 끝나는 "발사 직전" 플레이어 월드 좌표를 다시 받아서 발사 방향 결정
    /// </summary>
    public IEnumerator PlayOnce(Transform playerTF)
    {
        if (playerTF == null || groundTilemap == null || emitterController == null || projectilePrefab == null)
            yield break;

        // 1) 캐스팅(0.5초) - 이 동안 플레이어 좌표 확정 X
        yield return new WaitForSeconds(castTime);

        // 2) ✅ 등장 시점 좌표(셀) 락 = 스폰 위치 인덱싱 기준
        Vector3Int spawnLockCell = groundTilemap.WorldToCell(playerTF.position);

        // 가로(FixedY_14) : X 기준으로 14칸 중 하나 선택
        int idxY = emitterController.GetFixedYIndexFromPlayerCellX(spawnLockCell.x);
        Transform emitY = emitterController.GetFixedYEmitter(idxY);

        // 세로(FixedX_18) : Y 기준으로 18칸 중 하나 선택
        int idxX = emitterController.GetFixedXIndexFromPlayerCellY(spawnLockCell.y);
        Transform emitX = emitterController.GetFixedXEmitter(idxX);

        if (emitY == null || emitX == null) yield break;

        // 3) 탄막 2개 생성(등장 시작) - 아직 충돌 OFF 상태로 시작
        HandOfTimeProjectile pHorizontal = Instantiate(projectilePrefab, emitY.position, Quaternion.identity);
        HandOfTimeProjectile pVertical   = Instantiate(projectilePrefab, emitX.position, Quaternion.identity);

        pHorizontal.BeginFadeIn(fadeInTime);
        pVertical.BeginFadeIn(fadeInTime);
        yield return new WaitForSeconds(fadeInTime);
        
        // 4) ✅ 페이드인 0.5초 동안 대기 (이 동안 플레이어가 범위 밖으로 나가도 OK)
        //    -> 충돌은 Projectile 내부에서 OFF 상태
        yield return new WaitForSeconds(fadeInTime);

        // 5) ✅ 발사 시점 좌표(월드) 재획득 = 발사 방향 기준
        Vector3 fireLockWorld = playerTF.position;

        // 6) 발사 시작(이 시점부터 충돌 ON)
        pHorizontal.BeginFire(fireLockWorld, speedWorldPerSec);
        pVertical.BeginFire(fireLockWorld, speedWorldPerSec);
    }
}