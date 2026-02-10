using System.Collections;
using UnityEngine;

public enum RolietState
{
    Attack,     
    Null,       
    Cooldown
}

public class RolietCombat : MonoBehaviour
{
    public Transform playerTF;
    public JulmeoCombat julmeo;
    public float dashSpeed = 5f;

    private RolietState rolietState = RolietState.Null;
    

    
    public void StartBattle()
    {
        if (rolietState == RolietState.Attack) return;

        else 
        {
            StartCoroutine(BattleRoutine());
        }
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
        yield return new WaitForSeconds (0.15f);

        transform.position = playerTF.position + new Vector3 (0, 4.0f, 0);
        yield return new WaitForSeconds (0.3f);
        julmeo.StartBattle();

        yield return new WaitForSeconds(0.5f);

        rolietState = RolietState.Attack;

        while (rolietState == RolietState.Attack)
        {
            // 2. 현재 플레이어 위치를 목표로 대쉬
            Vector3 startPos = transform.position;
            Vector3 targetPos = playerTF.position;   // 매 사이클마다 “지금” 위치

            float dashTime = 0.3f;
            float elapsed = 0f;

            while (elapsed < dashTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dashTime;
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            // 3. 다음 대쉬 전 딜레이
            yield return new WaitForSeconds(2.6f);
        }
    }
}
