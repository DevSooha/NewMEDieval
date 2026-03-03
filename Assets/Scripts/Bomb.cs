using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    private const float PatternWindowSeconds = 8f;
    private const float SecondPhaseDelaySeconds = 2f;
    private const float AfterimageHazardDurationSeconds = 2f;
    private const float AfterimageHazardSizeUnits = 2f; // 64px with 32px = 1u

    [Header("Bomb Settings")]
    public ElementType bombElement = ElementType.Water;
    public int baseDamage = 200;
    public float timeToExplode = 2.0f;
    public float explosionRadius = 1.5f;
    public GameObject explosionEffect;
    [SerializeField] private float projectileLifetime = 2.5f;
    [SerializeField] private float projectileSpawnOffset = 0.1f;
    [SerializeField] private bool spawnProjectilePatterns = true;

    [Header("Projectile VFX")]
    [SerializeField] private GameObject waterProjectileVfxPrefab;
    [SerializeField] private GameObject fireProjectileVfxPrefab;
    [SerializeField] private GameObject electricProjectileVfxPrefab;

    private PotionPhaseSpec phase1Spec;
    private PotionPhaseSpec phase2Spec;
    private bool hasPotionSpecs;
    private bool exploded;

    private float activeSequenceEndTime;
    private bool patternSequenceRunning;
    private ProjectilePatternType activePatternType = ProjectilePatternType.Fireworks;

    private readonly List<PotionProjectileController> activeProjectiles = new List<PotionProjectileController>();
    private readonly List<PotionAreaHazard> activeHazards = new List<PotionAreaHazard>();
    private readonly List<Transform> runtimeGroups = new List<Transform>();
    private readonly Dictionary<Transform, Coroutine> groupRotationRoutines = new Dictionary<Transform, Coroutine>();

    private void Start()
    {
        StartCoroutine(ExplodeSequence());
    }

    private void OnDisable()
    {
        ForceImmediateCleanup();
    }

    public void ConfigureFromPotionData(PotionData potionData)
    {
        if (potionData == null) return;

        phase1Spec = potionData.GetPhase(0);
        phase2Spec = potionData.GetPhase(1);
        hasPotionSpecs = phase1Spec != null || phase2Spec != null;

        if (!hasPotionSpecs)
        {
            return;
        }

        int combinedDamage = 0;
        if (phase1Spec != null) combinedDamage += Mathf.Max(0, phase1Spec.baseDamage);
        if (phase2Spec != null) combinedDamage += Mathf.Max(0, phase2Spec.baseDamage);

        if (combinedDamage > 0)
        {
            baseDamage = combinedDamage;
        }

        PotionPhaseSpec leadSpec = phase1Spec != null ? phase1Spec : phase2Spec;
        if (leadSpec != null)
        {
            bombElement = leadSpec.primaryElement;
        }
    }

    private IEnumerator ExplodeSequence()
    {
        yield return new WaitForSeconds(timeToExplode);
        Explode();
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        HashSet<EnemyCombat> hitEnemies = new HashSet<EnemyCombat>();
        HashSet<BossHealth> hitBosses = new HashSet<BossHealth>();
        HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

        foreach (Collider2D hit in hits)
        {
            EnemyCombat enemy = hit.GetComponent<EnemyCombat>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyCombat>();
            if (enemy != null && hitEnemies.Add(enemy))
            {
                if (hasPotionSpecs)
                {
                    ApplyPotionSpecsToEnemy(enemy);
                }
                else
                {
                    enemy.EnemyTakeDamage(baseDamage);
                }
            }

            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss == null) boss = hit.GetComponentInParent<BossHealth>();
            if (boss != null && hitBosses.Add(boss))
            {
                if (hasPotionSpecs)
                {
                    ApplyPotionSpecsToBoss(boss);
                }
                else
                {
                    boss.TakeDamage(baseDamage, bombElement);
                }
            }

            PlayerHealth player = hit.GetComponent<PlayerHealth>();
            if (player == null) player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null && hitPlayers.Add(player) && hasPotionSpecs)
            {
                ApplyPotionSpecsToPlayer(player);
            }

            if (hit.CompareTag("Grass"))
            {
                Destroy(hit.gameObject);
            }
        }

        if (hasPotionSpecs && spawnProjectilePatterns)
        {
            StartCoroutine(SpawnPotionProjectilePatternsAndDestroy());
            return;
        }

        Destroy(gameObject);
    }

    private void ApplyPotionSpecsToEnemy(EnemyCombat enemy)
    {
        if (enemy == null) return;

        if (phase1Spec != null)
        {
            PotionHitResolver.ApplySpecToEnemy(phase1Spec, enemy);
        }

        if (phase2Spec != null)
        {
            PotionHitResolver.ApplySpecToEnemy(phase2Spec, enemy);
        }
    }

    private void ApplyPotionSpecsToBoss(BossHealth boss)
    {
        if (boss == null) return;

        if (phase1Spec != null)
        {
            PotionHitResolver.ApplySpecToBoss(phase1Spec, boss);
        }

        if (phase2Spec != null)
        {
            PotionHitResolver.ApplySpecToBoss(phase2Spec, boss);
        }
    }

    private void ApplyPotionSpecsToPlayer(PlayerHealth player)
    {
        if (player == null) return;

        if (phase1Spec != null)
        {
            PotionHitResolver.ApplySpecToPlayer(phase1Spec, player);
        }

        if (phase2Spec != null)
        {
            PotionHitResolver.ApplySpecToPlayer(phase2Spec, player);
        }
    }

    private IEnumerator SpawnPotionProjectilePatternsAndDestroy()
    {
        activeSequenceEndTime = Time.time + PatternWindowSeconds;
        patternSequenceRunning = true;
        activePatternType = ResolveSharedPatternType();

        if (phase1Spec != null)
        {
            StartCoroutine(ExecutePhaseWithDelay(phase1Spec, true, 0f, activePatternType, activeSequenceEndTime));
        }

        if (phase2Spec != null)
        {
            StartCoroutine(ExecutePhaseWithDelay(phase2Spec, false, SecondPhaseDelaySeconds, activePatternType, activeSequenceEndTime));
        }

        Collider2D ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
        {
            ownCollider.enabled = false;
        }

        SpriteRenderer ownRenderer = GetComponent<SpriteRenderer>();
        if (ownRenderer != null)
        {
            ownRenderer.enabled = false;
        }

        while (Time.time < activeSequenceEndTime)
        {
            yield return null;
        }

        if (activePatternType == ProjectilePatternType.AfterimageBomb)
        {
            ConvertAfterimageProjectilesToHazards(true, 1);
            ConvertAfterimageProjectilesToHazards(false, 2);
        }

        CleanupRuntimeObjects(includeHazards: false);

        yield return WaitForHazardsToExpire(AfterimageHazardDurationSeconds + 0.2f);

        patternSequenceRunning = false;
        Destroy(gameObject);
    }

    private IEnumerator ExecutePhaseWithDelay(PotionPhaseSpec phase, bool isFirstPhase, float delay, ProjectilePatternType forcedPatternType, float sequenceEndTime)
    {
        if (phase == null)
        {
            yield break;
        }

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (Time.time >= sequenceEndTime || !patternSequenceRunning)
        {
            yield break;
        }

        Transform group = null;
        Action<bool, float> rotationSetter = null;

        if (forcedPatternType == ProjectilePatternType.Tornado)
        {
            group = CreateRuntimeGroup(isFirstPhase ? "TornadoGroup_Phase1" : "TornadoGroup_Phase2");
            rotationSetter = (active, speed) => SetGroupRotation(group, active, speed);
        }

        yield return ProjectilePatternExecutor.ExecutePhase(
            phase,
            isFirstPhase,
            transform,
            request => SpawnProjectileFromPhase(phase, request),
            rotationSetter,
            group,
            forcedPatternType,
            sequenceEndTime);

        if (group != null)
        {
            SetGroupRotation(group, false, 0f);
        }
    }

    private ProjectilePatternType ResolveSharedPatternType()
    {
        if (phase1Spec != null)
        {
            return phase1Spec.patternType;
        }

        if (phase2Spec != null)
        {
            return phase2Spec.patternType;
        }

        return ProjectilePatternType.Fireworks;
    }

    private Transform CreateRuntimeGroup(string groupName)
    {
        GameObject groupObj = new GameObject(groupName);
        groupObj.transform.SetParent(transform, false);
        groupObj.transform.localPosition = Vector3.zero;
        groupObj.transform.localRotation = Quaternion.identity;
        runtimeGroups.Add(groupObj.transform);
        return groupObj.transform;
    }

    private void SetGroupRotation(Transform group, bool active, float speedDegPerSec)
    {
        if (group == null)
        {
            return;
        }

        if (!active)
        {
            if (groupRotationRoutines.TryGetValue(group, out Coroutine running))
            {
                if (running != null)
                {
                    StopCoroutine(running);
                }

                groupRotationRoutines.Remove(group);
            }

            return;
        }

        if (groupRotationRoutines.ContainsKey(group))
        {
            return;
        }

        Coroutine routine = StartCoroutine(RotateGroupClockwise(group, Mathf.Abs(speedDegPerSec)));
        groupRotationRoutines[group] = routine;
    }

    private IEnumerator RotateGroupClockwise(Transform group, float speedDegPerSec)
    {
        while (patternSequenceRunning && group != null)
        {
            group.Rotate(0f, 0f, -speedDegPerSec * Time.deltaTime, Space.Self);
            yield return null;
        }
    }

    private void SpawnProjectileFromPhase(PotionPhaseSpec phase, PatternSpawnRequest request)
    {
        if (phase == null || !patternSequenceRunning) return;

        GameObject projectilePrefab = ResolveProjectilePrefab(phase);
        GameObject projectileObj = projectilePrefab != null
            ? Instantiate(projectilePrefab)
            : new GameObject("PotionProjectile");

        projectileObj.name = "PotionProjectile";

        Vector2 dir = request.Direction.sqrMagnitude > 0.0001f ? request.Direction.normalized : Vector2.up;
        float totalOffset = Mathf.Max(0f, projectileSpawnOffset + request.SpawnOffsetUnits);
        Vector2 spawnPos = (Vector2)transform.position + (dir * totalOffset);
        projectileObj.transform.position = spawnPos;

        if (request.ParentGroup != null)
        {
            projectileObj.transform.SetParent(request.ParentGroup, true);
        }

        PotionProjectileController projectile = projectileObj.GetComponent<PotionProjectileController>();
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<PotionProjectileController>();
        }

        float life = Mathf.Max(projectileLifetime, Mathf.Max(0.1f, activeSequenceEndTime - Time.time + 0.25f));
        bool useLocalSpace = request.PatternType == ProjectilePatternType.Tornado && request.ParentGroup != null;

        projectile.Init(
            transform,
            phase,
            dir,
            request.SpeedUnitsPerSec,
            life,
            0f,
            null,
            projectilePrefab == null,
            useLocalSpace,
            GetInstanceID(),
            request.PhaseIndex,
            request.PatternType,
            NormalizeAngle(request.LineAngleDeg));

        activeProjectiles.Add(projectile);
    }

    private void ConvertAfterimageProjectilesToHazards(bool isFirstPhase, int phaseIndex)
    {
        float[] allowedAngles = ProjectilePatternExecutor.GetAfterimageAngles(isFirstPhase);

        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            PotionProjectileController projectile = activeProjectiles[i];
            if (projectile == null) continue;
            if (projectile.SourceBombId != GetInstanceID()) continue;
            if (projectile.PatternType != ProjectilePatternType.AfterimageBomb) continue;
            if (projectile.PhaseIndex != phaseIndex) continue;
            if (!IsAngleMatched(projectile.LineAngleDeg, allowedAngles)) continue;

            SpawnAfterimageHazard(projectile.PhaseSpec, projectile.transform.position);
            Destroy(projectile.gameObject);
            activeProjectiles[i] = null;
        }
    }

    private void SpawnAfterimageHazard(PotionPhaseSpec phase, Vector2 worldPosition)
    {
        if (phase == null) return;

        GameObject hazardObj = new GameObject("PotionAreaHazard");
        hazardObj.transform.SetParent(transform, true);
        hazardObj.transform.position = worldPosition;

        PotionAreaHazard hazard = hazardObj.AddComponent<PotionAreaHazard>();
        hazard.Init(
            phase,
            new Vector2(AfterimageHazardSizeUnits, AfterimageHazardSizeUnits),
            AfterimageHazardDurationSeconds);

        activeHazards.Add(hazard);
    }

    private IEnumerator WaitForHazardsToExpire(float timeout)
    {
        float endTime = Time.time + Mathf.Max(0f, timeout);

        while (Time.time < endTime)
        {
            PruneNullHazards();
            if (activeHazards.Count == 0)
            {
                yield break;
            }

            yield return null;
        }
    }

    private void CleanupRuntimeObjects(bool includeHazards)
    {
        foreach (KeyValuePair<Transform, Coroutine> pair in groupRotationRoutines)
        {
            if (pair.Value != null)
            {
                StopCoroutine(pair.Value);
            }
        }

        groupRotationRoutines.Clear();

        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            PotionProjectileController projectile = activeProjectiles[i];
            if (projectile == null) continue;
            Destroy(projectile.gameObject);
        }

        activeProjectiles.Clear();

        for (int i = 0; i < runtimeGroups.Count; i++)
        {
            Transform group = runtimeGroups[i];
            if (group == null) continue;
            Destroy(group.gameObject);
        }

        runtimeGroups.Clear();

        if (includeHazards)
        {
            for (int i = 0; i < activeHazards.Count; i++)
            {
                PotionAreaHazard hazard = activeHazards[i];
                if (hazard == null) continue;
                Destroy(hazard.gameObject);
            }

            activeHazards.Clear();
        }
    }

    private void ForceImmediateCleanup()
    {
        patternSequenceRunning = false;
        CleanupRuntimeObjects(includeHazards: true);
    }

    private void PruneNullHazards()
    {
        activeHazards.RemoveAll(h => h == null);
    }

    private static bool IsAngleMatched(float angle, float[] allowedAngles)
    {
        float normalized = NormalizeAngle(angle);
        for (int i = 0; i < allowedAngles.Length; i++)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(normalized, NormalizeAngle(allowedAngles[i]))) <= 0.1f)
            {
                return true;
            }
        }

        return false;
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    private GameObject ResolveProjectilePrefab(PotionPhaseSpec phase)
    {
        if (phase != null)
        {
            GameObject byPhaseElement = GetProjectilePrefabByElement(phase.primaryElement);
            if (byPhaseElement != null)
            {
                return byPhaseElement;
            }
        }

        GameObject byBombElement = GetProjectilePrefabByElement(bombElement);
        if (byBombElement != null)
        {
            return byBombElement;
        }

        if (waterProjectileVfxPrefab != null) return waterProjectileVfxPrefab;
        if (fireProjectileVfxPrefab != null) return fireProjectileVfxPrefab;
        if (electricProjectileVfxPrefab != null) return electricProjectileVfxPrefab;
        return null;
    }

    private GameObject GetProjectilePrefabByElement(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => fireProjectileVfxPrefab,
            ElementType.Electric => electricProjectileVfxPrefab,
            _ => waterProjectileVfxPrefab
        };
    }
}
