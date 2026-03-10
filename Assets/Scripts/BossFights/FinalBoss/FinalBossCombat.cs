using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Tilemaps;

public class FinalBossCombat : BossCombatBase, IBossDamageModifier, IBossPhaseHandler, IBossBattleResetNotifier, IBossStartPositioner
{
    private static readonly PropertyName MagicCircleRefId = new("8b93daa6954238d4599314cb95d7c374");
    private static readonly PropertyName ThornRefId = new("c8f0a49c6655fdb45a07db761386b42c");
    private const string MagicCircleChildName = "VFX_MagicCircle";
    private const string ThornChildName = "Thorn";

    private enum CombatPhase
    {
        Phase1,
        Phase2
    }

    [Header("Core")]
    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform stairRespawnPoint;
    [SerializeField] private int maxHp = 5400;
    [SerializeField] private string hiddenSceneName = "BOSS";

    [Header("Room Bounds")]
    [SerializeField] private Collider2D roomBoundsOverride;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private float teleportInset = 0.35f;
    [SerializeField] private float defaultTeleportDuration = 0.1f;

    [Header("Damage")]
    [SerializeField] private int damagePerHit = 1;
    [SerializeField] private Vector2 oneTileHitbox = Vector2.one;

    [Header("Latent Thorn Pattern")]
    [SerializeField] private Transform[] latentThornCastPoints;
    [SerializeField] private PlayableDirector latentThornTimeline;
    [SerializeField] private GameObject latentThornTimelinePrefab;
    [SerializeField] private Transform latentThornTimelineParent;
    [SerializeField] private Transform latentThornPositionsParent;
    [SerializeField] private LatentThornHitbox latentThornHitboxPrefab;
    [SerializeField] private float latentSwordSwingDuration = 0.5f;
    [SerializeField] private float latentWarningDuration = 0.5f;
    [SerializeField] private float latentRiseDuration = 0.1f;
    [SerializeField] private float latentHoldDuration = 5f;
    [SerializeField] private float latentDespawnDuration = 0.3f;

    [Header("Carma Excision")]
    [SerializeField] private CarmaExcisionTrueHitbox carmaTrueHitboxPrefab;
    [SerializeField] private float carmaInitialDelay = 2f;
    [SerializeField] private float carmaOpeningSwingDuration = 0.5f;
    [SerializeField] private float carmaTeleportDelay1 = 0.5f;
    [SerializeField] private float carmaTeleportDelay2 = 0.1f;
    [SerializeField] private float carmaTeleportDelay3 = 0.1f;
    [SerializeField] private float carmaFakeAttackDuration = 0.4f;
    [SerializeField] private float carmaFinalTeleportDuration = 0.1f;
    [SerializeField] private float carmaTrueHitboxDuration = 0.3f;
    [SerializeField] private float carmaPhase1EndDelay = 3f;
    [SerializeField] private float carmaPhase2EndDelay = 2f;

    [Header("Phase 2 Transition: Bedimmed Wall")]
    [SerializeField] private int phase2HpThreshold = 2500;
    [SerializeField] private FinalBossBedimmedWallProjectile bedimmedWallProjectilePrefab;
    [SerializeField] private Transform bedimmedWallsParent;
    [SerializeField] private Transform[] bedimmedWallSpawnPoints;
    [SerializeField] private float bedimmedTeleportDuration = 0.5f;
    [SerializeField] private float bedimmedSwingDuration = 0.5f;
    [SerializeField] private float bedimmedSpawnDuration = 0.7f;
    [SerializeField] private float bedimmedProjectileSpeed = 6f; // 192px/s == 6 tile/s
    [SerializeField] private float bedimmedPostDelay = 2f;
    [SerializeField] private float bedimmedSpawnRadius = 1.25f;

    [Header("Phase 2 Loop: Hand of Time")]
    [SerializeField] private FinalBossHandOfTimeBurstController handOfTimeController;
    [SerializeField] private float handTeleportDuration = 0.5f;
    [SerializeField] private float handPreCastDelay = 0.5f;
    [SerializeField] private float handSwingDuration = 0.5f;

    [Header("Overlay / Transition")]
    [SerializeField] private FinalBossDiamondFlickerOverlay diamondFlickerOverlay;
    [SerializeField] private FinalBossSceneTransitionController hiddenSceneTransitionController;

    [Header("Loop Delay")]
    [SerializeField] private float phase1LoopDelay = 0f;
    [SerializeField] private float phase2LoopDelay = 0f;

    private static readonly Vector2[] EightDirections =
    {
        Vector2.up,
        (Vector2.up + Vector2.right).normalized,
        Vector2.right,
        (Vector2.down + Vector2.right).normalized,
        Vector2.down,
        (Vector2.down + Vector2.left).normalized,
        Vector2.left,
        (Vector2.up + Vector2.left).normalized
    };

    private Transform playerTransform;
    private CombatPhase currentPhase = CombatPhase.Phase1;
    private Coroutine battleRoutine;
    private Coroutine deathRoutine;
    private Coroutine patternRoutineA;
    private Coroutine patternRoutineB;
    private bool patternRoutineACompleted;
    private bool patternRoutineBCompleted;
    private bool isBattleRunning;
    private bool isPhase2Queued;
    private bool isVictoryHandled;
    private Vector2 facingDirection = Vector2.right;
    private Vector3 initialBossPosition;
    private CarmaExcisionTrueHitbox activeCarmaHitbox;
    private readonly List<GameObject> spawnedHazards = new();
    private readonly List<Transform> latentThornSpawnPoints = new();
    private readonly List<Transform> bedimmedWallSlots = new();
    private readonly List<LatentThornHitbox> latentThornHitboxes = new();
    private readonly List<PlayableDirector> latentThornTimelines = new();

    public event Action OnBattleReset;

    protected override bool UseCollisionInvulnerability => false;

    private void Awake()
    {
        initialBossPosition = transform.position;

        if (bossHealth == null) bossHealth = GetComponent<BossHealth>();
        if (bossAnimator == null) bossAnimator = GetComponentInChildren<Animator>();
        ResolveGroundTilemap();
        ResolvePlayerTransform();
        CacheLatentThornSpawnPoints();
        BuildLatentThornHitboxes();

        if (bossHealth != null)
        {
            bossHealth.maxHP = maxHp;
            bossHealth.currentHP = maxHp;
            bossHealth.currentElement = ElementType.None;
            bossHealth.bossName = "???";
            bossHealth.SetInvulnerable(true);
        }

        SetPreplacedLayoutObjectsActive(false);
    }

    private void OnEnable()
    {
        PlayerHealth.OnPlayerDeath += HandlePlayerDeath;
        ResolvePlayerTransform();
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDeath -= HandlePlayerDeath;
        StopBattleRoutines();
        ClearOffensives();
    }

    public override void StartBattle()
    {
        if (isBattleRunning) return;
        if (!ResolvePlayerTransform()) return;

        isBattleRunning = true;
        isVictoryHandled = false;
        isPhase2Queued = false;
        currentPhase = CombatPhase.Phase1;

        if (bossHealth != null)
        {
            bossHealth.maxHP = maxHp;
            bossHealth.currentHP = maxHp;
            bossHealth.currentElement = ElementType.None;
            bossHealth.SetInvulnerable(false);
        }

        battleRoutine = StartCoroutine(BattleLoopRoutine());
    }

    public void PrepareIdleState(Transform overridePoint = null)
    {
        StopBattleRoutines();
        ClearOffensives();
        StopAttackAnimation();

        isBattleRunning = false;
        isVictoryHandled = false;
        isPhase2Queued = false;
        currentPhase = CombatPhase.Phase1;

        if (bossHealth != null)
        {
            bossHealth.maxHP = maxHp;
            bossHealth.currentHP = maxHp;
            bossHealth.currentElement = ElementType.None;
            bossHealth.SetInvulnerable(true);
        }

        Vector3 target = overridePoint != null
            ? overridePoint.position
            : ResolveBossStartPosition();
        transform.position = ClampToRoom(target);
        SetPreplacedLayoutObjectsActive(false);
    }

    public void SetToPointAImmediate()
    {
        transform.position = ClampToRoom(ResolveBossStartPosition());
    }

    public void OnBeamHit(Player player, Transform beamTransform)
    {
        if (player == null) return;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider == null) return;

        Vector2 attackerPos = beamTransform != null ? (Vector2)beamTransform.position : (Vector2)transform.position;
        BossHitResolver.TryApplyBossHit(playerCollider, damagePerHit, attackerPos);
    }

    public float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier)
    {
        // ??? has no elemental weakness/resistance: every element deals exactly 1x.
        return 1f;
    }

    public void OnBossHpChanged(int currentHp, int maxHpValue)
    {
        if (!isBattleRunning || isVictoryHandled) return;

        if (currentHp <= 0)
        {
            isVictoryHandled = true;
            if (bossHealth != null)
            {
                // Prevent BossHealth.Die() from destroying this object before scene transition.
                bossHealth.currentHP = 0;
                bossHealth.SetInvulnerable(true);
            }

            StartCoroutine(HandleVictoryRoutine());
            return;
        }

        if (currentHp < phase2HpThreshold && currentPhase == CombatPhase.Phase1)
        {
            isPhase2Queued = true;
        }
    }

    private IEnumerator BattleLoopRoutine()
    {
        // Battle opens immediately with Latent Thorn.
        while (isBattleRunning && !isVictoryHandled)
        {
            if (currentPhase == CombatPhase.Phase1)
            {
                yield return RunPatternsInParallel(
                    PatternLatentThorn(),
                    PatternCarmaExcision()
                );
                if (isVictoryHandled || !isBattleRunning) yield break;

                if (isPhase2Queued)
                {
                    currentPhase = CombatPhase.Phase2;
                    yield return PatternBedimmedWallUltimate();
                    if (isVictoryHandled || !isBattleRunning) yield break;
                }

                if (phase1LoopDelay > 0f) yield return new WaitForSeconds(phase1LoopDelay);
                continue;
            }

            yield return RunPatternsInParallel(
                PatternHandOfTime(),
                PatternCarmaExcision()
            );
            if (isVictoryHandled || !isBattleRunning) yield break;

            if (phase2LoopDelay > 0f) yield return new WaitForSeconds(phase2LoopDelay);
        }
    }

    private IEnumerator RunPatternsInParallel(IEnumerator routineA, IEnumerator routineB)
    {
        patternRoutineACompleted = false;
        patternRoutineBCompleted = false;

        patternRoutineA = StartCoroutine(TrackRoutineCompletion(routineA, () => patternRoutineACompleted = true));
        patternRoutineB = StartCoroutine(TrackRoutineCompletion(routineB, () => patternRoutineBCompleted = true));

        while (isBattleRunning && !isVictoryHandled && (!patternRoutineACompleted || !patternRoutineBCompleted))
        {
            yield return null;
        }

        StopParallelPatternRoutines();
    }

    private IEnumerator TrackRoutineCompletion(IEnumerator routine, Action onComplete)
    {
        if (routine != null)
        {
            yield return routine;
        }

        onComplete?.Invoke();
    }

    private IEnumerator PatternLatentThorn()
    {
        yield return MoveToStart(defaultTeleportDuration);

        FacePlayerHorizontal();
        yield return AttackSCast(latentSwordSwingDuration, true);
        PlayLatentThornTimelines();
        yield return HandleLatentThorn();
    }

    private IEnumerator HandleLatentThorn()
    {
        if (latentWarningDuration > 0f)
        {
            yield return new WaitForSeconds(latentWarningDuration);
        }

        EnsureLatentThornSetup();

        float activeDuration = Mathf.Max(0f, latentRiseDuration + latentHoldDuration + latentDespawnDuration);
        foreach (LatentThornHitbox thorn in latentThornHitboxes)
        {
            if (thorn != null)
            {
                thorn.ActivateForSeconds(activeDuration);
            }
        }

        if (activeDuration > 0f)
        {
            yield return new WaitForSeconds(activeDuration);
        }

        StopLatentThornTimelines();
    }

    private IEnumerator PatternCarmaExcision()
    {
        float openingDelay = Mathf.Max(0f, carmaOpeningSwingDuration + carmaInitialDelay);
        if (openingDelay > 0f) yield return new WaitForSeconds(openingDelay);

        if (!ResolvePlayerTransform()) yield break;

        if (carmaTeleportDelay1 > 0f) yield return new WaitForSeconds(carmaTeleportDelay1);
        if (!TryGetPlayerSideWorldPosition(-1, out Vector3 leftCell)) yield break;
        transform.position = leftCell;

        if (carmaTeleportDelay2 > 0f) yield return new WaitForSeconds(carmaTeleportDelay2);
        if (!TryGetPlayerSideWorldPosition(1, out Vector3 rightCell)) yield break;
        transform.position = rightCell;

        if (carmaTeleportDelay3 > 0f) yield return new WaitForSeconds(carmaTeleportDelay3);
        if (!TryGetPlayerSideWorldPosition(-1, out leftCell)) yield break;
        transform.position = leftCell;

        // Fake attack: Attack_S is intentionally empty and canceled at 0.4s.
        if (carmaFakeAttackDuration > 0f) yield return new WaitForSeconds(carmaFakeAttackDuration);
        if (!TryGetPlayerSideWorldPosition(1, out rightCell)) yield break;
        transform.position = rightCell;
        if (carmaFinalTeleportDuration > 0f) yield return new WaitForSeconds(carmaFinalTeleportDuration);

        float cellWidth = ResolveGroundCellWidth();
        Vector2 trueHitCenter = ClampToRoom(transform.position + Vector3.left * cellWidth);
        ActivateCarmaTrueHitbox(trueHitCenter, carmaTrueHitboxDuration);
        if (carmaTrueHitboxDuration > 0f) yield return new WaitForSeconds(carmaTrueHitboxDuration);

        float endDelay = carmaPhase1EndDelay > 0f ? carmaPhase1EndDelay : carmaPhase2EndDelay;
        if (endDelay > 0f) yield return new WaitForSeconds(endDelay);
    }

    private IEnumerator PatternBedimmedWallUltimate()
    {
        yield return MoveToStart(bedimmedTeleportDuration);

        FacePlayerHorizontal();
        yield return AttackSCast(bedimmedSwingDuration, true);

        diamondFlickerOverlay = EnsureDiamondFlickerOverlay();
        if (diamondFlickerOverlay != null)
        {
            diamondFlickerOverlay.BeginLoop();
        }

        List<FinalBossBedimmedWallProjectile> projectiles = new();
        List<Vector2> launchDirections = new();

        IReadOnlyList<Transform> slots = ResolveBedimmedWallSlots();
        bool hasConfiguredSlots = slots.Count > 0;
        if (hasConfiguredSlots)
        {
            foreach (Transform slot in slots)
            {
                if (slot == null) continue;

                Vector2 spawnPos = ClampToRoom(slot.position);
                TryDamagePlayerInBox(spawnPos, oneTileHitbox, damagePerHit, spawnPos);

                FinalBossBedimmedWallProjectile projectile = CreateWallProjectile(spawnPos);
                projectiles.Add(projectile);
                // "Front" in this fight is local right for placed BedimmedWall prefabs.
                Vector2 fireDir = ((Vector2)slot.right).sqrMagnitude < 0.001f
                    ? ((Vector2)slot.up).normalized
                    : ((Vector2)slot.right).normalized;
                if (fireDir.sqrMagnitude < 0.001f)
                {
                    fireDir = Vector2.right;
                }

                launchDirections.Add(fireDir);
                spawnedHazards.Add(projectile.gameObject);
            }
        }
        else
        {
            foreach (Vector2 dir in EightDirections)
            {
                Vector2 spawnPos = ClampToRoom((Vector2)transform.position + dir * bedimmedSpawnRadius);
                TryDamagePlayerInBox(spawnPos, oneTileHitbox, damagePerHit, spawnPos);

                FinalBossBedimmedWallProjectile projectile = CreateWallProjectile(spawnPos);
                projectiles.Add(projectile);
                launchDirections.Add(dir);
                spawnedHazards.Add(projectile.gameObject);
            }
        }

        if (bedimmedSpawnDuration > 0f)
        {
            yield return new WaitForSeconds(bedimmedSpawnDuration);
        }

        for (int i = 0; i < projectiles.Count; i++)
        {
            FinalBossBedimmedWallProjectile projectile = projectiles[i];
            if (projectile == null) continue;
            Vector2 fireDir = i < launchDirections.Count ? launchDirections[i] : Vector2.up;
            projectile.Launch(fireDir, bedimmedProjectileSpeed, oneTileHitbox, damagePerHit, ElementType.Electric);
        }

        if (bedimmedPostDelay > 0f) yield return new WaitForSeconds(bedimmedPostDelay);
    }

    private IReadOnlyList<Transform> ResolveBedimmedWallSlots()
    {
        bedimmedWallSlots.Clear();

        if (bedimmedWallsParent != null)
        {
            foreach (Transform child in bedimmedWallsParent)
            {
                if (child != null)
                {
                    bedimmedWallSlots.Add(child);
                }
            }
        }

        if (bedimmedWallSlots.Count > 0)
        {
            return bedimmedWallSlots;
        }

        if (bedimmedWallSpawnPoints != null)
        {
            for (int i = 0; i < bedimmedWallSpawnPoints.Length; i++)
            {
                Transform point = bedimmedWallSpawnPoints[i];
                if (point != null)
                {
                    bedimmedWallSlots.Add(point);
                }
            }
        }

        return bedimmedWallSlots;
    }

    private IEnumerator PatternHandOfTime()
    {
        yield return MoveToStart(handTeleportDuration);
        if (handPreCastDelay > 0f) yield return new WaitForSeconds(handPreCastDelay);

        FacePlayerHorizontal();
        yield return AttackSCast(handSwingDuration, true);

        FinalBossHandOfTimeBurstController controller = EnsureHandOfTimeController();
        if (controller == null) yield break;

        int previousHazardCount = spawnedHazards.Count;
        yield return controller.ExecutePattern(playerTransform, damagePerHit, spawnedHazards);

        for (int i = previousHazardCount; i < spawnedHazards.Count; i++)
        {
            RegisterBossOffensive(spawnedHazards[i]);
        }
    }

    private IEnumerator MoveToStart(float duration)
    {
        yield return TeleportSmooth(ResolveBossStartPosition(), duration);
    }

    private IEnumerator AttackSCast(float duration, bool applyDamage)
    {
        float safeDuration = Mathf.Max(0f, duration);
        float hitMoment = Mathf.Clamp(safeDuration * 0.5f, 0f, safeDuration);

        if (hitMoment > 0f) yield return new WaitForSeconds(hitMoment);

        if (applyDamage)
        {
            Vector2 forward = facingDirection.sqrMagnitude < 0.001f ? Vector2.right : facingDirection.normalized;
            Vector2 center = (Vector2)transform.position + forward;
            TryDamagePlayerInBox(center, oneTileHitbox, damagePerHit, transform.position);
        }

        float remain = safeDuration - hitMoment;
        if (remain > 0f) yield return new WaitForSeconds(remain);
        StopAttackAnimation();
    }

    private void StopAttackAnimation()
    {
        // Attack_S is intentionally empty for this boss.
    }

    private void ActivateCarmaTrueHitbox(Vector2 hitCenter, float activeDuration)
    {
        activeCarmaHitbox = EnsureCarmaHitbox();
        if (activeCarmaHitbox == null) return;

        activeCarmaHitbox.Activate(
            hitCenter,
            oneTileHitbox,
            activeDuration,
            transform.position
        );
    }

    private CarmaExcisionTrueHitbox EnsureCarmaHitbox()
    {
        if (activeCarmaHitbox != null) return activeCarmaHitbox;

        if (carmaTrueHitboxPrefab != null)
        {
            Transform parent = transform.root != null ? transform.root : null;
            activeCarmaHitbox = parent != null
                ? Instantiate(carmaTrueHitboxPrefab, transform.position, Quaternion.identity, parent)
                : Instantiate(carmaTrueHitboxPrefab, transform.position, Quaternion.identity);
            RegisterBossOffensive(activeCarmaHitbox.gameObject);
            return activeCarmaHitbox;
        }

        GameObject go = new GameObject("CarmaExcisionTrueHitbox");
        go.AddComponent<BoxCollider2D>();
        activeCarmaHitbox = go.AddComponent<CarmaExcisionTrueHitbox>();
        RegisterBossOffensive(activeCarmaHitbox.gameObject);
        return activeCarmaHitbox;
    }

    private void CacheLatentThornSpawnPoints()
    {
        latentThornSpawnPoints.Clear();

        if (latentThornPositionsParent == null) return;

        foreach (Transform child in latentThornPositionsParent)
        {
            if (child != null)
            {
                latentThornSpawnPoints.Add(child);
            }
        }
    }

    private void BuildLatentThornHitboxes()
    {
        foreach (LatentThornHitbox thorn in latentThornHitboxes)
        {
            if (thorn != null)
            {
                UnregisterBossOffensive(thorn.gameObject);
                Destroy(thorn.gameObject);
            }
        }

        latentThornHitboxes.Clear();

        if (latentThornHitboxPrefab == null) return;

        Transform parent = latentThornPositionsParent != null ? latentThornPositionsParent : transform;
        foreach (Transform spawnPoint in latentThornSpawnPoints)
        {
            if (spawnPoint == null) continue;

            LatentThornHitbox thorn = Instantiate(latentThornHitboxPrefab, spawnPoint.position, Quaternion.identity, parent);
            thorn.ResetState();
            RegisterBossOffensive(thorn.gameObject);
            latentThornHitboxes.Add(thorn);
        }
    }

    private void EnsureLatentThornSetup()
    {
        if (latentThornSpawnPoints.Count == 0)
        {
            CacheLatentThornSpawnPoints();
        }

        if (latentThornHitboxes.Count != latentThornSpawnPoints.Count)
        {
            BuildLatentThornHitboxes();
        }

        EnsureLatentThornTimelines();

        for (int i = 0; i < latentThornHitboxes.Count; i++)
        {
            if (latentThornHitboxes[i] == null || i >= latentThornSpawnPoints.Count || latentThornSpawnPoints[i] == null)
            {
                continue;
            }

            latentThornHitboxes[i].transform.position = latentThornSpawnPoints[i].position;
            latentThornHitboxes[i].gameObject.SetActive(true);
        }

        for (int i = 0; i < latentThornTimelines.Count; i++)
        {
            if (latentThornTimelines[i] == null || i >= latentThornSpawnPoints.Count || latentThornSpawnPoints[i] == null)
            {
                continue;
            }

            latentThornTimelines[i].transform.position = latentThornSpawnPoints[i].position;
        }
    }

    private void EnsureLatentThornTimelines()
    {
        if (latentThornSpawnPoints.Count == 0)
        {
            return;
        }

        GameObject sourcePrefab = null;
        if (latentThornTimelinePrefab != null)
        {
            sourcePrefab = latentThornTimelinePrefab;
        }
        else if (latentThornTimeline != null)
        {
            sourcePrefab = latentThornTimeline.gameObject;
        }

        if (sourcePrefab == null)
        {
            return;
        }

        while (latentThornTimelines.Count > latentThornSpawnPoints.Count)
        {
            int last = latentThornTimelines.Count - 1;
            if (latentThornTimelines[last] != null)
            {
                UnregisterBossOffensive(latentThornTimelines[last].gameObject);
                Destroy(latentThornTimelines[last].gameObject);
            }

            latentThornTimelines.RemoveAt(last);
        }

        while (latentThornTimelines.Count < latentThornSpawnPoints.Count)
        {
            int index = latentThornTimelines.Count;
            Transform spawnPoint = latentThornSpawnPoints[index];
            if (spawnPoint == null)
            {
                latentThornTimelines.Add(null);
                continue;
            }

            Transform parent = latentThornPositionsParent != null
                ? latentThornPositionsParent.parent
                : latentThornTimelineParent;
            GameObject instance = parent != null
                ? Instantiate(sourcePrefab, spawnPoint.position, Quaternion.identity, parent)
                : Instantiate(sourcePrefab, spawnPoint.position, Quaternion.identity);

            PlayableDirector director = instance.GetComponent<PlayableDirector>();
            if (director == null)
            {
                Debug.LogWarning("FinalBossCombat: LatentThorn timeline prefab has no PlayableDirector.", instance);
                Destroy(instance);
                latentThornTimelines.Add(null);
                continue;
            }

            director.playOnAwake = false;
            director.Stop();
            director.time = 0;
            BindLatentThornTimelineReferences(director);
            RegisterBossOffensive(instance, true);
            latentThornTimelines.Add(director);
        }

        latentThornTimeline = latentThornTimelines.Count > 0 ? latentThornTimelines[0] : null;
    }

    private void PlayLatentThornTimelines()
    {
        EnsureLatentThornTimelines();

        foreach (PlayableDirector director in latentThornTimelines)
        {
            if (director == null) continue;
            director.Stop();
            director.time = 0;
            director.Play();
        }
    }

    private void StopLatentThornTimelines()
    {
        foreach (PlayableDirector director in latentThornTimelines)
        {
            if (director == null) continue;
            director.Stop();
            director.time = 0;
        }
    }

    private void BindLatentThornTimelineReferences(PlayableDirector director)
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

    private FinalBossBedimmedWallProjectile CreateWallProjectile(Vector2 worldPosition)
    {
        if (bedimmedWallProjectilePrefab != null)
        {
            FinalBossBedimmedWallProjectile projectile = Instantiate(bedimmedWallProjectilePrefab, worldPosition, Quaternion.identity);
            RegisterBossOffensive(projectile.gameObject);
            return projectile;
        }

        GameObject go = new GameObject("FinalBossBedimmedWallProjectile");
        go.transform.position = worldPosition;
        go.AddComponent<BoxCollider2D>();
        FinalBossBedimmedWallProjectile fallbackProjectile = go.AddComponent<FinalBossBedimmedWallProjectile>();
        RegisterBossOffensive(fallbackProjectile.gameObject);
        return fallbackProjectile;
    }

    private FinalBossHandOfTimeBurstController EnsureHandOfTimeController()
    {
        if (handOfTimeController != null) return handOfTimeController;

        GameObject go = new GameObject("FinalBossHandOfTimeBurstController");
        go.transform.SetParent(transform, false);
        handOfTimeController = go.AddComponent<FinalBossHandOfTimeBurstController>();
        return handOfTimeController;
    }

    private FinalBossDiamondFlickerOverlay EnsureDiamondFlickerOverlay()
    {
        if (diamondFlickerOverlay != null) return diamondFlickerOverlay;

        GameObject go = new GameObject("FinalBossDiamondFlickerOverlay");
        diamondFlickerOverlay = go.AddComponent<FinalBossDiamondFlickerOverlay>();
        return diamondFlickerOverlay;
    }

    private bool TryDamagePlayerInBox(Vector2 center, Vector2 size, int damage, Vector2 attackerPosition)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || !hit.CompareTag("Player")) continue;
            return BossHitResolver.TryApplyBossHit(hit, damage, attackerPosition);
        }

        return false;
    }

    private IEnumerator TeleportSmooth(Vector3 rawTarget, float duration)
    {
        Vector3 target = ClampToRoom(rawTarget);
        if (duration <= 0f)
        {
            transform.position = target;
            yield break;
        }

        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.position = target;
    }

    private void FacePlayerHorizontal()
    {
        if (!ResolvePlayerTransform()) return;
        facingDirection = playerTransform.position.x >= transform.position.x ? Vector2.right : Vector2.left;
    }

    private bool ResolvePlayerTransform()
    {
        return TryResolvePlayerTransform(ref playerTransform);
    }

    private void ResolveGroundTilemap()
    {
        if (groundTilemap != null) return;

        Transform root = transform.root;
        if (root == null) return;

        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null) continue;
            if (tilemap.CompareTag("Ground"))
            {
                groundTilemap = tilemap;
                return;
            }
        }

        if (tilemaps.Length > 0)
        {
            groundTilemap = tilemaps[0];
        }
    }

    private Bounds ResolveRoomBounds()
    {
        if (roomBoundsOverride != null)
        {
            return roomBoundsOverride.bounds;
        }

        ResolveGroundTilemap();
        if (groundTilemap != null)
        {
            BoundsInt cellBounds = groundTilemap.cellBounds;
            if (cellBounds.size.x > 0 && cellBounds.size.y > 0)
            {
                Vector3 minCenter = groundTilemap.GetCellCenterWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
                Vector3 maxCenter = groundTilemap.GetCellCenterWorld(new Vector3Int(cellBounds.xMax - 1, cellBounds.yMax - 1, 0));
                Vector3 cellHalf = new Vector3(groundTilemap.cellSize.x * 0.5f, groundTilemap.cellSize.y * 0.5f, 0f);
                Vector3 min = minCenter - cellHalf;
                Vector3 max = maxCenter + cellHalf;

                Bounds result = new Bounds();
                result.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
                return result;
            }
        }

        if (RoomManager.Instance != null)
        {
            Vector3 center = transform.root != null ? transform.root.position : transform.position;
            float width = Mathf.Max(1f, RoomManager.Instance.playableWidth);
            float height = Mathf.Max(1f, RoomManager.Instance.playableHeight);
            return new Bounds(center, new Vector3(width, height, 1f));
        }

        return new Bounds(transform.position, new Vector3(28f, 18f, 1f));
    }

    private Vector3 ResolveBossStartPosition()
    {
        if (startPoint != null)
        {
            return startPoint.position;
        }

        return initialBossPosition;
    }

    // Teleport rule: unwalkable tile is allowed, but room-outside position is forbidden.
    private Vector3 ClampToRoom(Vector3 position)
    {
        Bounds roomBounds = ResolveRoomBounds();
        position.x = Mathf.Clamp(position.x, roomBounds.min.x + teleportInset, roomBounds.max.x - teleportInset);
        position.y = Mathf.Clamp(position.y, roomBounds.min.y + teleportInset, roomBounds.max.y - teleportInset);
        position.z = 0f;
        return position;
    }

    private float ResolveGroundCellWidth()
    {
        ResolveGroundTilemap();
        if (groundTilemap != null)
        {
            return Mathf.Max(0.01f, Mathf.Abs(groundTilemap.cellSize.x));
        }

        return 1f;
    }

    private bool TryGetPlayerSideWorldPosition(int horizontalSign, out Vector3 result)
    {
        result = default;
        if (!ResolvePlayerTransform()) return false;

        float sign = horizontalSign < 0 ? -1f : 1f;
        float cellWidth = ResolveGroundCellWidth();
        Vector3 target = playerTransform.position + Vector3.right * (cellWidth * sign);
        result = ClampToRoom(target);
        return true;
    }

    private void HandlePlayerDeath()
    {
        if (!isBattleRunning || isVictoryHandled) return;
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
            if (health != null) health.Resurrect();

            Transform respawnPoint = ResolveRoomRespawnPoint();
            if (respawnPoint == null)
            {
                respawnPoint = ResolveStairRespawnPoint();
            }

            if (respawnPoint != null)
            {
                player.position = respawnPoint.position;
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

    private Transform ResolveStairRespawnPoint()
    {
        if (stairRespawnPoint != null) return stairRespawnPoint;

        Transform root = transform.root;
        if (root == null) return null;

        Stack<Transform> stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            if (current == null) continue;

            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("stair"))
            {
                stairRespawnPoint = current;
                return stairRespawnPoint;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                stack.Push(current.GetChild(i));
            }
        }

        return null;
    }

    private Transform ResolveRoomRespawnPoint()
    {
        if (RoomManager.Instance != null)
        {
            Transform fromManager = RoomManager.Instance.GetSpawnPointForCurrentRoom("PlayerSpawnPoint");
            if (fromManager != null) return fromManager;
        }

        Transform root = transform.root;
        if (root == null) return null;

        Stack<Transform> stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            if (current == null) continue;
            if (current.name == "PlayerSpawnPoint") return current;

            for (int i = 0; i < current.childCount; i++)
            {
                stack.Push(current.GetChild(i));
            }
        }

        return null;
    }

    private IEnumerator HandleVictoryRoutine()
    {
        isBattleRunning = false;
        StopBattleRoutines();
        ClearOffensives();
        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }

        hiddenSceneTransitionController = EnsureSceneTransitionController();
        if (hiddenSceneTransitionController != null)
        {
            yield return hiddenSceneTransitionController.TransitionToHiddenScene(hiddenSceneName);
        }
    }

    private FinalBossSceneTransitionController EnsureSceneTransitionController()
    {
        if (hiddenSceneTransitionController != null) return hiddenSceneTransitionController;

        hiddenSceneTransitionController = FindFirstObjectByType<FinalBossSceneTransitionController>();
        if (hiddenSceneTransitionController != null) return hiddenSceneTransitionController;

        GameObject go = new GameObject("FinalBossSceneTransitionController");
        hiddenSceneTransitionController = go.AddComponent<FinalBossSceneTransitionController>();
        return hiddenSceneTransitionController;
    }

    private void ResetBattleForRetry()
    {
        PrepareIdleState(startPoint);
        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }
        OnBattleReset?.Invoke();
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;

        Stack<Transform> stack = new Stack<Transform>();
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

    private void StopBattleRoutines()
    {
        if (battleRoutine != null)
        {
            StopCoroutine(battleRoutine);
            battleRoutine = null;
        }

        StopParallelPatternRoutines();
    }

    private void StopParallelPatternRoutines()
    {
        if (patternRoutineA != null)
        {
            StopCoroutine(patternRoutineA);
            patternRoutineA = null;
        }

        if (patternRoutineB != null)
        {
            StopCoroutine(patternRoutineB);
            patternRoutineB = null;
        }
    }

    private void ClearOffensives()
    {
        ClearHazards();
        if (diamondFlickerOverlay != null)
        {
            diamondFlickerOverlay.StopLoop();
        }
        else
        {
            diamondFlickerOverlay = null;
        }
        StopLatentThornTimelines();

        if (activeCarmaHitbox != null)
        {
            activeCarmaHitbox.DeactivateImmediate();
        }

        foreach (LatentThornHitbox thorn in latentThornHitboxes)
        {
            if (thorn != null)
            {
                thorn.ResetState();
            }
        }

        SetPreplacedLayoutObjectsActive(false);
        CleanupBossOffensives(BossOffensiveCleanupReason.BattleReset);
    }

    private void SetPreplacedLayoutObjectsActive(bool active)
    {
        if (bedimmedWallsParent != null)
        {
            foreach (Transform child in bedimmedWallsParent)
            {
                if (child != null && child.gameObject.activeSelf != active)
                {
                    child.gameObject.SetActive(active);
                }
            }
        }

        if (handOfTimeController != null)
        {
            handOfTimeController.SetLayoutObjectsActive(active);
        }
    }

    private void ClearHazards()
    {
        foreach (GameObject hazard in spawnedHazards)
        {
            if (hazard != null)
            {
                UnregisterBossOffensive(hazard);
                Destroy(hazard);
            }
        }

        spawnedHazards.Clear();
    }
}
