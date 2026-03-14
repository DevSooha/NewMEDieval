using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class KnightCombat : BossCombatBase, IBossDamageModifier, IBossBattleResetNotifier, IBossStartPositioner
{
    private static readonly PropertyName MagicCircleRefId = new("8b93daa6954238d4599314cb95d7c374");
    private static readonly PropertyName ThornRefId = new("c8f0a49c6655fdb45a07db761386b42c");
    private const string MagicCircleChildName = "VFX_MagicCircle";
    private const string ThornChildName = "Thorn";
    private const string AttackLeftFullPath = "Base Layer.Attack_L";
    private const string AttackRightFullPath = "Base Layer.Attack_R";
    private enum SwingDirection
    {
        Left = -1,
        Right = 1
    }

    [Header("Boss Core")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Animator bossAnimator;

    [Header("Anchor Points")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;

    [Header("Pattern Settings")]
    [SerializeField] private float sideOffset = 1f;
    [SerializeField] private float sideYOffset = 0.5f;
    [SerializeField] private float openingTeleportDelay = 0.1f;
    [SerializeField] private float teleportDuration = 0.1f;
    [SerializeField] private float swordSwingDuration = 0.5f;
    [SerializeField] private float patternMoveDelay = 1f;
    [SerializeField] private float returnAttackDelay = 0.1f;
    [SerializeField] private float patternEndWaitDuration = 4f;

    [Header("Sword Damage")]
    [SerializeField] private int swordDamage = 1;
    [SerializeField] private float swordHitRangeTiles = 2f;
    [SerializeField] private int swordHitFrame = 11;
    [SerializeField] private float swordAnimationFps = 30f;
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private bool useScaleFlipForFacing = false;

    [Header("Stained Sword")]
    [SerializeField] private StainedSwordProjectile stainedSwordProjectilePrefab;
    [SerializeField] private float projectileSpawnOffset = 1f;

    [Header("Latent Thorn")]
    [SerializeField] private PlayableDirector thornTimeline;
    [SerializeField] private GameObject thornTimelinePrefab;
    [SerializeField] private Transform thornTimelineParent;
    [SerializeField] private Transform thornPositionsParent;
    [SerializeField] private LatentThornHitbox thornHitboxPrefab;
    [SerializeField] private float thornWarningDuration = 0.5f;
    [SerializeField] private float thornRiseDuration = 0.1f;
    [SerializeField] private float thornHoldDuration = 2f;
    [SerializeField] private float thornDespawnDuration = 0.3f;

    private readonly List<Transform> thornSpawnPoints = new();
    private readonly List<LatentThornHitbox> thornHitboxes = new();
    private readonly List<PlayableDirector> thornTimelines = new();
    private readonly List<StainedSwordProjectile> spawnedProjectiles = new();

    private Transform aut1RespawnPoint;


    private Transform playerTransform;
    private Coroutine battleLoopRoutine;
    private Coroutine deathRoutine;
    private bool isBattleRunning;
    private SwingDirection lastSwingDirection = SwingDirection.Left;

    public event Action OnBattleReset;

    protected override bool UseCollisionInvulnerability => false;

    private void Awake()
    {
        if (bossHealth == null) bossHealth = GetComponent<BossHealth>();

        if (bossHealth != null)
        {
            bossHealth.maxHP = 5000;
            bossHealth.currentHP = 5000;
            bossHealth.currentElement = ElementType.None;
        }

        CacheThornSpawnPoints();
        BuildThornHitboxes();
        ResolvePlayerTransform();
    }

    private void OnEnable()
    {
        ResumeBossPresentation();
        RegisterPlayerDeathBaseHandler(HandlePlayerDeath);
        ResolvePlayerTransform();
    }

    private void OnDisable()
    {
        UnregisterPlayerDeathBaseHandler(HandlePlayerDeath);
        CleanupOffensivesOnDisable();
    }

    public override void StartBattle()
    {
        ResumeBossPresentation();
        if (isBattleRunning) return;
        if (!ResolvePlayerTransform()) return;

        isBattleRunning = true;
        battleLoopRoutine = StartCoroutine(BattleLoop());
    }

    public void SetToPointAImmediate()
    {
        if (pointA != null)
        {
            transform.position = pointA.position;
        }
    }

    public float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier)
    {
        return attackType == ElementType.Light ? baseMultiplier * 2f : baseMultiplier;
    }

    private IEnumerator BattleLoop()
    {
        bool isLeftPattern = true;
        bool includeOpeningTeleport = true;

        while (isBattleRunning && !IsBossDefeated())
        {
            yield return RunPattern(isLeftPattern, includeOpeningTeleport);

            includeOpeningTeleport = false;
            isLeftPattern = !isLeftPattern;
        }
    }

    private IEnumerator RunPattern(bool isLeftPattern, bool includeOpeningTeleport)
    {
        if (!ResolvePlayerTransform() || IsBossDefeated()) yield break;

        if (includeOpeningTeleport)
        {
            if (openingTeleportDelay > 0f)
            {
                yield return new WaitForSeconds(openingTeleportDelay);
            }
            Vector3 sidePosition = GetPlayerSidePosition(isLeftPattern, useOppositeSide: false);
            yield return TeleportSmooth(sidePosition, teleportDuration);
        }

        lastSwingDirection = isLeftPattern ? SwingDirection.Left : SwingDirection.Right;
        FaceDirection(lastSwingDirection == SwingDirection.Left ? Vector2.left : Vector2.right);
        yield return SwingSword(lastSwingDirection, swordSwingDuration);

        StainedSwordProjectile patternProjectile = SpawnStainedSword();

        yield return new WaitForSeconds(patternMoveDelay);

        Transform returnPoint = isLeftPattern ? pointA : pointB;
        if (returnPoint != null)
        {
            yield return TeleportSmooth(returnPoint.position, teleportDuration);
        }

        if (returnAttackDelay > 0f)
        {
            yield return new WaitForSeconds(returnAttackDelay);
        }

        if (patternProjectile != null)
        {
            yield return WaitForProjectileToDespawn(patternProjectile);
        }

        yield return SwingSword(lastSwingDirection, swordSwingDuration);

        PlayThornTimelines();

        yield return HandleLatentThorn();

        if (patternEndWaitDuration > 0f)
        {
            yield return new WaitForSeconds(patternEndWaitDuration);
        }

        if (ResolvePlayerTransform())
        {
            Vector3 nextSidePosition = GetPlayerSidePosition(isLeftPattern, useOppositeSide: true);
            yield return TeleportSmooth(nextSidePosition, teleportDuration);
        }
    }

    private bool ResolvePlayerTransform()
    {
        return TryResolvePlayerTransform(ref playerTransform);
    }

    private IEnumerator TeleportSmooth(Vector3 targetPosition, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, targetPosition, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.position = targetPosition;
    }

    private IEnumerator SwingSword(SwingDirection swingDirection, float duration)
    {
        PlayAttackAnimation(swingDirection);

        float safeFps = Mathf.Max(1f, swordAnimationFps);
        float hitCheckDelay = Mathf.Clamp(swordHitFrame / safeFps, 0f, duration);

        if (hitCheckDelay > 0f)
        {
            yield return new WaitForSeconds(hitCheckDelay);
        }

        TryApplySwordDamage(swingDirection);

        float remain = duration - hitCheckDelay;
        if (remain > 0f)
        {
            yield return new WaitForSeconds(remain);
        }

        StopAttackAnimation();
    }

    private IEnumerator HandleLatentThorn()
    {
        float timelineDuration = ResolveConfiguredThornTimelineDuration();

        if (thornWarningDuration > 0f)
        {
            yield return new WaitForSeconds(thornWarningDuration);
        }

        EnsureThornSetup();

        float damageDuration = Mathf.Max(0f, timelineDuration - thornWarningDuration);
        foreach (LatentThornHitbox thorn in thornHitboxes)
        {
            if (thorn != null)
            {
                thorn.ActivateForSeconds(damageDuration);
            }
        }

        if (damageDuration > 0f)
        {
            yield return new WaitForSeconds(damageDuration);
        }

        StopThornTimelines();
    }

    private StainedSwordProjectile SpawnStainedSword()
    {
        if (stainedSwordProjectilePrefab == null) return null;
        if (!ResolvePlayerTransform()) return null;

        Vector3 swingOffset = Vector3.right * (-(int)lastSwingDirection * projectileSpawnOffset);
        Vector3 spawnPosition = transform.position + swingOffset;

        StainedSwordProjectile projectile = Instantiate(stainedSwordProjectilePrefab, spawnPosition, Quaternion.identity);
        projectile.Initialize(playerTransform, HandleProjectileDestroyed);
        RegisterBossOffensive(projectile.gameObject);
        spawnedProjectiles.Add(projectile);
        return projectile;
    }

    private void HandleProjectileDestroyed(StainedSwordProjectile projectile)
    {
        spawnedProjectiles.Remove(projectile);

        if (projectile != null)
        {
            UnregisterBossOffensive(projectile.gameObject);
        }
    }

    private void FaceDirection(Vector2 direction)
    {
        if (!useScaleFlipForFacing) return;
        if (Mathf.Abs(direction.x) < 0.01f) return;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x);
        transform.localScale = scale;
    }

    private void PlayAttackAnimation(SwingDirection swingDirection)
    {
        if (bossAnimator == null) return;
        bossAnimator.speed = 1f;

        // Force explicit full state path to avoid any serialized-name mismatch.
        string fullPath = swingDirection == SwingDirection.Left ? AttackLeftFullPath : AttackRightFullPath;
        bossAnimator.Play(fullPath, 0, 0f);
    }

    private void TryApplySwordDamage(SwingDirection swingDirection)
    {
        if (!ResolvePlayerTransform()) return;

        float directionSign = swingDirection == SwingDirection.Left ? 1f : -1f;
        Vector3 delta = playerTransform.position - transform.position;
        float horizontalDistanceInAttackDirection = delta.x * directionSign;
        float hitRange = Mathf.Max(0f, swordHitRangeTiles);

        if (horizontalDistanceInAttackDirection < 0f || horizontalDistanceInAttackDirection > hitRange)
        {
            return;
        }

        BossHitResolver.TryApplyBossHit(
            playerTransform,
            swordDamage,
            transform.position
        );
    }

    private bool IsBossDefeated()
    {
        return bossHealth != null && bossHealth.currentHP <= 0;
    }

    private void HandlePlayerDeath()
    {
        CleanupBossPresentationOnPlayerDeath();
        if (deathRoutine != null) return;
        deathRoutine = StartCoroutine(HandlePlayerDeathRoutine());
    }

    private IEnumerator HandlePlayerDeathRoutine()
    {
        if (UIManager.Instance != null)
        {
            yield return UIManager.Instance.FadeOut(0.5f);
        }

        yield return null;

        Transform player = Player.Instance != null ? Player.Instance.transform : null;
        if (player != null)
        {
            player.gameObject.SetActive(true);

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Resurrect();
            }

            Transform resolvedRespawn = ResolveAut1RespawnPoint();
            if (resolvedRespawn != null)
            {
                player.position = resolvedRespawn.position;
            }

            if (player.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.UpdateRoomStateAfterTeleport();
                RoomManager.Instance.SyncCameraToPlayer();
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideSelectPanel();
            yield return UIManager.Instance.FadeIn(0.1f);
        }

        ResetBattleForRetry();
        deathRoutine = null;
    }

    private Transform ResolveAut1RespawnPoint()
    {
        if (aut1RespawnPoint != null) return aut1RespawnPoint;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var rootGameObjects = scene.GetRootGameObjects();

        foreach (var rootGO in rootGameObjects)
        {
            if (rootGO == null) continue;

            // 깊이 우선 탐색 스택으로 하위 트리를 순회
            var stack = new Stack<Transform>();
            stack.Push(rootGO.transform);

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t == null) continue;

                if (t.name == "PlayerSpawnPoint")
                {
                    // 찾은 노드에서부터 부모를 타고 올라가며 aut_1이 있는지 확인
                    Transform cursor = t;
                    while (cursor != null)
                    {
                        if (cursor.name == "aut_1")
                        {
                            aut1RespawnPoint = t;
                            return aut1RespawnPoint;
                        }
                        cursor = cursor.parent;
                    }
                }

                // 자식들을 스택에 추가
                // foreach (Transform child in t) 가 IEnumerator를 사용하므로 null 안정성 유지
                for (int i = 0; i < t.childCount; i++)
                {
                    var child = t.GetChild(i);
                    if (child != null)
                    {
                        stack.Push(child);
                    }
                }
            }
        }

        return null;
    }

    private void ResetBattleForRetry()
    {
        ResumeBossPresentation();
        isBattleRunning = false;

        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
        }

        foreach (StainedSwordProjectile projectile in spawnedProjectiles)
        {
            if (projectile != null)
            {
                UnregisterBossOffensive(projectile.gameObject);
                Destroy(projectile.gameObject);
            }
        }
        spawnedProjectiles.Clear();

        foreach (LatentThornHitbox thorn in thornHitboxes)
        {
            if (thorn != null)
            {
                thorn.ResetState();
            }
        }

        StopThornTimelines();

        if (bossHealth != null)
        {
            bossHealth.currentHP = bossHealth.maxHP;
        }

        CleanupOffensivesOnBattleReset();

        SetToPointAImmediate();

        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }

        gameObject.SetActive(false);
        OnBattleReset?.Invoke();
    }

    private void CacheThornSpawnPoints()
    {
        thornSpawnPoints.Clear();

        if (thornPositionsParent == null) return;

        foreach (Transform child in thornPositionsParent)
        {
            thornSpawnPoints.Add(child);
        }
    }

    private void BuildThornHitboxes()
    {
        foreach (LatentThornHitbox thorn in thornHitboxes)
        {
            if (thorn != null)
            {
                UnregisterBossOffensive(thorn.gameObject);
                Destroy(thorn.gameObject);
            }
        }

        thornHitboxes.Clear();

        if (thornHitboxPrefab == null) return;

        Transform parent = thornPositionsParent != null ? thornPositionsParent : transform;
        foreach (Transform spawnPoint in thornSpawnPoints)
        {
            if (spawnPoint == null) continue;

            LatentThornHitbox thorn = Instantiate(thornHitboxPrefab, spawnPoint.position, Quaternion.identity, parent);
            thorn.ResetState();
            RegisterBossOffensive(thorn.gameObject);
            thornHitboxes.Add(thorn);
        }
    }

    private void EnsureThornSetup()
    {
        if (thornSpawnPoints.Count == 0)
        {
            CacheThornSpawnPoints();
        }

        if (thornHitboxes.Count != thornSpawnPoints.Count)
        {
            BuildThornHitboxes();
        }

        EnsureThornTimelines();

        for (int i = 0; i < thornHitboxes.Count; i++)
        {
            if (thornHitboxes[i] == null || i >= thornSpawnPoints.Count || thornSpawnPoints[i] == null)
            {
                continue;
            }

            thornHitboxes[i].transform.position = thornSpawnPoints[i].position;
            thornHitboxes[i].gameObject.SetActive(true);
        }

        for (int i = 0; i < thornTimelines.Count; i++)
        {
            if (thornTimelines[i] == null || i >= thornSpawnPoints.Count || thornSpawnPoints[i] == null)
            {
                continue;
            }

            thornTimelines[i].transform.position = thornSpawnPoints[i].position;
        }
    }

    private void EnsureThornTimelines()
    {
        if (thornSpawnPoints.Count == 0)
        {
            return;
        }

        GameObject sourcePrefab = null;
        if (thornTimelinePrefab != null)
        {
            sourcePrefab = thornTimelinePrefab;
        }
        else if (thornTimeline != null)
        {
            sourcePrefab = thornTimeline.gameObject;
        }

        if (sourcePrefab == null)
        {
            return;
        }

        for (int i = thornTimelines.Count - 1; i >= 0; i--)
        {
            if (thornTimelines[i] != null)
            {
                continue;
            }

            thornTimelines.RemoveAt(i);
        }

        while (thornTimelines.Count > thornSpawnPoints.Count)
        {
            int last = thornTimelines.Count - 1;
            if (thornTimelines[last] != null)
            {
                UnregisterBossOffensive(thornTimelines[last].gameObject);
                Destroy(thornTimelines[last].gameObject);
            }
            thornTimelines.RemoveAt(last);
        }

        while (thornTimelines.Count < thornSpawnPoints.Count)
        {
            int index = thornTimelines.Count;
            Transform spawnPoint = thornSpawnPoints[index];
            if (spawnPoint == null)
            {
                thornTimelines.Add(null);
                continue;
            }

            Transform parent = thornPositionsParent != null
                ? thornPositionsParent.parent
                : thornTimelineParent;
            GameObject instance = parent != null
                ? Instantiate(sourcePrefab, spawnPoint.position, Quaternion.identity, parent)
                : Instantiate(sourcePrefab, spawnPoint.position, Quaternion.identity);
            PlayableDirector director = instance.GetComponent<PlayableDirector>();
            if (director == null)
            {
                Debug.LogWarning("KnightCombat: ThornTimeline prefab has no PlayableDirector.", instance);
                Destroy(instance);
                thornTimelines.Add(null);
                continue;
            }

            director.playOnAwake = false;
            director.Stop();
            director.time = 0;
            BindThornTimelineReferences(director);
            RegisterBossOffensive(instance, true);
            thornTimelines.Add(director);
        }

        thornTimeline = thornTimelines.Count > 0 ? thornTimelines[0] : null;
    }

    private void PlayThornTimelines()
    {
        EnsureThornTimelines();

        foreach (PlayableDirector director in thornTimelines)
        {
            if (director == null) continue;
            if (!director.gameObject.activeSelf)
            {
                director.gameObject.SetActive(true);
            }
            director.Stop();
            director.time = 0;
            director.Play();
        }
    }

    private void StopThornTimelines()
    {
        foreach (PlayableDirector director in thornTimelines)
        {
            if (director == null) continue;
            director.Stop();
            director.time = 0;
            if (director.gameObject.activeSelf)
            {
                director.gameObject.SetActive(false);
            }
        }
    }

    private float ResolveConfiguredThornTimelineDuration()
    {
        return Mathf.Max(0f, thornWarningDuration + thornRiseDuration + thornHoldDuration + thornDespawnDuration);
    }

    private void BindThornTimelineReferences(PlayableDirector director)
    {
        if (director == null) return;

        Transform root = director.transform;
        Transform magicCircle = FindChildByName(root, MagicCircleChildName);
        Transform thorn = FindChildByName(root, ThornChildName);

        if (magicCircle != null)
        {
            director.SetReferenceValue(MagicCircleRefId, magicCircle.gameObject);
        }

        if (thorn != null)
        {
            director.SetReferenceValue(ThornRefId, thorn.gameObject);
        }
    }

    private void StopAttackAnimation()
    {
        if (bossAnimator == null) return;

        if (!string.IsNullOrWhiteSpace(idleStateName))
        {
            int idleHash = Animator.StringToHash(idleStateName);
            if (bossAnimator.HasState(0, idleHash))
            {
                bossAnimator.Play(idleHash, 0, 0f);
                bossAnimator.speed = 1f;
                return;
            }
        }
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;

        var stack = new Stack<Transform>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            if (current == null) continue;

            if (current.name == childName)
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                stack.Push(current.GetChild(i));
            }
        }

        return null;
    }

    private IEnumerator WaitForProjectileToDespawn(StainedSwordProjectile projectile)
    {
        const float maxWaitSeconds = 5f;
        float elapsed = 0f;

        while (projectile != null && elapsed < maxWaitSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (projectile != null)
        {
            Debug.LogWarning("KnightCombat: StainedSwordProjectile did not despawn in time.");
        }
    }

    private Vector3 GetPlayerSidePosition(bool isLeftPattern, bool useOppositeSide)
    {
        Vector3 horizontalOffset;
        if (!useOppositeSide)
        {
            horizontalOffset = isLeftPattern ? Vector3.left : Vector3.right;
        }
        else
        {
            horizontalOffset = isLeftPattern ? Vector3.right : Vector3.left;
        }

        return playerTransform.position + horizontalOffset * sideOffset + Vector3.up * sideYOffset;
    }
}

