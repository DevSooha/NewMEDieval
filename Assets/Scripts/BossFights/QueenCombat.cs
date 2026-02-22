using System.Collections;
using UnityEngine;

public class QueenCombat : BossCombatBase
{
    [Header("References")]
    [SerializeField] private PearlBeamController pearlBeam;

    [Header("Knockback Settings")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float knockbackStunTime = 0.2f;

    [Tooltip("Proxy를 플레이어 옆에 둘 거리(월드 단위). 타일 1칸이 1이면 1로 두면 됨.")]
    [SerializeField] private float kbProxySideOffset = 1f;

    [Header("Pattern Loop")]
    [SerializeField] private float repeatDelay = 1.8f;

    private Coroutine battleRoutine;
    private Transform playerTF;

    private WaitForSeconds repeatWait;
    private Transform kbProxy;

    private void Awake()
    {
        repeatWait = new WaitForSeconds(repeatDelay);

        // ✅ 넉백 방향을 “수평”으로 만들기 위한 가짜 sender
        kbProxy = new GameObject("KB_Proxy").transform;
        kbProxy.hideFlags = HideFlags.HideInHierarchy;
    }

    private void OnDestroy()
    {
        if (kbProxy != null) Destroy(kbProxy.gameObject);
    }

    private void OnValidate()
    {
        repeatWait = new WaitForSeconds(repeatDelay);
    }

    public override void StartBattle()
    {
        if (battleRoutine != null) return;

        if (playerTF == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;
            playerTF = playerObj.transform;
        }

        if (pearlBeam == null)
            pearlBeam = FindAnyObjectByType<PearlBeamController>();

        battleRoutine = StartCoroutine(BattleLoop());
    }

    private IEnumerator BattleLoop()
    {
        while (true)
        {
            while (pearlBeam == null)
            {
                pearlBeam = FindAnyObjectByType<PearlBeamController>();
                yield return null;
            }

            yield return pearlBeam.PlayOnce(playerTF);
            yield return repeatWait;
        }
    }

    // 🔥 PearlBeamController에서 플레이어 맞췄을 때 호출
    public void OnBeamHit(Player player, Transform beamOrigin)
    {
        if (player == null) return;

        // 플레이어의 현재 타일 X 좌표
        int playerCellX = Mathf.RoundToInt(player.transform.position.x);

        // 반 나누기 기준
        // x <= 0 → 오른쪽으로
        // x > 0  → 왼쪽으로
        bool knockToRight = playerCellX <= 0;

        // Player.KnockBack은 (player - sender) 방향으로 밀기 때문에
        // 오른쪽으로 밀고 싶으면 sender를 왼쪽에 둔다
        float senderSide = knockToRight ? -1f : 1f;

        Vector3 p = player.transform.position;
        kbProxy.position = new Vector3(p.x + senderSide * 1f, p.y, p.z);

        Knockback(player, kbProxy, knockbackForce, knockbackStunTime);
    }
    private void OnDisable()
    {
        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }
    }
}