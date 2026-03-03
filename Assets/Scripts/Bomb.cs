using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Bomb : MonoBehaviour
{
    private const float PatternTotalSeconds = 8f;
    private const float SecondPhaseDelaySeconds = 2f;
    private const float AfterimageExplosionDurationSeconds = 2f;
    private const float AfterimageExplosionSizePx = 64f;
    private const float PixelPerUnit = 32f;
    private const float LineAngleToleranceDeg = 0.5f;

    [Header("Bomb Settings")]
    public ElementType bombElement = ElementType.Water;
    public int baseDamage = 200;
    public float timeToExplode = 2.0f;
    public GameObject explosionEffect;

    [Header("Bomb Visual")]
    [SerializeField] private BombVisualRenderer visualRenderer;

    [Header("Pattern Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private GameObject waterProjectileVfxPrefab;
    [SerializeField] private GameObject fireProjectileVfxPrefab;
    [SerializeField] private GameObject electricProjectileVfxPrefab;
    [FormerlySerializedAs("projectileLifetime")]
    [SerializeField] private float defaultProjectileLifetime = 3f;
    [SerializeField] private float projectileLifetimeFromPhaseDurationScale = 0.35f;
    [SerializeField] private float minProjectileLifetime = 0.25f;
    [FormerlySerializedAs("projectileSpawnOffset")]
    [SerializeField] private float projectileSpawnOffset = 0.1f;

    [Header("Debug")]
    [FormerlySerializedAs("debugDisablePatternSpawn")]
    [FormerlySerializedAs("spawnProjectilePatterns")]
    [SerializeField] private bool spawnProjectilePatterns = true;
    [SerializeField] private bool debugVisualOnlyExplosion;

    private PotionData sourcePotionData;
    private Transform projectileOwner;
    private PotionPhaseSpec phase1Spec;
    private PotionPhaseSpec phase2Spec;
    private int bombInstanceId;
    private Coroutine lifecycleRoutine;
    private Coroutine phase1Routine;
    private Coroutine phase2Routine;
    private readonly List<Coroutine> rotationRoutines = new List<Coroutine>();
    private readonly List<Transform> phaseGroups = new List<Transform>();
    private readonly List<PotionProjectileController> spawnedProjectiles = new List<PotionProjectileController>();
    private readonly List<PotionAreaHazard> spawnedHazards = new List<PotionAreaHazard>();
    private bool suppressForcedCleanup;

    private void Awake()
    {
        bombInstanceId = gameObject.GetInstanceID();
        ResolveVisualRenderer();
    }

    public void ConfigureFromPotionData(PotionData potionData)
    {
        sourcePotionData = potionData;
        if (potionData != null)
        {
            baseDamage = Mathf.Max(1, potionData.damage1 + potionData.damage2);
            bombElement = potionData.element1 switch
            {
                Element.Fire => ElementType.Fire,
                Element.Lightning => ElementType.Electric,
                _ => ElementType.Water
            };
        }

        ApplyPotionVisual();
    }

    public void SetProjectileOwner(Transform ownerTransform)
    {
        projectileOwner = ownerTransform;
    }

    private void Start()
    {
        ApplyPotionVisual();
        StartCoroutine(ExplodeSequence());
    }

    private IEnumerator ExplodeSequence()
    {
        yield return new WaitForSeconds(timeToExplode);
        Explode();
    }

    private void Explode()
    {
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, transform.position, Quaternion.identity);
        }

        // AoE direct damage is intentionally removed. Bombs now deal damage only via spawned pattern projectiles.
        if (debugVisualOnlyExplosion || !spawnProjectilePatterns)
        {
            Destroy(gameObject);
            return;
        }

        if (lifecycleRoutine != null)
        {
            StopCoroutine(lifecycleRoutine);
        }

        lifecycleRoutine = StartCoroutine(RunPatternLifecycle());
    }

    private IEnumerator RunPatternLifecycle()
    {
        phase1Spec = sourcePotionData != null ? sourcePotionData.GetPhase(0) : null;
        phase2Spec = sourcePotionData != null ? sourcePotionData.GetPhase(1) : null;

        if (phase1Spec == null && phase2Spec == null)
        {
            phase1Spec = BuildFallbackPhase();
        }

        Vector2 anchorCenter = ResolveAnchorCenter();
        Vector2 baseDirection = ResolveBaseDirection();

        float patternStartTime = Time.time;
        float globalEndTime = patternStartTime + PatternTotalSeconds;

        if (phase1Spec != null)
        {
            phase1Routine = StartCoroutine(RunPatternPhase(
                phase1Spec,
                true,
                anchorCenter,
                baseDirection,
                patternStartTime,
                globalEndTime));
        }

        if (phase2Spec != null)
        {
            phase2Routine = StartCoroutine(RunPatternPhaseWithDelay(
                phase2Spec,
                anchorCenter,
                baseDirection,
                patternStartTime + SecondPhaseDelaySeconds,
                globalEndTime));
        }

        while (Time.time < globalEndTime)
        {
            yield return null;
        }

        StopActivePatternRoutines();
        ConvertAfterimageProjectilesToHazards();
        CleanupPatternState(destroyProjectiles: true, destroyHazards: false, destroyGroups: true, stopCoroutines: false);

        suppressForcedCleanup = true;
        lifecycleRoutine = null;
        Destroy(gameObject);
    }

    private IEnumerator RunPatternPhaseWithDelay(
        PotionPhaseSpec phase,
        Vector2 anchorCenter,
        Vector2 baseDirection,
        float phaseStartTime,
        float globalEndTime)
    {
        if (!TryWaitUntil(phaseStartTime, globalEndTime, out float waitToPhase))
        {
            yield break;
        }

        if (waitToPhase > 0f)
        {
            yield return new WaitForSeconds(waitToPhase);
        }

        if (Time.time >= globalEndTime)
        {
            yield break;
        }

        yield return StartCoroutine(RunPatternPhase(
            phase,
            false,
            anchorCenter,
            baseDirection,
            phaseStartTime,
            globalEndTime));
    }

    private IEnumerator RunPatternPhase(
        PotionPhaseSpec phase,
        bool isFirstPhase,
        Vector2 anchorCenter,
        Vector2 baseDirection,
        float phaseStartTime,
        float globalEndTime)
    {
        if (phase == null)
        {
            yield break;
        }

        Transform phaseGroup = new GameObject(isFirstPhase ? "BombPhase1Group" : "BombPhase2Group").transform;
        phaseGroup.position = anchorCenter;
        phaseGroup.rotation = Quaternion.identity;
        phaseGroups.Add(phaseGroup);

        Coroutine rotateRoutine = null;
        float groupRotateSpeedDegPerSec = 0f;

        void SetGroupRotationActive(bool active, float speedDegPerSec)
        {
            groupRotateSpeedDegPerSec = active ? speedDegPerSec : 0f;

            if (active)
            {
                if (rotateRoutine == null)
                {
                    rotateRoutine = StartCoroutine(RotateGroupRoutine(phaseGroup, () => groupRotateSpeedDegPerSec));
                    rotationRoutines.Add(rotateRoutine);
                }

                return;
            }

            if (rotateRoutine != null)
            {
                StopCoroutine(rotateRoutine);
                rotationRoutines.Remove(rotateRoutine);
                rotateRoutine = null;
            }
        }

        yield return StartCoroutine(ProjectilePatternExecutor.ExecutePhase(
            phase,
            isFirstPhase,
            anchorCenter,
            baseDirection,
            SpawnPatternProjectile,
            SetGroupRotationActive,
            phaseGroup,
            null,
            phaseStartTime,
            globalEndTime));

        if (rotateRoutine != null)
        {
            StopCoroutine(rotateRoutine);
            rotationRoutines.Remove(rotateRoutine);
        }

        if (phaseGroup != null)
        {
            phaseGroups.Remove(phaseGroup);
            Destroy(phaseGroup.gameObject);
        }
    }

    private IEnumerator RotateGroupRoutine(Transform phaseGroup, Func<float> getRotateSpeed)
    {
        while (phaseGroup != null)
        {
            float rotateSpeed = getRotateSpeed != null ? getRotateSpeed() : 0f;
            if (Mathf.Abs(rotateSpeed) > 0.01f)
            {
                phaseGroup.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
            }

            yield return null;
        }
    }

    private void SpawnPatternProjectile(PatternSpawnRequest request)
    {
        PotionPhaseSpec phase = ResolvePhaseSpec(request.PhaseIndex);
        if (phase == null)
        {
            return;
        }

        Vector2 dir = request.Direction.sqrMagnitude > 0.0001f ? request.Direction.normalized : Vector2.up;
        float offsetUnits = Mathf.Max(0f, request.SpawnOffsetUnits) + Mathf.Max(0f, projectileSpawnOffset);
        Vector3 offset = (Vector3)(dir * offsetUnits);
        bool useLocalSpaceMovement = request.PatternType == ProjectilePatternType.Tornado && request.ParentGroup != null;
        Vector3 anchorCenter = new Vector3(request.AnchorCenter.x, request.AnchorCenter.y, transform.position.z);

        GameObject prefabToSpawn = ResolvePatternProjectilePrefab(phase);
        GameObject projectileObj = prefabToSpawn != null
            ? Instantiate(prefabToSpawn)
            : new GameObject("PotionPatternProjectile");

        Transform projectileTransform = projectileObj.transform;
        if (useLocalSpaceMovement)
        {
            projectileTransform.SetParent(request.ParentGroup, false);
            projectileTransform.localPosition = offset;
            projectileTransform.localRotation = Quaternion.identity;
        }
        else
        {
            projectileTransform.position = anchorCenter + offset;
        }

        PotionProjectileController controller = projectileObj.GetComponent<PotionProjectileController>();
        if (controller == null)
        {
            controller = projectileObj.AddComponent<PotionProjectileController>();
        }

        Transform hitOwner = projectileOwner != null ? projectileOwner : transform;
        bool allowFallbackSprite = prefabToSpawn == null;

        controller.Init(
            hitOwner,
            phase,
            dir,
            request.SpeedUnitsPerSec,
            ResolveProjectileLifetime(phase),
            0f,
            null,
            allowFallbackSprite,
            useLocalSpaceMovement,
            bombInstanceId,
            request.PhaseIndex,
            request.PatternType,
            request.LineAngleDeg);

        spawnedProjectiles.Add(controller);
    }

    private GameObject ResolvePatternProjectilePrefab(PotionPhaseSpec phase)
    {
        if (projectilePrefab != null)
        {
            return projectilePrefab;
        }

        ElementType element = phase != null ? NormalizeElement(phase.primaryElement) : NormalizeElement(bombElement);
        return element switch
        {
            ElementType.Fire => fireProjectileVfxPrefab,
            ElementType.Electric => electricProjectileVfxPrefab,
            _ => waterProjectileVfxPrefab
        };
    }

    private PotionPhaseSpec ResolvePhaseSpec(int phaseIndex)
    {
        if (phaseIndex == 1) return phase1Spec;
        if (phaseIndex == 2) return phase2Spec;
        return phase1Spec ?? phase2Spec;
    }

    private float ResolveProjectileLifetime(PotionPhaseSpec phase)
    {
        if (phase == null)
        {
            return Mathf.Max(0.1f, Mathf.Max(defaultProjectileLifetime, PatternTotalSeconds + 0.1f));
        }

        float scaledByPhase = Mathf.Max(0f, phase.duration) * Mathf.Max(0f, projectileLifetimeFromPhaseDurationScale);
        float baseLife = scaledByPhase > 0f ? scaledByPhase : defaultProjectileLifetime;
        float patternMinimum = PatternTotalSeconds + 0.1f;
        return Mathf.Max(minProjectileLifetime, Mathf.Max(baseLife, patternMinimum));
    }

    private PotionPhaseSpec BuildFallbackPhase()
    {
        return new PotionPhaseSpec
        {
            patternType = ProjectilePatternType.Fireworks,
            duration = 0.2f,
            projectileSpeed = 6f,
            baseDamage = Mathf.Max(1, baseDamage),
            primaryElement = NormalizeElement(bombElement),
            subElement = ElementType.None,
            damageTarget = DamageTargetType.EnemyOnly
        };
    }

    private void ResolveVisualRenderer()
    {
        if (visualRenderer != null)
        {
            return;
        }

        visualRenderer = GetComponent<BombVisualRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = gameObject.AddComponent<BombVisualRenderer>();
        }
    }

    private void ApplyPotionVisual()
    {
        ResolveVisualRenderer();
        if (visualRenderer != null)
        {
            visualRenderer.Apply(sourcePotionData);
        }
    }

    private static ElementType NormalizeElement(ElementType element)
    {
        return element == ElementType.None ? ElementType.Water : element;
    }

    private Vector2 ResolveAnchorCenter()
    {
        // One bomb owns one bullet-pattern center: start from bomb world position.
        return transform.position;
    }

    private Vector2 ResolveBaseDirection()
    {
        if (projectileOwner == null)
        {
            return Vector2.up;
        }

        Vector2 dir = (Vector2)projectileOwner.position - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.0001f)
        {
            return Vector2.up;
        }

        return dir.normalized;
    }

    private void ConvertAfterimageProjectilesToHazards()
    {
        float[] firstPhaseAngles = ProjectilePatternExecutor.GetAfterimageAngles(true);
        float[] secondPhaseAngles = ProjectilePatternExecutor.GetAfterimageAngles(false);
        Vector2 hazardSize = Vector2.one * (AfterimageExplosionSizePx / PixelPerUnit);

        for (int i = 0; i < spawnedProjectiles.Count; i++)
        {
            PotionProjectileController projectile = spawnedProjectiles[i];
            if (projectile == null) continue;
            if (projectile.SourceBombId != bombInstanceId) continue;
            if (projectile.PatternType != ProjectilePatternType.AfterimageBomb) continue;

            bool onAllowedLine = projectile.PhaseIndex switch
            {
                1 => IsAngleInLines(projectile.LineAngleDeg, firstPhaseAngles),
                2 => IsAngleInLines(projectile.LineAngleDeg, secondPhaseAngles),
                _ => IsAngleInLines(projectile.LineAngleDeg, firstPhaseAngles) || IsAngleInLines(projectile.LineAngleDeg, secondPhaseAngles)
            };

            if (!onAllowedLine)
            {
                continue;
            }

            PotionPhaseSpec phase = ResolvePhaseSpec(projectile.PhaseIndex) ?? phase1Spec ?? phase2Spec;
            SpawnAreaHazard(projectile.transform.position, phase, hazardSize, AfterimageExplosionDurationSeconds, projectile.PhaseIndex);
            Destroy(projectile.gameObject);
        }
    }

    private void SpawnAreaHazard(Vector3 worldPosition, PotionPhaseSpec phase, Vector2 sizeUnits, float durationSeconds, int phaseIndex)
    {
        if (phase == null)
        {
            return;
        }

        GameObject hazardObject = new GameObject("PotionAfterimageHazard");
        hazardObject.transform.position = worldPosition;

        PotionAreaHazard hazard = hazardObject.AddComponent<PotionAreaHazard>();
        hazard.Init(phase, sizeUnits, durationSeconds, bombInstanceId, phaseIndex);
        spawnedHazards.Add(hazard);
    }

    private static bool IsAngleInLines(float angleDeg, float[] lineAngles)
    {
        if (lineAngles == null)
        {
            return false;
        }

        for (int i = 0; i < lineAngles.Length; i++)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angleDeg, lineAngles[i])) <= LineAngleToleranceDeg)
            {
                return true;
            }
        }

        return false;
    }

    private void StopActivePatternRoutines()
    {
        if (phase1Routine != null)
        {
            StopCoroutine(phase1Routine);
            phase1Routine = null;
        }

        if (phase2Routine != null)
        {
            StopCoroutine(phase2Routine);
            phase2Routine = null;
        }

        for (int i = 0; i < rotationRoutines.Count; i++)
        {
            Coroutine rotateRoutine = rotationRoutines[i];
            if (rotateRoutine != null)
            {
                StopCoroutine(rotateRoutine);
            }
        }

        rotationRoutines.Clear();
    }

    private void CleanupPatternState(bool destroyProjectiles, bool destroyHazards, bool destroyGroups, bool stopCoroutines)
    {
        if (stopCoroutines)
        {
            StopAllCoroutines();
            lifecycleRoutine = null;
            phase1Routine = null;
            phase2Routine = null;
            rotationRoutines.Clear();
        }

        if (destroyGroups)
        {
            for (int i = 0; i < phaseGroups.Count; i++)
            {
                Transform group = phaseGroups[i];
                if (group != null)
                {
                    Destroy(group.gameObject);
                }
            }

            phaseGroups.Clear();
        }

        if (destroyProjectiles)
        {
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                PotionProjectileController projectile = spawnedProjectiles[i];
                if (projectile != null)
                {
                    Destroy(projectile.gameObject);
                }
            }

            PotionProjectileController[] allProjectiles = FindObjectsByType<PotionProjectileController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < allProjectiles.Length; i++)
            {
                PotionProjectileController projectile = allProjectiles[i];
                if (projectile != null && projectile.SourceBombId == bombInstanceId)
                {
                    Destroy(projectile.gameObject);
                }
            }

            spawnedProjectiles.Clear();
        }

        if (destroyHazards)
        {
            for (int i = 0; i < spawnedHazards.Count; i++)
            {
                PotionAreaHazard hazard = spawnedHazards[i];
                if (hazard != null)
                {
                    Destroy(hazard.gameObject);
                }
            }

            PotionAreaHazard[] allHazards = FindObjectsByType<PotionAreaHazard>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < allHazards.Length; i++)
            {
                PotionAreaHazard hazard = allHazards[i];
                if (hazard != null && hazard.SourceBombId == bombInstanceId)
                {
                    Destroy(hazard.gameObject);
                }
            }

            spawnedHazards.Clear();
        }
    }

    private void OnDisable()
    {
        if (suppressForcedCleanup)
        {
            return;
        }

        CleanupPatternState(destroyProjectiles: true, destroyHazards: true, destroyGroups: true, stopCoroutines: true);
    }

    private void OnDestroy()
    {
        if (suppressForcedCleanup)
        {
            return;
        }

        CleanupPatternState(destroyProjectiles: true, destroyHazards: true, destroyGroups: true, stopCoroutines: true);
    }

    private static bool TryWaitUntil(float targetTime, float maxEndTime, out float wait)
    {
        wait = 0f;

        if (float.IsPositiveInfinity(maxEndTime))
        {
            wait = Mathf.Max(0f, targetTime - Time.time);
            return true;
        }

        if (Time.time >= maxEndTime)
        {
            return false;
        }

        wait = Mathf.Max(0f, targetTime - Time.time);
        float remaining = maxEndTime - Time.time;
        if (wait > remaining)
        {
            return false;
        }

        return true;
    }
}
