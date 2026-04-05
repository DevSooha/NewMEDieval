using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class NemiFateSever : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Transform playerTF;

    [Header("Timing (FateSever)")]
    [SerializeField] private float fateCastTime = 0.5f;          // FateSever 시전 모션 0.5초
    [SerializeField] private float tp1Delay = 0.5f;              // 0.5초만에 첫 텔포
    [SerializeField] private float tp2Delay = 0.1f;              // 0.1초만에 두번째 텔포
    [SerializeField] private float tp3Delay = 0.1f;              // 0.1초만에 세번째 텔포
    [SerializeField] private float attackMotionTime = 0.5f;      // 공격 모션 0.5초

    [Header("Attack Detection")]
    [SerializeField] private LayerMask playerLayerMask;           // Player 레이어 마스크
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackYOffset = -0.5f;          // 공격 판정 Y 보정값

    private void Awake()
    {
        if (groundTilemap == null)
        {
            var groundObj = GameObject.FindGameObjectWithTag("Ground");
            if (groundObj != null) groundTilemap = groundObj.GetComponentInChildren<Tilemap>();
        }

        if (playerTF == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTF = p.transform;
        }

        // playerLayerMask가 Inspector에서 설정되지 않은 경우 "Player" 레이어 자동 설정
        if (playerLayerMask == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                playerLayerMask = 1 << playerLayer;
        }
    }

    /// <summary>
    /// NemiBossCombat에서 직접 yield return으로 호출하는 Fate Sever 코루틴.
    /// 텔포 3회 + 공격 판정 1회를 수행한다.
    /// </summary>
    public IEnumerator HandleFateSever()
    {
        if (playerTF == null || groundTilemap == null)
            yield break;

        // A) 0.5초 시전 모션 (아트 미구현, 인터벌만)
        yield return new WaitForSeconds(fateCastTime);

        // B) 첫 텔포 대기 (tp1Delay) — 이 시간 동안 플레이어가 움직일 수 있음
        yield return new WaitForSeconds(tp1Delay);

        // C) 텔포 직전에 플레이어 위치를 "락" (FinalBoss의 Carma Excision과 동일한 타이밍)
        Vector3 lockedPlayerPos = playerTF.position;
        Vector3Int lockedPlayerCell = groundTilemap.WorldToCell(lockedPlayerPos);

        // D) 텔포 목표 셀 2개 (왼/오 1칸) 미리 계산
        Vector3Int leftCell  = new Vector3Int(lockedPlayerCell.x - 1, lockedPlayerCell.y, 0);
        Vector3Int rightCell = new Vector3Int(lockedPlayerCell.x + 1, lockedPlayerCell.y, 0);

        // E) X축만 셀 중앙 사용, Y축은 플레이어 월드 좌표 +0.5
        float targetY = lockedPlayerPos.y + 0.5f;
        Vector3 leftWorld  = new Vector3(groundTilemap.GetCellCenterWorld(leftCell).x, targetY, 0f);
        Vector3 rightWorld = new Vector3(groundTilemap.GetCellCenterWorld(rightCell).x, targetY, 0f);

        // F) 즉시 첫 텔포 — 플레이어 왼쪽 1칸
        transform.position = leftWorld;

        // F) 텔포 2: 0.1초 후 플레이어 오른쪽 1칸
        yield return new WaitForSeconds(tp2Delay);
        transform.position = rightWorld;

        // G) 텔포 3: 0.1초 후 다시 플레이어 왼쪽 1칸 (이 위치에서 공격)
        yield return new WaitForSeconds(tp3Delay);
        transform.position = leftWorld;

        // H) 공격 모션 + 실제 판정
        yield return StartCoroutine(ExecuteAttack());
    }

    /// <summary>
    /// 공격 모션 0.5초 후, 보스의 오른쪽 1칸(= 보스 기준 +1 셀)에 플레이어가
    /// 실시간으로 존재하는지 OverlapBox로 판정한다.
    /// </summary>
    private IEnumerator ExecuteAttack()
    {
        // 0.5초 공격 모션 (아트 미구현, 인터벌만)
        yield return new WaitForSeconds(attackMotionTime);

        // 보스 현재 위치 기준 오른쪽 1칸 셀 계산, Y는 보스 위치 기준 유지
        Vector3Int bossCell = groundTilemap.WorldToCell(transform.position);
        Vector3Int attackCell = new Vector3Int(bossCell.x + 1, bossCell.y, 0);
        Vector3 attackCenter = new Vector3(
            groundTilemap.GetCellCenterWorld(attackCell).x,
            transform.position.y + attackYOffset,
            0f
        );

        // 타일 1칸 크기로 OverlapBox 판정 (약간 안쪽으로 줄여 가장자리 오판 방지)
        Vector2 boxSize = new Vector2(0.9f, 0.9f);
        Collider2D hit = Physics2D.OverlapBox(attackCenter, boxSize, 0f, playerLayerMask);

        if (hit != null && hit.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(hit, attackDamage, transform.position);
        }
    }

    private void OnDrawGizmos()
    {
        if (groundTilemap == null) return;

        // 보스 현재 위치 기준 오른쪽 1칸 공격 판정 영역 (attackYOffset 적용)
        Vector3Int bossCell = groundTilemap.WorldToCell(transform.position);
        Vector3Int attackCell = new Vector3Int(bossCell.x + 1, bossCell.y, 0);
        Vector3 attackCenter = new Vector3(
            groundTilemap.GetCellCenterWorld(attackCell).x,
            transform.position.y + attackYOffset,
            0f
        );

        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawCube(attackCenter, new Vector3(0.9f, 0.9f, 0f));
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackCenter, new Vector3(0.9f, 0.9f, 0f));

        // 플레이어 기준 텔포 위치 미리보기 (텔포는 플레이어 Y +1)
        if (playerTF != null)
        {
            Vector3 pPos = playerTF.position;
            float gizmoY = pPos.y + 0.5f;
            Vector3Int pCell = groundTilemap.WorldToCell(pPos);
            Vector3Int lCell = new Vector3Int(pCell.x - 1, pCell.y, 0);
            Vector3Int rCell = new Vector3Int(pCell.x + 1, pCell.y, 0);
            Vector3 leftPos = new Vector3(groundTilemap.GetCellCenterWorld(lCell).x, gizmoY, 0f);
            Vector3 rightPos = new Vector3(groundTilemap.GetCellCenterWorld(rCell).x, gizmoY, 0f);

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(leftPos, new Vector3(0.8f, 0.8f, 0f));
            Gizmos.DrawCube(rightPos, new Vector3(0.8f, 0.8f, 0f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(leftPos, new Vector3(0.8f, 0.8f, 0f));
            Gizmos.DrawWireCube(rightPos, new Vector3(0.8f, 0.8f, 0f));
        }
    }

    /// <summary>
    /// playerTF 참조를 외부에서 갱신할 때 사용 (플레이어 부활 후 등)
    /// </summary>
    public void RefreshPlayerReference()
    {
        if (Player.Instance != null)
        {
            playerTF = Player.Instance.transform;
            return;
        }

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTF = p.transform;
    }
}
