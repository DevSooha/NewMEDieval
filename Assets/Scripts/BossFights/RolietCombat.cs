using System.Collections;
using UnityEngine;

public enum RolietState
{
    Attack,
    Null,
    Cooldown
}

public class RolietCombat : BossCombatBase
{
    public Transform playerTF;
    public JulmeoCombat julmeo;
    public float dashSpeed = 5f;
    [SerializeField] private int dashDamage = 1;
    [SerializeField] private float dashHitRadius = 0.6f;

    private RolietState rolietState = RolietState.Null;
    private bool hasDealtDashDamage;

    protected override bool UseCollisionInvulnerability => false;

    public override void StartBattle()
    {
        if (rolietState == RolietState.Attack) return;

        StartCoroutine(BattleRoutine());
    }

    public void StopBattle()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    IEnumerator BattleRoutine()
    {
        if (playerTF == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            playerTF = playerObj?.transform;
        }

        if (playerTF == null) yield break;

        yield return new WaitForSeconds(0.15f);

        transform.position = playerTF.position + new Vector3(0, 4.0f, 0);
        yield return new WaitForSeconds(0.3f);

        if (julmeo != null)
        {
            julmeo.StartBattle();
        }

        yield return new WaitForSeconds(0.5f);

        rolietState = RolietState.Attack;

        while (rolietState == RolietState.Attack)
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = playerTF.position;

            float dashTime = 0.3f;
            float elapsed = 0f;
            hasDealtDashDamage = false;

            while (elapsed < dashTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dashTime;
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                TryDealDashDamageOnce();
                yield return null;
            }

            yield return new WaitForSeconds(2.6f);
        }
    }

    private void TryDealDashDamageOnce()
    {
        if (hasDealtDashDamage) return;

        if (playerTF == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            playerTF = playerObj?.transform;
            if (playerTF == null) return;
        }

        Vector2 delta = playerTF.position - transform.position;
        if (delta.sqrMagnitude > dashHitRadius * dashHitRadius) return;

        hasDealtDashDamage = BossHitResolver.TryApplyBossHit(
            playerTF,
            dashDamage,
            transform.position
        );
    }
}
