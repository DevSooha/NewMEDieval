using System.Collections;
using UnityEngine;

public enum BossState
{
    Move,
    Attack,
    Cooldown
}

public class ThreeWitchCombat : BossCombatBase, IBossPhaseHandler
{
    public static ThreeWitchCombat Instance;

    public BossState currentState;
    public int phase = 1;
    public Transform playerTF;
    public float keepCloseTimer = 0;
    public float moveSpeed = 2.0f;

    [Header("Phase HP Thresholds")]
    [SerializeField] private int phase2HpThreshold = 8000;
    [SerializeField] private int phase3HpThreshold = 4000;

    public GameObject fireStartEffect;
    public GameObject fireWallPrefab;
    public GameObject aquaRayPrefab;
    public GameObject electricWallPrefab;
    public GameObject electricRayPrefab;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }
    }

    public override void StartBattle()
    {
        StartCoroutine(AppearRoutine());
    }

    public void OnBossHpChanged(int currentHp, int maxHp)
    {
        int nextPhase = 1;
        if (currentHp > phase2HpThreshold) nextPhase = 1;
        else if (currentHp > phase3HpThreshold) nextPhase = 2;
        else if (currentHp > 0) nextPhase = 3;

        if (phase != nextPhase)
        {
            Debug.Log($"[BOSS] 페이즈 변경! {phase} -> {nextPhase}");
            phase = nextPhase;
        }
    }

    IEnumerator AppearRoutine()
    {
        float timer = 0f;
        float appearTime = 1.0f;

        while (timer < appearTime)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / appearTime);
            if (spriteRenderer != null)
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
            float currentDistance = Vector2.Distance(transform.position, playerTF.position);

            if (currentState == BossState.Move)
            {
                transform.position = Vector2.MoveTowards(transform.position, playerTF.position, moveSpeed * Time.deltaTime);

                if (currentDistance > 3.0f)
                {
                    keepCloseTimer = 0;
                    currentState = BossState.Attack;
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
        Debug.Log("공격!");

        switch (phase)
        {
            case 1:
                yield return StartCoroutine(FirePattern());
                break;
            case 2:
                yield return StartCoroutine(WaterPattern());
                break;
            case 3:
                yield return StartCoroutine(ElectricPattern());
                break;
        }

        yield return new WaitForSeconds(0.5f);

        currentState = BossState.Cooldown;
        yield return new WaitForSeconds(4.0f);

        currentState = BossState.Move;
    }

    IEnumerator FirePattern()
    {
        if (playerTF == null) yield break;
        Debug.Log("파이어월 매직!");
        Vector2 dir = playerTF.position - transform.position;

        for (int i = 0; i < 2; i++)
        {
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            for (int j = -2; j <= 2; j++)
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
        Debug.Log("아쿠아레이 매직!");

        Vector2 dir = playerTF.position - transform.position;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 6; i++)
        {
            float finalAngle = baseAngle + (i * 60f);
            Quaternion rot = Quaternion.Euler(0, 0, finalAngle);
            Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

            GameObject rayObj = Instantiate(aquaRayPrefab, spawnPos, rot);
            rayObj.GetComponent<BossProjectile>()?.Setup(ElementType.Water);
        }
        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator ElectricPattern()
    {
        if (playerTF == null) yield break;
        Debug.Log("판도라의 전기 매직!");

        Vector2 dir = playerTF.position - transform.position;
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = -2; i <= 2; i++)
        {
            float finalAngle = baseAngle + (i * 45f);
            Quaternion rot = Quaternion.Euler(0, 0, finalAngle);
            Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

            GameObject wallObj = Instantiate(electricWallPrefab, spawnPos, rot);
            wallObj.GetComponent<BossProjectile>()?.Setup(ElementType.Electric);
        }

        yield return new WaitForSeconds(1.2f);

        dir = playerTF.position - transform.position;
        baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 6; i++)
        {
            float finalAngle = baseAngle + (i * 60f);
            Quaternion rot = Quaternion.Euler(0, 0, finalAngle);
            Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

            GameObject rayObj = Instantiate(electricRayPrefab, spawnPos, rot);
            rayObj.GetComponent<BossProjectile>()?.Setup(ElementType.Electric);
        }
    }
}
