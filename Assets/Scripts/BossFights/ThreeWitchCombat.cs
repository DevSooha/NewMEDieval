using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossState
{
    Move,
    Attack,
    Cooldown
}

public class ThreeWitchCombat : BossCombatBase, IBossPhaseHandler
{
    private enum FacingDirection
    {
        W,
        S,
        E
    }

    private const float PixelsPerUnit = 32f;
    private const float RangedAttackDistancePx = 96f;
    private const float RangedAttackRetrySeconds = 8f;

    public BossState currentState;
    public int phase = 1;
    public Transform playerTF;
    public float keepCloseTimer = 0;
    public float moveSpeed = 2.0f;

    [Header("Phase HP Thresholds")]
    [SerializeField] private int phase2HpThreshold = 8000;
    [SerializeField] private int phase3HpThreshold = 4000;

    [Header("Animation")]
    [SerializeField] private Animator threeWitchAnimator;
    [SerializeField] private int animatorLayerIndex = 0;
    [SerializeField] private float directionEpsilon = 0.05f;
    [SerializeField] private float southEnterRatio = 1.1f;
    [SerializeField] private float southExitRatio = 0.85f;

    public GameObject fireStartEffect;
    public GameObject fireWallPrefab;
    public GameObject aquaRayPrefab;
    public GameObject electricWallPrefab;
    public GameObject electricRayPrefab;

    // QS-12 확장: 패턴별 최대 동시 탄 수 + 탄 수명(3s)과 웨이브 겹침을 고려한 풀 크기
    private const int FireWallPoolSize = 12;    // 2웨이브 × 5발, 웨이브 간격 0.7s < 수명 3s → 10발 동시 생존
    private const int AquaRayPoolSize = 8;      // 6발 일제 발사
    private const int ElectricWallPoolSize = 8; // 5발 일제 발사
    private const int ElectricRayPoolSize = 8;  // 6발 일제 발사

    private BossProjectilePool fireWallPool;
    private BossProjectilePool aquaRayPool;
    private BossProjectilePool electricWallPool;
    private BossProjectilePool electricRayPool;
    private Transform projectilePoolRoot;
    private readonly HashSet<string> warnedInvalidProjectilePrefabs = new HashSet<string>();

    private SpriteRenderer spriteRenderer;
    private string lastPlayedStateName;
    private int lastHorizontalFacing = 1;
    private FacingDirection currentFacingDirection = FacingDirection.E;
    private bool hasTriggeredFarAttackInCurrentWindow;
    private bool hasPlayedAttackAnimationInCurrentAttack;
    private bool isBattleStopped;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (threeWitchAnimator == null) threeWitchAnimator = GetComponentInChildren<Animator>();

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = 0f;
            spriteRenderer.color = c;
        }

        EnsureProjectilePools();
    }

    // QS-12: 풀 루트는 보스 자식이 아닌 독립 오브젝트로 둔다 (Julmeo 선례).
    // ThreeWitch는 정지형이지만, 보스 비활성화 시 자식 풀 오브젝트가
    // activeInHierarchy=false로 함께 묶여 Rent의 SetActive가 무력화되는 것을 막는다.
    private void EnsureProjectilePools()
    {
        if (projectilePoolRoot != null) return;

        projectilePoolRoot = new GameObject("ThreeWitchProjectilePoolRoot").transform;

        if (fireWallPrefab != null) fireWallPool = new BossProjectilePool(fireWallPrefab, FireWallPoolSize, projectilePoolRoot);
        if (aquaRayPrefab != null) aquaRayPool = new BossProjectilePool(aquaRayPrefab, AquaRayPoolSize, projectilePoolRoot);
        if (electricWallPrefab != null) electricWallPool = new BossProjectilePool(electricWallPrefab, ElectricWallPoolSize, projectilePoolRoot);
        if (electricRayPrefab != null) electricRayPool = new BossProjectilePool(electricRayPrefab, ElectricRayPoolSize, projectilePoolRoot);
    }

    private void OnDestroy()
    {
        if (projectilePoolRoot != null)
        {
            Destroy(projectilePoolRoot.gameObject);
        }
    }

    private void SpawnPooledProjectile(BossProjectilePool pool, GameObject prefab, string slotName, Vector3 spawnPos, Quaternion rot, ElementType element)
    {
        BossProjectile projectile = pool != null ? pool.Rent() : null;
        if (projectile != null)
        {
            // QS-12 조건: Rent 직후 매번 등록 (재사용 인스턴스는 딕셔너리 덮어쓰기)
            RegisterBossOffensive(projectile.gameObject);
            projectile.transform.SetPositionAndRotation(spawnPos, rot);
            projectile.Setup(element);
            return;
        }

        // 풀 구성 실패(프리팹 미배선/스크립트 누락) 시 기존 Instantiate 경로 유지 (QS-86 패턴)
        if (prefab == null)
        {
            WarnProjectilePrefabInvalidOnce(slotName, "프리팹 미배선");
            return;
        }

        GameObject obj = Instantiate(prefab, spawnPos, rot);
        BossProjectile script = obj.GetComponent<BossProjectile>();
        if (script == null)
        {
            // 이동/소멸 로직 없는 정지 오브젝트가 발사마다 쌓이는 것 방지 (QS-86)
            WarnProjectilePrefabInvalidOnce(slotName, "프리팹에 BossProjectile 없음");
            Destroy(obj);
            return;
        }

        RegisterBossOffensive(obj);
        script.Setup(element);
    }

    private void WarnProjectilePrefabInvalidOnce(string slotName, string reason)
    {
        if (!warnedInvalidProjectilePrefabs.Add(slotName)) return;
        Debug.LogError($"[ThreeWitch] {slotName} 발사 불가: {reason}. 발사를 건너뜁니다.");
    }

    public override void StartBattle()
    {
        ResumeBossPresentation();
        ResetBattleFlags();
        StartCoroutine(AppearRoutine());
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResumeBossPresentation();
        isBattleStopped = false;
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

    private IEnumerator AppearRoutine()
    {
        float appearTime = 1.0f;
        yield return FadeSpriteAlpha(spriteRenderer, appearTime, 0f, 1f);

        if (TryResolvePlayerTransform(ref playerTF))
        {
            StartCoroutine(BattleRoutine());
        }
        else
        {
            Debug.LogError("Player를 찾을 수 없음!");
        }
    }

    private IEnumerator BattleRoutine()
    {
        while (true)
        {
            if (isBattleStopped) yield break;
            if (playerTF == null) yield break;
            float currentDistanceUnits = Vector2.Distance(transform.position, playerTF.position);
            float currentDistancePx = currentDistanceUnits * PixelsPerUnit;

            if (currentState != BossState.Attack)
            {
                UpdateAnimatorForState(currentState, playerTF.position - transform.position);
            }

            if (currentState == BossState.Move)
            {
                bool isOutOfRange = currentDistancePx >= RangedAttackDistancePx;
                if (isOutOfRange)
                {
                    keepCloseTimer += Time.deltaTime;

                    bool shouldAttackImmediately = !hasTriggeredFarAttackInCurrentWindow;
                    bool shouldAttackByTimeout = keepCloseTimer >= RangedAttackRetrySeconds;
                    if (shouldAttackImmediately || shouldAttackByTimeout)
                    {
                        hasTriggeredFarAttackInCurrentWindow = true;
                        keepCloseTimer = 0f;
                        currentState = BossState.Attack;
                        StartCoroutine(AttackRoutine());
                    }
                }
                else
                {
                    keepCloseTimer = 0f;
                    hasTriggeredFarAttackInCurrentWindow = false;
                }
            }
            yield return null;
        }
    }

    private IEnumerator AttackRoutine()
    {
        if (isBattleStopped) yield break;
        Debug.Log("공격!");
        hasPlayedAttackAnimationInCurrentAttack = false;

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
        UpdateAnimatorForState(BossState.Cooldown, playerTF != null ? (playerTF.position - transform.position) : Vector2.right);
        yield return new WaitForSeconds(4.0f);

        currentState = BossState.Move;
    }

    private IEnumerator FirePattern()
    {
        if (playerTF == null) yield break;
        Debug.Log("파이어월 매직!");
        Vector2 dir = playerTF.position - transform.position;
        TryPlayAttackAnimationAtFireTime(dir);

        for (int i = 0; i < 2; i++)
        {
            float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            for (int j = -2; j <= 2; j++)
            {
                float finalAngle = baseAngle + (j * 45f);
                Quaternion rot = Quaternion.Euler(0, 0, finalAngle);

                Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

                // muzzle(fireStartEffect)은 BossProjectile 미부착 연출 전용 — 풀링 범위 밖, 현행 유지
                GameObject muzzle = Instantiate(fireStartEffect, spawnPos, rot);
                RegisterBossOffensive(muzzle, true);

                SpawnPooledProjectile(fireWallPool, fireWallPrefab, nameof(fireWallPrefab), spawnPos, rot, ElementType.Fire);
                Destroy(muzzle, 1.0f);
            }
            yield return new WaitForSeconds(0.7f);
        }
    }

    private IEnumerator WaterPattern()
    {
        if (playerTF == null) yield break;
        Debug.Log("아쿠아레이 매직!");
        Vector2 dir = playerTF.position - transform.position;
        TryPlayAttackAnimationAtFireTime(dir);
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 6; i++)
        {
            float finalAngle = baseAngle + (i * 60f);
            Vector3 fireDir = Quaternion.Euler(0f, 0f, finalAngle) * Vector3.right;
            Vector3 spawnPos = transform.position + (fireDir * 2.5f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.down, fireDir);

            SpawnPooledProjectile(aquaRayPool, aquaRayPrefab, nameof(aquaRayPrefab), spawnPos, rot, ElementType.Water);
        }
        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator ElectricPattern()
    {
        if (playerTF == null) yield break;
        Debug.Log("판도라의 전기 매직!");

        Vector2 dir = playerTF.position - transform.position;
        TryPlayAttackAnimationAtFireTime(dir);
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = -2; i <= 2; i++)
        {
            float finalAngle = baseAngle + (i * 45f);
            Quaternion rot = Quaternion.Euler(0, 0, finalAngle);
            Vector3 spawnPos = transform.position + (rot * Vector3.right * 2.5f);

            SpawnPooledProjectile(electricWallPool, electricWallPrefab, nameof(electricWallPrefab), spawnPos, rot, ElementType.Electric);
        }

        yield return new WaitForSeconds(1.2f);

        dir = playerTF.position - transform.position;
        baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < 6; i++)
        {
            float finalAngle = baseAngle + (i * 60f);
            Vector3 fireDir = Quaternion.Euler(0f, 0f, finalAngle) * Vector3.right;
            Vector3 spawnPos = transform.position + (fireDir * 2.5f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.down, fireDir);

            // 구 AddComponent<ElectricLaserRay> 폴백은 제거 — 풀 재사용 인스턴스에 런타임
            // 컴포넌트를 덧붙이면 프리팹 원본과 상태가 갈라진다. 프리팹 실측(QS-12 확장 감사)상
            // ElectricLaserPrefab 루트에 ElectricLaserRay가 있어 죽은 경로이며,
            // 스크립트 누락 결함은 QS-86 경고+스킵 폴백이 담당한다.
            SpawnPooledProjectile(electricRayPool, electricRayPrefab, nameof(electricRayPrefab), spawnPos, rot, ElementType.Electric);
        }
    }

    private void UpdateAnimatorForState(BossState state, Vector2 dirToPlayer)
    {
        if (threeWitchAnimator == null)
        {
            return;
        }

        string element = phase switch
        {
            1 => "Fire",
            2 => "Water",
            _ => "Electric"
        };

        string action = state == BossState.Attack ? "Attack" : "Idle";
        string direction = ResolveDirectionSuffix(dirToPlayer);
        string stateName = $"{element}_{action}_{direction}";

        if (!threeWitchAnimator.HasState(animatorLayerIndex, Animator.StringToHash(stateName)))
        {
            string fallbackName = $"{stateName} 0";
            if (threeWitchAnimator.HasState(animatorLayerIndex, Animator.StringToHash(fallbackName)))
            {
                stateName = fallbackName;
            }
        }

        if (stateName == lastPlayedStateName)
        {
            return;
        }

        lastPlayedStateName = stateName;
        threeWitchAnimator.Play(stateName, animatorLayerIndex, 0f);
    }

    private string ResolveDirectionSuffix(Vector2 dirToPlayer)
    {
        if (dirToPlayer.sqrMagnitude <= directionEpsilon * directionEpsilon)
        {
            return FacingToSuffix(currentFacingDirection);
        }

        if (dirToPlayer.x >= directionEpsilon)
        {
            lastHorizontalFacing = 1;
        }
        else if (dirToPlayer.x <= -directionEpsilon)
        {
            lastHorizontalFacing = -1;
        }

        float absX = Mathf.Abs(dirToPlayer.x);
        float absY = Mathf.Abs(dirToPlayer.y);
        bool isPlayerBelow = dirToPlayer.y < -directionEpsilon;
        if (isPlayerBelow)
        {
            float ratio = currentFacingDirection == FacingDirection.S ? southExitRatio : southEnterRatio;
            if (absY >= Mathf.Max(directionEpsilon, absX * ratio))
            {
                currentFacingDirection = FacingDirection.S;
                return FacingToSuffix(currentFacingDirection);
            }
        }

        currentFacingDirection = lastHorizontalFacing >= 0 ? FacingDirection.E : FacingDirection.W;
        return FacingToSuffix(currentFacingDirection);
    }

    private static string FacingToSuffix(FacingDirection direction)
    {
        return direction switch
        {
            FacingDirection.W => "W",
            FacingDirection.S => "S",
            _ => "E"
        };
    }

    private void TryPlayAttackAnimationAtFireTime(Vector2 dirToPlayer)
    {
        if (hasPlayedAttackAnimationInCurrentAttack)
        {
            return;
        }

        hasPlayedAttackAnimationInCurrentAttack = true;
        Vector2 resolvedDir = dirToPlayer.sqrMagnitude > 0.0001f ? dirToPlayer : Vector2.right;
        UpdateAnimatorForState(BossState.Attack, resolvedDir);
    }

    protected override void OnPlayerDied()
    {
        if (isBattleStopped)
        {
            return;
        }

        isBattleStopped = true;
        StopAllCoroutines();
        CleanupBossPresentationOnPlayerDeath();
        ResetBattleFlags();
    }

    private void ResetBattleFlags()
    {
        currentState = BossState.Move;
        keepCloseTimer = 0f;
        hasTriggeredFarAttackInCurrentWindow = false;
        hasPlayedAttackAnimationInCurrentAttack = false;
        lastPlayedStateName = null;
    }

}
