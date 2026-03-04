using UnityEngine;

public class NemiBossCombat : BossCombatBase, IBossPhaseHandler
{
    [Header("Refs")]
    [SerializeField] private BossHealth bossHealth; // 보스 체력/데미지 계산을 담당
    [SerializeField] private NemiFateSever nemiFateSever; // Phase1 패턴 담당 스크립트, HP가 2200이상일 때 호출
    [SerializeField] private NemiPearlBeam nemiPearlBeam; // Phase2 패턴 담당 스크립트, Phase1 진행중이라면 Phase2 진입 시 Phase1 끊고 Phase2실행

    [Header("Phase Threshold")]
    [SerializeField] private int phase2ThresholdHp = 2200; // 페이스 전환 임계값, currentHP가 이 값 미만이 되는 순간 Phase2로 진입

    
    private int lastHp = int.MaxValue; // 직전 데미지 처리 이전의 HP를 저장, 2200 그 부근 순간을 정확히 잡기 위해서
    private bool phase2Triggered = false; // Phase2 요구사항 상 한번만 발동해야 하므로, 플래그로 재발동 방지

    private void Awake()
    {
        if (bossHealth == null) bossHealth = GetComponent<BossHealth>(); //BossHealth가 같은 오브젝트에 붙어있으면 자도으로 참조 가져온다
        if (nemiFateSever == null) nemiFateSever = GetComponent<NemiFateSever>();
        if (nemiPearlBeam == null) nemiPearlBeam = GetComponent<NemiPearlBeam>();
    }

    public override void StartBattle()
    {
        // 시작 시 HP 기준으로 “현재 페이즈” 패턴만 시작
        if (bossHealth != null) lastHp = bossHealth.currentHP;

        if (bossHealth == null) return;// BossHealth가 없으면 HP 기반 페이즈 불가

        if (bossHealth.currentHP >= phase2ThresholdHp)
            StartPhase1(); // HP가 2200 이상이면 FateSever 패턴 시작
        else
            StartPhase2Once(); // 시작부터 HP가 2200 미만이면 Phase2 시작
    }

    public void OnBossHpChanged(int currentHp, int maxHp)
    {
        if (phase2Triggered) return; //이미 PHase2 발동했다면 더 이상 어떤 HP 변화에 반응 안한다. 

        // “플레이어 공격으로 인해” 2200 미만으로 떨어지는 순간만 캐치
        bool crossedToPhase2 = (lastHp >= phase2ThresholdHp && currentHp < phase2ThresholdHp);
        lastHp = currentHp;

        if (crossedToPhase2)
            StartPhase2Once(); //임계값 막 넘어섰다면 Phase2 발동
    }

    private void StartPhase1()
    {
        if (phase2Triggered) return;//Phase2가 이미 발동된 뒤라면 Phase1 시작하면 안된다. 

        nemiPearlBeam?.StopPattern(); // Phase2 패턴 중단
        nemiFateSever?.StartPattern(); //Phase1 패턴 시작
    }

    private void StartPhase2Once()
    {
        if (phase2Triggered) return;//혹시라도 중복 호출되면 2번 이상 발동될 수 있으니 방어
        phase2Triggered = true; // Phase2가 발동되었음을 기록

        nemiFateSever?.StopPattern(); // Phase1 패턴 중단
        nemiPearlBeam?.StartPatternOnce(); //Phase2 필살기 1회 실행
    }

    private void OnDisable()
    {
        nemiFateSever?.StopPattern();
        nemiPearlBeam?.StopPattern();
    }
}