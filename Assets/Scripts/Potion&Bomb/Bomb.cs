using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public class Bomb : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private const float DesignFramesPerSecond = 60f;
    private const float FireworksSpeedPxPerFrame = 120f;
    private const float AfterimageSpeedPxPerFrame = 64f;

    private const float AfterimageFirstShotTime = 0f;
    private const float AfterimageSecondShotTime = 3f;
    private const float AfterimageThirdShotTime = 6f;
    private const float AfterimageExplosionDelaySeconds = 8f;

    private const float AfterimageExplosionSizePx = 64f;
    private const float AfterimageExplosionLifetimeSeconds = 0.08f;

    [Header("Bomb Settings")]
    public ElementType bombElement = ElementType.Water;
    public int baseDamage = 200;
    public float timeToExplode = 2.0f;
    public GameObject explosionEffect;

    [Header("Bomb Visual")]
    [SerializeField] private BombVisualRenderer visualRenderer;

    [Header("Projectile")]
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
    [SerializeField] private float projectileSpeedMultiplier = 0.04f;

    [Header("Debug")]
    [FormerlySerializedAs("debugDisablePatternSpawn")]
    [FormerlySerializedAs("spawnProjectilePatterns")]
    [SerializeField] private bool spawnProjectilePatterns = true;
    [SerializeField] private bool debugVisualOnlyExplosion;

    private PotionData sourcePotionData;
    private Transform projectileOwner;
    private Vector2 forcedBaseDirection;
    private bool hasForcedBaseDirection;
    private int bombInstanceId;
    private bool hasExploded;

    private readonly List<PotionProjectileController> trackedAfterimageProjectiles = new();

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

    public void SetPatternBaseDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            hasForcedBaseDirection = false;
            forcedBaseDirection = Vector2.zero;
            return;
        }

        forcedBaseDirection = direction.normalized;
        hasForcedBaseDirection = true;
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
        if (hasExploded)
        {
            return;
        }

        hasExploded = true;

        if (explosionEffect != null)
        {
            GameObject explosionObj = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            ApplyFieldVisualScaleIfNeeded(explosionObj);
        }

        HideBombVisual();

        if (debugVisualOnlyExplosion || !spawnProjectilePatterns)
        {
            DestroyBombObject();
            return;
        }

        StartCoroutine(SpawnPatternSequenceThenDestroy());
    }

    private IEnumerator SpawnPatternSequenceThenDestroy()
    {
        PotionPhaseSpec phase1 = sourcePotionData != null ? sourcePotionData.GetPhase(0) : null;
        PotionPhaseSpec phase2 = sourcePotionData != null ? sourcePotionData.GetPhase(1) : null;
        PotionPhaseSpec drivingPhase = phase1 ?? phase2 ?? BuildFallbackPhase();

        switch (drivingPhase.patternType)
        {
            case ProjectilePatternType.Fireworks:
                yield return RunFireworksPatternSequence(phase1, phase2);
                break;

            case ProjectilePatternType.AfterimageBomb:
                yield return RunAfterimageBombPatternSequence(phase1, phase2);
                break;

            default:
            {
                int defaultPhaseIndex = phase1 != null ? 1 : (phase2 != null ? 2 : 1);
                SpawnProjectilePattern(
                    drivingPhase.patternType,
                    drivingPhase,
                    defaultPhaseIndex);
                break;
            }
        }

        DestroyBombObject();
    }

    private IEnumerator RunFireworksPatternSequence(PotionPhaseSpec phase1, PotionPhaseSpec phase2)
    {
        SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(phase1, phase2, 1), 1);
        yield return new WaitForSeconds(2f);

        SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(phase1, phase2, 2), 2);
        yield return new WaitForSeconds(2f);

        SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(phase1, phase2, 1), 1);
        yield return new WaitForSeconds(2f);

        SpawnProjectilePattern(ProjectilePatternType.Fireworks, ResolveMaterialPhase(phase1, phase2, 2), 2);
    }

    private IEnumerator RunAfterimageBombPatternSequence(PotionPhaseSpec phase1, PotionPhaseSpec phase2)
    {
        trackedAfterimageProjectiles.Clear();

        SpawnProjectilePattern(
            ProjectilePatternType.AfterimageBomb,
            ResolveMaterialPhase(phase1, phase2, 1),
            1,
            RegisterAfterimageProjectile);

        float waitToSecond = Mathf.Max(0f, AfterimageSecondShotTime - AfterimageFirstShotTime);
        if (waitToSecond > 0f)
        {
            yield return new WaitForSeconds(waitToSecond);
        }

        SpawnProjectilePattern(
            ProjectilePatternType.AfterimageBomb,
            ResolveMaterialPhase(phase1, phase2, 2),
            2,
            RegisterAfterimageProjectile);

        float waitToThird = Mathf.Max(0f, AfterimageThirdShotTime - AfterimageSecondShotTime);
        if (waitToThird > 0f)
        {
            yield return new WaitForSeconds(waitToThird);
        }

        SpawnProjectilePattern(
            ProjectilePatternType.AfterimageBomb,
            ResolveMaterialPhase(phase1, phase2, 1),
            1,
            RegisterAfterimageProjectile);

        float waitToExplosion = Mathf.Max(0f, AfterimageExplosionDelaySeconds - AfterimageThirdShotTime);
        if (waitToExplosion > 0f)
        {
            yield return new WaitForSeconds(waitToExplosion);
        }

        ExplodeRemainingAfterimageProjectiles();
        trackedAfterimageProjectiles.Clear();
    }

    private void SpawnProjectilePattern(
        ProjectilePatternType patternType,
        PotionPhaseSpec phase,
        int phaseIndex,
        Action<PotionProjectileController> onProjectileSpawn = null)
    {
        PotionPhaseSpec resolvedPhase = phase ?? BuildFallbackPhase();

        float speed = ResolveProjectileSpeed(patternType, resolvedPhase);
        float lifetime = ResolveProjectileLifetime(patternType, resolvedPhase);
        Vector2 baseDirection = ResolveBaseDirection();
        float offsetUnits = Mathf.Max(0f, projectileSpawnOffset);
        Vector3 spawnCenter = transform.position + (Vector3)(baseDirection * offsetUnits);

        GameObject prefabToSpawn = ResolveProjectilePrefab(resolvedPhase);
        Transform hitOwner = projectileOwner != null ? projectileOwner : transform;

        BombProjectilePatternSpawner.Spawn(
            patternType,
            resolvedPhase,
            prefabToSpawn,
            hitOwner,
            spawnCenter,
            baseDirection,
            bombInstanceId,
            phaseIndex,
            speed,
            lifetime,
            onProjectileSpawn);
    }

    private static PotionPhaseSpec ResolveMaterialPhase(PotionPhaseSpec phase1, PotionPhaseSpec phase2, int materialIndex)
    {
        if (materialIndex == 2)
        {
            return phase2 ?? phase1;
        }

        return phase1 ?? phase2;
    }

    private float ResolveProjectileSpeed(ProjectilePatternType patternType, PotionPhaseSpec phase)
    {
        float resolvedSpeed;
        switch (patternType)
        {
            case ProjectilePatternType.Fireworks:
                resolvedSpeed = ConvertPixelsPerFrameToUnitsPerSecond(FireworksSpeedPxPerFrame);
                break;

            case ProjectilePatternType.AfterimageBomb:
                resolvedSpeed = ConvertPixelsPerFrameToUnitsPerSecond(AfterimageSpeedPxPerFrame);
                break;

            default:
                resolvedSpeed = phase != null && phase.projectileSpeed > 0f ? phase.projectileSpeed : 6f;
                break;
        }

        float multiplier = Mathf.Max(0.01f, projectileSpeedMultiplier);
        return Mathf.Max(0.1f, resolvedSpeed * multiplier);
    }

    private float ResolveProjectileLifetime(ProjectilePatternType patternType, PotionPhaseSpec phase)
    {
        float lifetime;
        if (phase == null)
        {
            lifetime = Mathf.Max(0.1f, defaultProjectileLifetime);
        }
        else
        {
            float scaledByPhase = Mathf.Max(0f, phase.duration) * Mathf.Max(0f, projectileLifetimeFromPhaseDurationScale);
            float baseLife = scaledByPhase > 0f ? scaledByPhase : defaultProjectileLifetime;
            lifetime = Mathf.Max(minProjectileLifetime, Mathf.Max(0.1f, baseLife));
        }

        if (patternType == ProjectilePatternType.AfterimageBomb)
        {
            lifetime = Mathf.Max(lifetime, AfterimageExplosionDelaySeconds + 0.05f);
        }

        return lifetime;
    }

    private GameObject ResolveProjectilePrefab(PotionPhaseSpec phase)
    {
        if (projectilePrefab != null)
        {
            if (ContainsBombComponent(projectilePrefab))
            {
                Debug.LogWarning($"[Bomb] projectilePrefab '{projectilePrefab.name}' contains Bomb component. Falling back to element projectile.");
            }
            else
            {
                return projectilePrefab;
            }
        }

        ElementType element = phase != null ? NormalizeElement(phase.primaryElement) : NormalizeElement(bombElement);
        return ResolveElementProjectilePrefab(element);
    }

    private GameObject ResolveElementProjectilePrefab(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => fireProjectileVfxPrefab,
            ElementType.Electric => electricProjectileVfxPrefab,
            _ => waterProjectileVfxPrefab
        };
    }

    private static bool ContainsBombComponent(GameObject prefabCandidate)
    {
        if (prefabCandidate == null)
        {
            return false;
        }

        if (prefabCandidate.GetComponent<Bomb>() != null)
        {
            return true;
        }

        return prefabCandidate.GetComponentInChildren<Bomb>(true) != null;
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
            damageTarget = DamageTargetType.Both
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

    private Vector2 ResolveBaseDirection()
    {
        if (hasForcedBaseDirection)
        {
            return forcedBaseDirection;
        }

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

    private static float ConvertPixelsPerFrameToUnitsPerSecond(float pixelsPerFrame)
    {
        return pixelsPerFrame * DesignFramesPerSecond / Mathf.Max(1f, PixelsPerUnit);
    }

    private void RegisterAfterimageProjectile(PotionProjectileController controller)
    {
        if (controller == null)
        {
            return;
        }

        if (!trackedAfterimageProjectiles.Contains(controller))
        {
            trackedAfterimageProjectiles.Add(controller);
        }
    }

    private void ExplodeRemainingAfterimageProjectiles()
    {
        Camera cam = Camera.main;
        float explosionSizeUnits = AfterimageExplosionSizePx / Mathf.Max(1f, PixelsPerUnit);

        for (int i = 0; i < trackedAfterimageProjectiles.Count; i++)
        {
            PotionProjectileController projectile = trackedAfterimageProjectiles[i];
            if (projectile == null)
            {
                continue;
            }

            if (cam != null && !IsOnScreen(cam, projectile.transform.position))
            {
                continue;
            }

            SpawnAfterimageExplosion(
                projectile.transform.position,
                BuildExplosionSpec(projectile.PhaseSpec),
                projectile.PhaseIndex,
                explosionSizeUnits);

            Destroy(projectile.gameObject);
        }
    }

    private static bool IsOnScreen(Camera cam, Vector3 position)
    {
        Vector3 viewport = cam.WorldToViewportPoint(position);
        return viewport.z > 0f
               && viewport.x >= 0f && viewport.x <= 1f
               && viewport.y >= 0f && viewport.y <= 1f;
    }

    private void SpawnAfterimageExplosion(Vector3 worldPosition, PotionPhaseSpec sourcePhase, int phaseIndex, float explosionSizeUnits)
    {
        GameObject hazardObject = new GameObject("AfterimageExplosionHazard");
        hazardObject.transform.position = worldPosition;

        PotionAreaHazard hazard = hazardObject.AddComponent<PotionAreaHazard>();
        hazard.Init(
            sourcePhase,
            new Vector2(explosionSizeUnits, explosionSizeUnits),
            AfterimageExplosionLifetimeSeconds,
            bombInstanceId,
            phaseIndex);
    }

    private PotionPhaseSpec BuildExplosionSpec(PotionPhaseSpec sourcePhase)
    {
        PotionPhaseSpec source = sourcePhase ?? BuildFallbackPhase();
        PotionPhaseSpec explosion = new PotionPhaseSpec
        {
            ingredientId = source.ingredientId,
            patternType = ProjectilePatternType.AfterimageBomb,
            useCardinalDirections = source.useCardinalDirections,
            duration = source.duration,
            fireInterval = source.fireInterval,
            projectileSpeed = source.projectileSpeed,
            rotationSpeedDegPerSec = source.rotationSpeedDegPerSec,
            baseDamage = source.baseDamage,
            primaryElement = source.primaryElement,
            subElement = source.subElement,
            damageTarget = DamageTargetType.Both,
            healsPlayerOnSelfHit = source.healsPlayerOnSelfHit,
            ignoreSelfHitPenalty = source.ignoreSelfHitPenalty
        };

        CopyEffects(source.onPlayerHitEffects, explosion.onPlayerHitEffects);
        CopyEffects(source.onEnemyHitEffects, explosion.onEnemyHitEffects);
        return explosion;
    }

    private static void CopyEffects(List<StatusEffectSpec> source, List<StatusEffectSpec> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            StatusEffectSpec effect = source[i];
            if (effect == null)
            {
                continue;
            }

            destination.Add(new StatusEffectSpec
            {
                effectType = effect.effectType,
                duration = effect.duration,
                magnitude = effect.magnitude,
                interval = effect.interval
            });
        }
    }

    private static void ApplyFieldVisualScaleIfNeeded(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        bool isFieldScene = IsFieldSceneContext(target);
        if (!isFieldScene)
        {
            return;
        }

        target.transform.localScale *= 0.25f;
    }

    private static bool IsFieldSceneContext(GameObject target)
    {
        if (target != null && IsFieldSceneName(target.scene.name))
        {
            return true;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (IsFieldSceneName(activeScene.name))
        {
            return true;
        }

        int loadedSceneCount = SceneManager.sceneCount;
        for (int i = 0; i < loadedSceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (IsFieldSceneName(loadedScene.name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFieldSceneName(string sceneName)
    {
        return string.Equals(sceneName, "FIeld", StringComparison.OrdinalIgnoreCase)
               || string.Equals(sceneName, "Field", StringComparison.OrdinalIgnoreCase);
    }

    private void HideBombVisual()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private void DestroyBombObject()
    {
        Destroy(gameObject);
    }
}
