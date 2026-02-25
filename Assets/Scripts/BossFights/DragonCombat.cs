using System.Collections;
using UnityEngine;

public class DragonCombat : BossCombatBase, IBossDamageModifier, IBossPhaseHandler
{
    [Header("References")]
    [SerializeField] private BossHealth bossHealth;
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

    private const string PlayerTag = "Player";

    public static DragonCombat ActiveInstance { get; private set; }

    private Coroutine battleLoopRoutine;
    private Player player;
    private bool isBattleActive;
    private bool isStealthSkipped;
    private bool battleResultHandled;

    private void Awake()
    {
        ActiveInstance = this;

        if (bossHealth == null) bossHealth = GetComponent<BossHealth>();
        if (dragonAnimator == null) dragonAnimator = GetComponentInChildren<Animator>();

        ApplyDragonStats();
        ApplyFlyingStandbyPose();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
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

        if (bossHealth != null)
        {
            bossHealth.currentHP = bossHealth.maxHP;
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
        if (bossHealth == null)
        {
            return;
        }

        bossHealth.maxHP = maxHp;
        bossHealth.currentHP = maxHp;
        bossHealth.currentElement = dragonElement;
        bossHealth.bossName = "Dragon";
    }

    private void ApplyFlyingStandbyPose()
    {
        if (flyingStandbyPoint != null)
        {
            transform.position = flyingStandbyPoint.position;
        }

        PlayStateImmediate(flyingStateName);
        SetFlyingRestrictionZoneActive(true);
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

        PlayStateImmediate(landingStateName);
        yield return new WaitForSeconds(landingClipDuration);

        if (sequenceGap > 0f) yield return new WaitForSeconds(sequenceGap);

        PlayStateImmediate(idleStateName);
        yield return new WaitForSeconds(idleHoldDuration);

        if (sequenceGap > 0f) yield return new WaitForSeconds(sequenceGap);

        PlayStateImmediate(takeOffStateName);
        yield return new WaitForSeconds(takeOffClipDuration);

        if (sequenceGap > 0f) yield return new WaitForSeconds(sequenceGap);

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

        Quaternion pivotRotation = projectileSpawnPivot != null ? projectileSpawnPivot.rotation : transform.rotation;
        Vector3 pivotPosition = projectileSpawnPivot != null ? projectileSpawnPivot.position : transform.position;

        for (int i = 0; i < wallAngles.Length; i++)
        {
            float angle = wallAngles[i];
            // 기준 0도는 화면의 아래 방향(Vector3.down)
            Vector3 fireDirection = pivotRotation * (Quaternion.Euler(0f, 0f, angle) * Vector3.down);
            fireDirection.z = 0f;
            fireDirection.Normalize();

            Quaternion projectileRotation = Quaternion.FromToRotation(Vector3.right, fireDirection);
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
        isBattleActive = false;

        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
        }

        Debug.Log("[Dragon] Player defeated.");
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
