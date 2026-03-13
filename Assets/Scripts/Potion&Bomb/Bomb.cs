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
    private const float TornadoSpeedPxPerFrame = 96f;
    private const float FireworksMinimumLifetimeSeconds = 4f;
    private const float AfterimageExplosionDelaySeconds = 8f;
    private const float TornadoTotalLifetimeSeconds = 8f;

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
        LogConfiguredBombInfo();
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

            case ProjectilePatternType.Tornado:
                resolvedSpeed = ConvertPixelsPerFrameToUnitsPerSecond(TornadoSpeedPxPerFrame);
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

        if (patternType == ProjectilePatternType.Fireworks)
        {
            lifetime = Mathf.Max(lifetime, FireworksMinimumLifetimeSeconds);
        }
        else if (patternType == ProjectilePatternType.AfterimageBomb)
        {
            lifetime = Mathf.Max(lifetime, AfterimageExplosionDelaySeconds + 0.05f);
        }
        else if (patternType == ProjectilePatternType.Tornado)
        {
            lifetime = Mathf.Max(lifetime, TornadoTotalLifetimeSeconds);
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

    private void LogConfiguredBombInfo()
    {
        if (sourcePotionData == null)
        {
            Debug.Log($"[Bomb] Placed bomb | object={name} | potionData=null");
            return;
        }

        PotionPhaseSpec phase1 = sourcePotionData.GetPhase(0);
        PotionPhaseSpec phase2 = sourcePotionData.GetPhase(1);
        string displayName = sourcePotionData.GetDisplayName();

        Debug.Log(
            $"[Bomb]\n" +
            $"Name: {displayName}\n" +
            $"Temp: {sourcePotionData.temperature}\n" +
            $"Ingredient1: {GetIngredientLabel(phase1)}\n" +
            $"Ingredient2: {GetIngredientLabel(phase2)}\n" +
            $"Phase1: {DescribePhaseForPlacementLog(phase1, 1)}\n" +
            $"Phase2: {DescribePhaseForPlacementLog(phase2, 2)}",
            this);
    }

    private static string GetIngredientLabel(PotionPhaseSpec phase)
    {
        return phase == null || string.IsNullOrWhiteSpace(phase.ingredientId) ? "none" : phase.ingredientId;
    }

    private static string DescribePhaseForPlacementLog(PotionPhaseSpec phase, int phaseIndex)
    {
        if (phase == null)
        {
            return $"#{phaseIndex}:none";
        }

        string effectsSummary = DescribeEffectsForPlacementLog(phase);
        return
            $"#{phaseIndex} {phase.patternType} | Elem={phase.primaryElement}/{phase.subElement} | " +
            $"Target={phase.damageTarget} | Effects={effectsSummary}";
    }

    private static string DescribeEffectsForPlacementLog(PotionPhaseSpec phase)
    {
        if (phase == null)
        {
            return "none";
        }

        System.Collections.Generic.List<string> parts = new();

        if (phase.healsPlayerOnSelfHit)
        {
            parts.Add("self-hit heals player");
        }

        if (phase.ignoreSelfHitPenalty)
        {
            parts.Add("self-hit penalty ignored");
        }

        AppendEffectDescriptions(parts, phase.onPlayerHitEffects, "player");
        AppendEffectDescriptions(parts, phase.onEnemyHitEffects, "enemy");

        return parts.Count == 0 ? "no extra status effect" : string.Join("; ", parts);
    }

    private static void AppendEffectDescriptions(
        System.Collections.Generic.List<string> parts,
        System.Collections.Generic.List<StatusEffectSpec> effects,
        string targetLabel)
    {
        if (parts == null || effects == null)
        {
            return;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffectSpec effect = effects[i];
            if (effect == null || effect.effectType == StatusEffectType.None)
            {
                continue;
            }

            parts.Add($"{targetLabel} {DescribeStatusEffect(effect)}");
        }
    }

    private static string DescribeStatusEffect(StatusEffectSpec effect)
    {
        return effect.effectType switch
        {
            StatusEffectType.HealPlayerFlat => $"heals immediately (amount {effect.magnitude:0.##})",
            StatusEffectType.HealEnemyCurrentHpPercent => $"heals current HP ratio immediately ({effect.magnitude:0.##})",
            StatusEffectType.StealthOnly => $"gets stealth for {effect.duration:0.##}s",
            StatusEffectType.StealthInvulnerable => $"gets stealth+invulnerability for {effect.duration:0.##}s",
            StatusEffectType.PlayerMoveSpeedMultiplier => $"move speed multiplier {effect.magnitude:0.##} for {effect.duration:0.##}s",
            StatusEffectType.PlayerStun => $"is stunned for {effect.duration:0.##}s",
            StatusEffectType.EnemyMoveSpeedMultiplier => $"move speed multiplier {effect.magnitude:0.##} for {effect.duration:0.##}s",
            StatusEffectType.EnemyStun => $"is stunned for {effect.duration:0.##}s",
            StatusEffectType.PlayerInputReverse => $"input is reversed for {effect.duration:0.##}s",
            StatusEffectType.PlayerInputDelay => $"input is delayed for {effect.duration:0.##}s",
            StatusEffectType.BlindBlack => $"gets black blind effect for {effect.duration:0.##}s",
            StatusEffectType.BlindWhite => $"gets white blind effect for {effect.duration:0.##}s",
            StatusEffectType.EnemyKnockback => $"is knocked back by {effect.magnitude:0.##}",
            StatusEffectType.PoisonDot => $"takes {effect.magnitude:0.##} damage every {effect.interval:0.##}s for {effect.duration:0.##}s",
            StatusEffectType.PlayerRedStateContactBurn => $"gets contact burn for {effect.duration:0.##}s, {effect.magnitude:0.##} damage every {effect.interval:0.##}s",
            StatusEffectType.PlayerKnockbackImmune => $"gets knockback immunity for {effect.duration:0.##}s",
            _ => effect.effectType.ToString()
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
