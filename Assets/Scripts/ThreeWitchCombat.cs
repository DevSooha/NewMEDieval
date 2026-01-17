using System.Collections;
using System.Threading;
using UnityEngine;

public enum BossState
{
    Move,
    Attack,
    Cooldown
}

public class ThreeWitchCombat : MonoBehaviour
{
    public static ThreeWitchCombat Instance;

    public BossState currentState;
    public int phase = 1;
    public Transform playerTF;
    public float keepCloseTimer = 0;
    public float moveSpeed = 2.0f;

    public GameObject fireStartEffect;
    public GameObject fireWallPrefab;
    public GameObject aquaRayPrefab;
    public GameObject electricWallPrefab;
    public GameObject electricRayPrefab;

    private SpriteRenderer spriteRenderer;
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (Instance == null) {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }
    }

    public void StartBattle()
    {
        StartCoroutine(AppearRoutine());
    }

    IEnumerator AppearRoutine()
    {
        float timer = 0f;
        float appearTime = 1.0f;

        while (timer < appearTime)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / appearTime);
            if(spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }
            yield return null;
        }

        GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
        if (foundPlayer != null)
        {
            playerTF = foundPlayer.transform;
            StartCoroutine(BattleRoutine());
        }
        else
        {
            Debug.LogError("Player를 찾을 수 없음!");
        }
    }

    IEnumerator BattleRoutine()
    {

        

        while (true)
        {

            if (playerTF == null) yield break;
            float currentDistance= Vector2.Distance(transform.position, playerTF.position);  

            if (currentState == BossState.Move)
            {
                transform.position = Vector2.MoveTowards(transform.position, playerTF.position, moveSpeed * Time.deltaTime);

                if (currentDistance > 3.0f)
                {
                    keepCloseTimer = 0;
                    currentState=BossState.Attack;
                    StartCoroutine(AttackRoutine());
                }
                else
                {
                    keepCloseTimer += Time.deltaTime;
                    if (keepCloseTimer > 8.0f)
                    {
                        keepCloseTimer = 0;
                        currentState = BossState.Attack;
                        StartCoroutine(AttackRoutine());
                    }
                }
            }
            yield return null;
        }
    }

    IEnumerator AttackRoutine()
    {
        // 공격 모션 대기
        Debug.Log("공격!");
        
        switch (phase)
        {
            case 1:
                yield return StartCoroutine(FirePattern());
                break;
            case 2:
                yield return StartCoroutine (WaterPattern());
                break;
            case 3:
                yield return StartCoroutine (ElectricPattern());
                break;
        }

        yield return new WaitForSeconds(0.5f);

        // 쿨타임 대기
        currentState = BossState.Cooldown;
        yield return new WaitForSeconds(4.0f);

        currentState = BossState.Move;
    }   

    IEnumerator FirePattern() {
        if (playerTF == null) yield break;
        Debug.Log("파이어월 매직!");
        Vector2 dir = playerTF.position - transform.position;

        for (int i = 0; i < 2; i++)
        {
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            for (int j= -2; j<=2; j++)
            {
                float finalAngle = baseAngle + (j * 45f);
                Quaternion rot = Quaternion.Euler(0, 0, finalAngle);

                Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

                GameObject muzzle = Instantiate(fireStartEffect, spawnPos, rot);

                GameObject projectile = Instantiate(fireWallPrefab, spawnPos, rot);
                projectile.GetComponent<BossProjectile>()?.Setup(ElementType.Fire);
                Destroy(muzzle, 1.0f);
            }
            yield return new WaitForSeconds(0.7f);
        }
    }

    IEnumerator WaterPattern()
    {
        if (playerTF == null) yield break;
        Debug.Log("아쿠아레이 매직!"); // [cite: 50]

        Vector2 dir = playerTF.position - transform.position;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 6; i++) // [cite: 54] 6발 발사
        {
            float finalAngle = baseAngle + (i * 60f); // [cite: 54] 60도 간격
            Quaternion rot = Quaternion.Euler(0, 0, finalAngle);

            // 1. [중요] 보스 몸속이 아니라, 약간 앞에서 생성되게 위치 조정
            // (불 패턴에 있던 계산식을 가져왔습니다)
            Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

            // 2. 수정된 위치(spawnPos)에 생성
            GameObject rayObj = Instantiate(aquaRayPrefab, spawnPos, rot);

            // 3. [핵심] Setup을 반드시 호출해서 데미지/속성 부여
            rayObj.GetComponent<BossProjectile>()?.Setup(ElementType.Water);
        }

        yield return new WaitForSeconds(0.5f);
    }

    // ThreeWitchCombat.cs

    IEnumerator ElectricPattern()
    {
        if (playerTF == null) yield break;
        Debug.Log("판도라의 전기 매직!");

        Vector2 dir = playerTF.position - transform.position;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 1단계: 전기 벽 소환 (FireWall 아님!)
        for (int i = -2; i <= 2; i++)
        {
            float finalAngle = baseAngle + (i * 45f);
            Quaternion rot = Quaternion.Euler(0, 0, finalAngle);
            Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

            // [수정 1] FireWallPrefab -> ElectricWallPrefab으로 변경
            GameObject wallObj = Instantiate(electricWallPrefab, spawnPos, rot);

            // [수정 2] Setup 호출 (속성은 Electric)
            wallObj.GetComponent<BossProjectile>()?.Setup(ElementType.Electric);
        }

        yield return new WaitForSeconds(1.2f);

        // 2단계: 전기 광선 발사
        dir = playerTF.position - transform.position;
        baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 6; i++)
        {
            float finalAngle = baseAngle + (i * 60f);
            Quaternion rot = Quaternion.Euler(0, 0, finalAngle);

            // [수정 3] 보스 몸속(transform.position)이 아니라, 앞(2.5f)으로 끄집어내기
            Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

            GameObject rayObj = Instantiate(electricRayPrefab, spawnPos, rot);

            // [수정 4] Setup 호출
            rayObj.GetComponent<BossProjectile>()?.Setup(ElementType.Electric);
        }
    }
}
