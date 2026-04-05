using System.Collections;
using UnityEngine;

public class NemiBossCombat : BossCombatBase, IBossPhaseHandler
{
    [Header("Refs")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private NemiFateSever nemiFateSever;
    [SerializeField] private NemiPearlBeam nemiPearlBeam;
    [SerializeField] private HandOfTime handOfTime;

    [Header("Phase Threshold")]
    [SerializeField] private int phase2ThresholdHp = 2200;

    [Header("Timing")]
    [SerializeField] private float afterHandDelay = 2f;   // Hand of Time 이후 보스 휴식
    [SerializeField] private float postFateSeverDelay = 3f; // Fate Sever 이후 후딜
    [SerializeField] private float postPearlBeamDelay = 3f; // Pearl Beam 이후 후딜

    [Header("Positions")]
    [SerializeField] private Transform startPositionTF;    // 플레이어 리스폰 위치 (0.5, -7.9)
    [SerializeField] private Vector3 bossInitialPosition = new Vector3(0.5f, 0f, 0f);

    private int lastHp = int.MaxValue;
    private bool phase2Triggered;
    private bool isBattleRunning;
    private bool isVictoryHandled;
    private Coroutine battleRoutine;

    private Transform playerTF;

    private void Awake()
    {
        if (bossHealth == null) bossHealth = GetComponent<BossHealth>();
        if (nemiFateSever == null) nemiFateSever = GetComponent<NemiFateSever>();
        if (nemiPearlBeam == null) nemiPearlBeam = GetComponent<NemiPearlBeam>();
        if (handOfTime == null) handOfTime = GetComponentInChildren<HandOfTime>();
    }

    private void OnEnable()
    {
        RegisterPlayerDeathBaseHandler(HandlePlayerDeath);
    }

    // ===========================
    // StartBattle — BossBattleTrigger에서 호출
    // ===========================
    public override void StartBattle()
    {
        ResolvePlayerTF();

        if (bossHealth != null)
            lastHp = bossHealth.currentHP;

        isBattleRunning = true;
        isVictoryHandled = false;

        if (battleRoutine != null)
            StopCoroutine(battleRoutine);

        battleRoutine = StartCoroutine(BattleLoopRoutine());
    }

    // ===========================
    // 마스터 전투 루프
    // ===========================
    private IEnumerator BattleLoopRoutine()
    {
        // ── Phase 1 루프 (HP >= 2200) ──
        while (isBattleRunning && !phase2Triggered)
        {
            // 1) Hand of Time
            ResolvePlayerTF();
            if (handOfTime != null && playerTF != null)
                yield return handOfTime.PlayOnce(playerTF);

            if (!isBattleRunning) yield break;
            if (phase2Triggered) break; // HoT 도중 HP가 2200 미만이 되면 즉시 Phase2로

            // 2) 보스 2초 휴식
            yield return new WaitForSeconds(afterHandDelay);
            if (!isBattleRunning) yield break;
            if (phase2Triggered) break;

            // 3) Fate Sever
            if (nemiFateSever != null)
                yield return nemiFateSever.HandleFateSever();

            if (!isBattleRunning) yield break;
            if (phase2Triggered) break; // Fate Sever 도중이라도 종료 후 Phase2로

            // 4) 3초 후딜
            yield return new WaitForSeconds(postFateSeverDelay);
            if (!isBattleRunning) yield break;
        }

        // Phase2가 트리거되지 않았으면 (전투 중단 등) 종료
        if (!phase2Triggered || !isBattleRunning)
            yield break;

        // ── Phase 2 전환: Pearl Beam ──
        transform.position = bossInitialPosition; // 보스 (0.5, 0)으로 이동

        if (nemiPearlBeam != null)
            yield return nemiPearlBeam.ExecutePearlBeamPattern();

        if (!isBattleRunning) yield break;

        // Pearl Beam 이후 3초 대기
        yield return new WaitForSeconds(postPearlBeamDelay);
        if (!isBattleRunning) yield break;

        // ── Phase 2 이후 루프: 응용 HoT + Fate Sever ──
        while (isBattleRunning)
        {
            // 1) 응용 Hand of Time (Bedimmed Wall 포함)
            ResolvePlayerTF();
            if (handOfTime != null && playerTF != null)
                yield return handOfTime.PlayOnceAdvanced(playerTF);

            if (!isBattleRunning) yield break;

            // 2) 보스 2초 휴식
            yield return new WaitForSeconds(afterHandDelay);
            if (!isBattleRunning) yield break;

            // 3) Fate Sever (Phase 1과 동일)
            if (nemiFateSever != null)
                yield return nemiFateSever.HandleFateSever();

            if (!isBattleRunning) yield break;

            // 4) 3초 후딜
            yield return new WaitForSeconds(postFateSeverDelay);
            if (!isBattleRunning) yield break;
        }
    }

    // ===========================
    // HP 변경 콜백 — Phase 전환 + 승리 감지
    // ===========================
    public void OnBossHpChanged(int currentHp, int maxHp)
    {
        // 승리 감지 (Die()보다 먼저 호출됨)
        if (currentHp <= 0 && !isVictoryHandled)
        {
            isVictoryHandled = true;
            isBattleRunning = false;
            StopAllPatterns();
            Debug.Log("[NemiBossCombat] Victory! Nemi defeated.");
            // 엔딩 씬 전환은 추후 별도 구현 예정
            return;
        }

        // Phase2 전환 감지
        if (!phase2Triggered)
        {
            bool crossedToPhase2 = (lastHp >= phase2ThresholdHp && currentHp < phase2ThresholdHp);
            lastHp = currentHp;

            if (crossedToPhase2)
            {
                phase2Triggered = true;
                // BattleLoopRoutine의 while문 조건에서 phase2Triggered를 감지하여
                // Phase1 루프를 자연스럽게 빠져나감
            }
        }
        else
        {
            lastHp = currentHp;
        }
    }

    // ===========================
    // 플레이어 사망 처리
    // ===========================
    private void HandlePlayerDeath()
    {
        isBattleRunning = false;
        StopAllPatterns();
        CleanupBossPresentationOnPlayerDeath();
        StartCoroutine(HandlePlayerDeathRoutine());
    }

    private IEnumerator HandlePlayerDeathRoutine()
    {
        // 검은색 페이드 아웃 0.5초
        if (UIManager.Instance != null)
            yield return UIManager.Instance.FadeOut(0.5f);

        yield return null;

        // 플레이어 부활 + startPositionTF로 이동
        if (Player.Instance != null)
        {
            Player player = Player.Instance;
            player.gameObject.SetActive(true);

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null) health.Resurrect();

            if (startPositionTF != null)
                player.transform.position = startPositionTF.position;

            if (player.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
                rb.linearVelocity = Vector2.zero;

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.UpdateRoomStateAfterTeleport();
                RoomManager.Instance.SyncCameraToPlayer();
            }
        }

        // 보스 HP 5200으로 완전 리셋
        if (bossHealth != null)
            bossHealth.ResetToFull();

        // 보스 초기 위치로 이동
        transform.position = bossInitialPosition;

        // 페이즈 플래그 리셋
        phase2Triggered = false;
        isVictoryHandled = false;
        lastHp = bossHealth != null ? bossHealth.currentHP : int.MaxValue;

        // Pearl Beam 재사용 가능하도록 리셋
        if (nemiPearlBeam != null)
            nemiPearlBeam.ResetState();

        // FateSever 플레이어 참조 갱신
        if (nemiFateSever != null)
            nemiFateSever.RefreshPlayerReference();

        // 애니메이터 복구
        ResumeBossPresentation();

        // 검은색 페이드 인 0.5초
        if (UIManager.Instance != null)
            yield return UIManager.Instance.FadeIn(0.5f);

        // 전투 Phase1부터 재시작
        StartBattle();
    }

    // ===========================
    // 패턴 정지 및 정리
    // ===========================
    private void StopAllPatterns()
    {
        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }

        nemiFateSever?.StopAllCoroutines();
        nemiPearlBeam?.StopPattern();
        handOfTime?.DestroyAllProjectiles();
        CleanupBossOffensives(BossOffensiveCleanupReason.BattleReset);
    }

    private void OnDisable()
    {
        UnregisterPlayerDeathBaseHandler(HandlePlayerDeath);
        StopAllPatterns();
        CleanupOffensivesOnDisable();
    }

    // ===========================
    // 유틸리티
    // ===========================
    private void ResolvePlayerTF()
    {
        TryResolvePlayerTransform(ref playerTF);
    }
}
