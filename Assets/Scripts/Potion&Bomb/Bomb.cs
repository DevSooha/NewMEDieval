using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class Bomb : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private const float DesignFramesPerSecond = 60f;
    private const float FireworksSpeedPxPerFrame = 120f;
    private const float AfterimageSpeedPxPerFrame = 64f;
    private const float AfterimageExplosionDelaySeconds = 8f;

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
    private int bombInstanceId;
    private bool hasExploded;

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
            FieldSceneScaleUtility.ApplyIfNeeded(explosionObj);
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
        BombPatternExecutionContext executionContext = new(
            phase1,
            phase2,
            bombInstanceId,
            BuildFallbackPhase,
            SpawnProjectilePattern);

        yield return BombPatternSequenceRunner.Run(executionContext);
        DestroyBombObject();
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
        Vector2 baseDirection = Vector2.up;
        float offsetUnits = Mathf.Max(0f, projectileSpawnOffset);
        Vector3 spawnCenter = transform.position + (Vector3)(baseDirection * offsetUnits);

        GameObject prefabToSpawn = ResolveProjectilePrefab(resolvedPhase);

        BombProjectilePatternSpawner.Spawn(
            patternType,
            resolvedPhase,
            prefabToSpawn,
            transform,
            spawnCenter,
            baseDirection,
            bombInstanceId,
            phaseIndex,
            speed,
            lifetime,
            onProjectileSpawn);
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

    private static float ConvertPixelsPerFrameToUnitsPerSecond(float pixelsPerFrame)
    {
        return pixelsPerFrame * DesignFramesPerSecond / Mathf.Max(1f, PixelsPerUnit);
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
