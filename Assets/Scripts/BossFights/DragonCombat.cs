using System;
using System.Collections;
using UnityEngine;

public class DragonCombat : BossCombatBase, IBossDamageModifier, IBossPhaseHandler, IBossBattleResetNotifier, IBossStartPositioner
{
    [Header("References")]
    [SerializeField] private BossHealth dragonBossHealth;
    [SerializeField] private Animator dragonAnimator;
    [SerializeField] private Transform projectileSpawnPivot;

    [Header("Positions")]
    [SerializeField] private Transform flyingStandbyPoint;
    [SerializeField] private Transform landingPoint;

    [Header("Flying Restriction Zone")]
    [SerializeField] private Collider2D flyingMovementBlockZone;
    [SerializeField] private Collider2D flyingBombBlockTriggerZone;

    [Header("Dragon Stats")]
    [SerializeField] private int maxHp = 22250;
    [SerializeField] private ElementType dragonElement = ElementType.Dark;

    [Header("Element Multipliers")]
    [SerializeField] private ElementType weakElement = ElementType.Light;
    [SerializeField] private float advantageMultiplier = 2f;
    [SerializeField] private float neutralMultiplier = 1f;
    [SerializeField] private float disadvantageMultiplier = 0.5f;

    [Header("Eternal Night")]
    [SerializeField] private GameObject eternalNightWallPrefab;
    [SerializeField] private ElementType wallElement = ElementType.Dark;
    [SerializeField] private int wallDamage = 1;
    [SerializeField] private float wallMoveSpeed = 6f;
    [SerializeField] private float wallScaleMultiplier = 1.75f;
    [SerializeField] private float wallSpawnRadius = 2.5f;
    [SerializeField] private float[] wallAngles = { -90f, -45f, 0f, 45f, 90f };
    [SerializeField] private float secondVolleyDelay = 0.7f;
    [SerializeField] private float patternDelay = 5.5f;

    [Header("Landing Sequence")]
    [SerializeField] private int animatorLayerIndex;
    [SerializeField] private string landingStateName = "Landing";
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string takeOffStateName = "TakeOff";
    [SerializeField] private string flyingStateName = "Flying";
    [SerializeField] private float landingClipDuration = 0.8f;
    [SerializeField] private float idleHoldDuration = 1f;
    [SerializeField] private float takeOffClipDuration = 0.8f;
    [SerializeField] private float sequenceGap = 0.05f;

    [Header("Visual Scale")]
    [SerializeField] private Transform dragonVisualRoot;
    [SerializeField] private float landingScaleMultiplier = 1f;
    [SerializeField] private float flyingScaleMultiplier = 2.5f;

    private const string PlayerTag = "Player";

    public static DragonCombat ActiveInstance { get; private set; }

    public event Action OnBattleReset;

    private Coroutine battleLoopRoutine;
    private Player player;
    private bool isBattleActive;
    private bool isStealthSkipped;
    private bool battleResultHandled;
    private Vector3 baseVisualScale = Vector3.one;
    private bool hasBaseVisualScale;
    private float currentVisualScaleMultiplier = 1f;

    private void Awake()
    {
        ActiveInstance = this;

        if (dragonBossHealth == null) dragonBossHealth = GetComponent<BossHealth>();
        if (dragonAnimator == null) dragonAnimator = GetComponentInChildren<Animator>();
        if (dragonVisualRoot == null && dragonAnimator != null) dragonVisualRoot = dragonAnimator.transform;

        CacheVisualScale();

        ApplyDragonStats();
        ApplyFlyingStandbyPose();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        ActiveInstance = this;
        ApplyFlyingStandbyPose();
        PlayerHealth.OnPlayerDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDeath -= HandlePlayerDeath;
        SetFlyingRestrictionZoneActive(false);

        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }
    }

    public void Init(bool isStealthActive)
    {
        isStealthSkipped = isStealthActive;
        if (!isStealthSkipped) return;

        Debug.Log("[Dragon] Stealth route detected. Dragon battle skipped.");
        gameObject.SetActive(false);
    }

    public override void StartBattle()
    {
        if (isStealthSkipped)
        {
            Debug.Log("[Dragon] StartBattle ignored because stealth skip is active.");
            gameObject.SetActive(false);
            return;
        }

        if (isBattleActive)
        {
            return;
        }

        isBattleActive = true;
        battleResultHandled = false;

        if (dragonBossHealth != null)
        {
            dragonBossHealth.currentHP = dragonBossHealth.maxHP;
        }

        StartCoroutine(BattleStartSequence());
    }

    public float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier)
    {
        if (attackType == weakElement) return advantageMultiplier;
        if (attackType == dragonElement) return neutralMultiplier;
        return disadvantageMultiplier;
    }

    public void OnBossHpChanged(int currentHp, int maxHpValue)
    {
        if (!isBattleActive || battleResultHandled)
        {
            return;
        }

        if (currentHp <= 0)
        {
            battleResultHandled = true;
            HandleBossDefeated();
        }
    }

    private void ApplyDragonStats()
    {
        if (dragonBossHealth == null)
        {
            return;
        }

        dragonBossHealth.maxHP = maxHp;
        dragonBossHealth.currentHP = maxHp;
        dragonBossHealth.currentElement = dragonElement;
        dragonBossHealth.bossName = "Dragon";
    }

    private void ApplyFlyingStandbyPose()
    {
        if (flyingStandbyPoint != null)
        {
            transform.position = flyingStandbyPoint.position;
        }

        currentVisualScaleMultiplier = flyingScaleMultiplier;
        ApplyVisualScale(currentVisualScaleMultiplier);
        PlayStateImmediate(flyingStateName);
        SetFlyingRestrictionZoneActive(true);
    }

    public void SetToPointAImmediate()
    {
        ApplyFlyingStandbyPose();
    }

    private IEnumerator BattleStartSequence()
    {
        ResolvePlayer();
        SetPlayerStun(true);

        SetFlyingRestrictionZoneActive(false);

        if (landingPoint != null)
        {
            transform.position = landingPoint.position;
        }

        currentVisualScaleMultiplier = landingScaleMultiplier;
        ApplyVisualScale(currentVisualScaleMultiplier);
        PlayStateImmediate(landingStateName);
        yield return new WaitForSeconds(landingClipDuration);

        if (sequenceGap > 0f) yield return new WaitForSeconds(sequenceGap);

        PlayStateImmediate(idleStateName);
        yield return new WaitForSeconds(idleHoldDuration);

        if (sequenceGap > 0f) yield return new WaitForSeconds(sequenceGap);

        PlayStateImmediate(takeOffStateName);
        yield return new WaitForSeconds(takeOffClipDuration);

        if (sequenceGap > 0f) yield return new WaitForSeconds(sequenceGap);

        currentVisualScaleMultiplier = flyingScaleMultiplier;
        ApplyVisualScale(currentVisualScaleMultiplier);
        PlayStateImmediate(flyingStateName);
        SetFlyingRestrictionZoneActive(true);

        SetPlayerStun(false);
        battleLoopRoutine = StartCoroutine(EternalNightLoop());
    }

    private IEnumerator EternalNightLoop()
    {
        while (isBattleActive)
        {
            yield return StartCoroutine(FireEternalNightVolley());
            yield return new WaitForSeconds(secondVolleyDelay);

            yield return StartCoroutine(FireEternalNightVolley());
            yield return new WaitForSeconds(patternDelay);
        }
    }

    private IEnumerator FireEternalNightVolley()
    {
        if (eternalNightWallPrefab == null)
        {
            yield break;
        }

        bool isAquaRay = eternalNightWallPrefab.GetComponent<AquaRay>() != null;
        Quaternion pivotRotation = projectileSpawnPivot != null ? projectileSpawnPivot.rotation : transform.rotation;
        Vector3 pivotPosition = projectileSpawnPivot != null ? projectileSpawnPivot.position : transform.position;

        int volleyCount = isAquaRay ? 6 : wallAngles.Length;
        for (int i = 0; i < volleyCount; i++)
        {
            Vector3 fireDirection;
            if (isAquaRay)
            {
                float fixedAngle = i * 60f; // 0, 60, 120, 180, 240, 300
                fireDirection = Quaternion.Euler(0f, 0f, fixedAngle) * Vector3.right;
            }
            else
            {
                float angle = wallAngles[i];
                fireDirection = pivotRotation * (Quaternion.Euler(0f, 0f, angle) * Vector3.down);
            }

            fireDirection.z = 0f;
            fireDirection.Normalize();

            Quaternion projectileRotation = isAquaRay
                ? Quaternion.FromToRotation(Vector3.down, fireDirection)
                : Quaternion.FromToRotation(Vector3.right, fireDirection);
            Vector3 spawnPos = pivotPosition + (fireDirection * wallSpawnRadius);

            GameObject wallObj = Instantiate(eternalNightWallPrefab, spawnPos, projectileRotation);
            wallObj.transform.localScale *= wallScaleMultiplier;

            BossProjectile projectile = wallObj.GetComponent<BossProjectile>();
            if (projectile == null) continue;

            projectile.damage = wallDamage;
            projectile.speed = wallMoveSpeed;
            projectile.Setup(wallElement);
        }

        yield return null;
    }

    private void PlayStateImmediate(string stateName)
    {
        if (dragonAnimator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        dragonAnimator.Play(stateName, animatorLayerIndex, 0f);
    }

    private void LateUpdate()
    {
        // Animator scale curves can overwrite localScale every frame.
        // Re-apply configured visual scale after animation evaluation.
        if (currentVisualScaleMultiplier > 0f)
        {
            ApplyVisualScale(currentVisualScaleMultiplier);
        }
    }

    private void CacheVisualScale()
    {
        if (hasBaseVisualScale) return;
        if (dragonVisualRoot == null) return;

        baseVisualScale = dragonVisualRoot.localScale;
        hasBaseVisualScale = true;
    }

    private void ApplyVisualScale(float multiplier)
    {
        if (dragonVisualRoot == null) return;

        CacheVisualScale();
        if (!hasBaseVisualScale) return;

        float safeMultiplier = Mathf.Max(0.01f, multiplier);
        dragonVisualRoot.localScale = baseVisualScale * safeMultiplier;
    }

    private void SetFlyingRestrictionZoneActive(bool isActive)
    {
        if (flyingMovementBlockZone != null)
        {
            flyingMovementBlockZone.enabled = isActive;
        }

        if (flyingBombBlockTriggerZone != null)
        {
            flyingBombBlockTriggerZone.enabled = isActive;
        }
    }

    public static bool IsPositionBlockedByFlyingZone(Vector2 worldPosition)
    {
        if (ActiveInstance == null)
        {
            return false;
        }

        bool movementBlocked = ActiveInstance.flyingMovementBlockZone != null
                               && ActiveInstance.flyingMovementBlockZone.enabled
                               && ActiveInstance.flyingMovementBlockZone.OverlapPoint(worldPosition);

        bool bombBlocked = ActiveInstance.flyingBombBlockTriggerZone != null
                           && ActiveInstance.flyingBombBlockTriggerZone.enabled
                           && ActiveInstance.flyingBombBlockTriggerZone.OverlapPoint(worldPosition);

        return movementBlocked || bombBlocked;
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag(PlayerTag);
        if (playerObj != null)
        {
            player = playerObj.GetComponent<Player>();
        }
    }

    private void SetPlayerStun(bool shouldStun)
    {
        ResolvePlayer();
        if (player == null)
        {
            return;
        }

        player.SetCanMove(!shouldStun);
        if (shouldStun)
        {
            player.StopMoving();
        }
    }

    private void HandlePlayerDeath()
    {
        if (!isBattleActive || battleResultHandled)
        {
            return;
        }

        battleResultHandled = true;
        Debug.Log("[Dragon] Player defeated.");
        ResetBattleForRetry();
    }

    private void ResetBattleForRetry()
    {
        isBattleActive = false;
        battleResultHandled = false;

        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
        }

        if (dragonBossHealth != null)
        {
            dragonBossHealth.currentHP = dragonBossHealth.maxHP;
        }

        ApplyFlyingStandbyPose();

        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }

        gameObject.SetActive(false);
        OnBattleReset?.Invoke();
    }

    private void HandleBossDefeated()
    {
        isBattleActive = false;

        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
        }

        Debug.Log("[Dragon] Defeated.");
    }
}

