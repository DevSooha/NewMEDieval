using UnityEngine;

public class QueenCombat : BossCombatBase
{
    [Header("References")]
    [SerializeField] private PearlBeamController pearlBeam;

    public override void StartBattle()
    {
        // 플레이어 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("[QueenCombat] Player not found (Tag: Player).");
            return;
        }

        // PearlBeam 자동 연결(인스펙터에서 안 넣었을 때 대비)
        if (pearlBeam == null)
        {
            pearlBeam = FindAnyObjectByType<PearlBeamController>();
        }

        if (pearlBeam == null)
        {
            Debug.LogError("[QueenCombat] PearlBeamController not assigned/found.");
            return;
        }

        // 빔 시작
        pearlBeam.Begin(playerObj.transform);

        Debug.Log("[QueenCombat] Battle started: PearlBeam Begin()");
    }
}