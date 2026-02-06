using System.Collections;
using UnityEngine;

public enum RolietState
{
    Spawn,      // 순간이동
    FacePlayer, // 방향고정
    Dash,       // 돌진
    Delay       // 딜레이
}

public class RolietCombat : MonoBehaviour
{
    public Transform playerTF;
    public float dashSpeed = 5f;
    public float cellSize = 0.32f;
    private bool isBattleActive;
    private void Start()
    {
         isBattleActive = false;
    }

    public void StartBattle()
    {
        if (isBattleActive) return;

        isBattleActive = true;

        if (playerTF == null) {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            playerTF = playerObj?.transform;
        }    
        Vector3 spawnPos = playerTF.position;
        transform.position = spawnPos;
        
        
        StartCoroutine(BattleRoutine());
    }
    public void StopBattle()
    {
        isBattleActive = false;
        StopAllCoroutines();
        gameObject.SetActive(false);  // BossManager에서 호출
    }

    IEnumerator BattleRoutine()
    {
        while (true)
        {
            // 1. 스폰 (0.1s)
            yield return new WaitForSeconds(0.1f);

            // 2. 방향고정 (0.5s)
            //Vector3 dir = playerTF.position - transform.position;
            //transform.rotation = Quaternion.LookRotation(dir);
            //yield return new WaitForSeconds(0.5f);

            // 3. 돌진 (0.3s)
            Vector3 lastPos = playerTF.position;
            Vector3 startDashPos = transform.position;
            float dashTime = 0.3f;
            float elapsed = 0f;

            while (elapsed < dashTime)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startDashPos, lastPos, elapsed / dashTime);
                yield return null;
            }

            // 4-5. 딜레이 (2.6s)
            yield return new WaitForSeconds(2.6f);
        }
    }
}
