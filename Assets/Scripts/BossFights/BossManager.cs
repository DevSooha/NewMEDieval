using UnityEngine;
using System;

public class BossManager : MonoBehaviour
{
    public static BossManager Instance;
    public bool IsBossActive { get; private set; }

    // 보스전 종료 시 문을 열기 위한 이벤트
    public event Action OnBossBattleEnded;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 보스전 시작 (Trigger가 호출)
    public void NotifyBossStart()
    {
        IsBossActive = true;
    }

    // 보스 사망 (Boss 스크립트가 호출)
    public void EndBossBattle()
    {
        IsBossActive = false;
        OnBossBattleEnded?.Invoke(); // 문 열라고 신호 보냄
    }
}