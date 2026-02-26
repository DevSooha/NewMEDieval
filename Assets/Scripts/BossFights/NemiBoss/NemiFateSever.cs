using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class NemiFateSever : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Transform playerTF;

    [SerializeField] private HandOfTime handOfTime; 
    // HandOfTime을 별도 스크립트로 분리했기 때문에
    // PatternRoutine에서 1회 실행 코루틴을 호출하는 방식으로 사용한다.

    [Header("Timing (FateSever)")]
    [SerializeField] private float afterHandDelay = 2f;          // Hand of time 이후 보스 후딜 2초
    [SerializeField] private float fateCastTime = 0.5f;          // FateSever 시전 모션 0.5초
    [SerializeField] private float tp1Delay = 0.5f;              // 0.5초만에 첫 텔포
    [SerializeField] private float tp2Delay = 0.1f;              // 0.1초만에 두번째 텔포
    [SerializeField] private float tp3Delay = 0.1f;              // 0.1초만에 세번째 텔포
    [SerializeField] private float attackMotionTime = 0.5f;      // 공격 모션 0.5초
    [SerializeField] private float postDelay = 3f;               // 공격 후딜 3초

    private Coroutine patternRoutine;
    private bool isRunning = false;

    private void Awake()
    {
        // Tilemap 자동 탐색(기존 PearlBeamController 방식 재사용)
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

        // HandOfTime 자동 탐색(같은 오브젝트에 붙이거나 자식에 붙여도 됨)
        if (handOfTime == null)
            handOfTime = GetComponentInChildren<HandOfTime>();
    }
    
    // 외부(NemiBossCombat)에서 호출
    public void StartPattern()
    {
        if (isRunning) return;
        isRunning = true;
        patternRoutine = StartCoroutine(PatternRoutine());
    }

    public void StopPattern()
    {
        if (!isRunning) return;

        if (patternRoutine != null)
            StopCoroutine(patternRoutine);

        patternRoutine = null;
        isRunning = false;
    }

    // =========================
    // FateSever "패턴 루프"
    // 기획 흐름:
    // 1) Hand of time
    // 2) (보스가 그 자리에서) 2초 딜레이
    // 3) Fate sever (텔포 3번 + 공격)
    // 4) 3초 딜레이
    // 5) 반복
    // =========================
    private IEnumerator PatternRoutine()
    {
        while (isRunning)
        {
            // 1) Hand of time 1회 실행 (별도 스크립트)
            // - 아직 HandOfTime은 골격만 있으므로, 나중에 내부를 채우면 그대로 연동됨
            if (handOfTime != null)
                yield return StartCoroutine(handOfTime.PlayOnce(playerTF));
            else
                yield return null; // handOfTime 없으면 그냥 넘어감(테스트용)

            // 2) Hand of time 이후 보스 후딜 2초
            yield return new WaitForSeconds(afterHandDelay);

            // 3) Fate sever 실행
            yield return StartCoroutine(HandleFateSever());

            // 4) Fate sever 이후 후딜 3초
            yield return new WaitForSeconds(postDelay);
        }
    }

    // =========================
    // Fate Sever 핵심 코루틴
    // =========================
    private IEnumerator HandleFateSever()
    {
        // 필수 참조 없으면 종료
        if (playerTF == null || groundTilemap == null)
            yield break;

        // A) 0.5초 시전 모션(시계 드는 모션 + 버튼 소리)
        yield return new WaitForSeconds(fateCastTime);

        // B) 시전 모션 종료 시점에 플레이어 위치를 "락" 한다. 이후 텔포/공격은 이 lockedPlayerCell 기준으로 진행 (플레이어가 움직여도 기준은 유지)
        Vector3Int lockedPlayerCell = groundTilemap.WorldToCell(playerTF.position);

        // C) 텔포 목표 셀 2개(왼/오 1칸)를 미리 계산
        // - 기획 예외: 밟을 수 없는 영역이어도 공중이라 텔포 가능 => 유효성 검사 안함
        Vector3Int leftCell  = new Vector3Int(lockedPlayerCell.x - 1, lockedPlayerCell.y, 0);
        Vector3Int rightCell = new Vector3Int(lockedPlayerCell.x + 1, lockedPlayerCell.y, 0);

        // D) 셀을 월드 좌표(셀 중앙)로 변환
        Vector3 leftWorld  = groundTilemap.GetCellCenterWorld(leftCell);
        Vector3 rightWorld = groundTilemap.GetCellCenterWorld(rightCell);

        // E) 텔포 1: 0.5초만에 플레이어 왼쪽 1칸
        // - “순간이동”이므로, 시간이 지난 후 position을 즉시 바꿈
        yield return new WaitForSeconds(tp1Delay);
        transform.position = leftWorld;

        // F) 텔포 2: 0.1초만에 플레이어 오른쪽 1칸
        yield return new WaitForSeconds(tp2Delay);
        transform.position = rightWorld;

        // G) 텔포 3: 0.1초만에 다시 플레이어 왼쪽 1칸
        // - 3번째 위치에서 실제 공격을 한다.
        yield return new WaitForSeconds(tp3Delay);
        transform.position = leftWorld;

        // H) 공격 모션 + 실제 판정은 별도 함수로 분리
        // - 최소 구현: 0.5초 기다리고 "공격 발생" 로그만 찍음
        yield return StartCoroutine(NemiFateSeverAttackMotion(lockedPlayerCell));
    }

    // =========================
    // 공격 모션 파트 분리(최소 구현)
    // - 0.5초 공격 모션
    // - 모션 끝나면 "공격 판정 1회" (지금은 자리만)
    // =========================
    private IEnumerator NemiFateSeverAttackMotion(Vector3Int lockedTargetCell)
    {
        // 0.5초 동안 시계 빛으로 공격하는 모션(애니메이션 트리거는 추후)
        yield return new WaitForSeconds(attackMotionTime);

        // 모션 이후 실제 공격(판정 1회)
        // - 기획상 공격 위치는 "기존 플레이어 좌표(= lockedTargetCell)" 32x32
        // - 최소 구현 단계라서 일단 로그만
        Debug.Log($"[NemiFateSever] Attack once at locked cell = {lockedTargetCell}");

        // TODO(다음 단계):
        // - lockedTargetCell의 월드 중앙을 구하고
        // - OverlapBox(타일 1칸 크기)로 Player 판정 후 HP 1 감소
    }
}