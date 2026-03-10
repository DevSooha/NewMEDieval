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

    private SpriteRenderer spriteRenderer;
    private string lastPlayedStateName;
    private int lastHorizontalFacing = 1;
    private FacingDirection currentFacingDirection = FacingDirection.E;
    private bool hasTriggeredFarAttackInCurrentWindow;
    private bool hasPlayedAttackAnimationInCurrentAttack;

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
    }

    public override void StartBattle()
    {
        StartCoroutine(AppearRoutine());
    }

    private void OnDisable()
    {
        CleanupOffensivesOnDisable();
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

                GameObject muzzle = Instantiate(fireStartEffect, spawnPos, rot);
                RegisterBossOffensive(muzzle, true);

                GameObject projectile = Instantiate(fireWallPrefab, spawnPos, rot);
                RegisterBossOffensive(projectile);
                projectile.GetComponent<BossProjectile>()?.Setup(ElementType.Fire);
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

            GameObject rayObj = Instantiate(aquaRayPrefab, spawnPos, rot);
            RegisterBossOffensive(rayObj);
            rayObj.GetComponent<BossProjectile>()?.Setup(ElementType.Water);
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

            GameObject wallObj = Instantiate(electricWallPrefab, spawnPos, rot);
            RegisterBossOffensive(wallObj);
            wallObj.GetComponent<BossProjectile>()?.Setup(ElementType.Electric);
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

            GameObject rayObj = Instantiate(electricRayPrefab, spawnPos, rot);
            RegisterBossOffensive(rayObj);
            BossProjectile electricRay = rayObj.GetComponent<BossProjectile>();
            if (electricRay == null)
            {
                electricRay = rayObj.AddComponent<ElectricLaserRay>();
            }

            electricRay.Setup(ElementType.Electric);
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
}
