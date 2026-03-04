using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class Bomb : MonoBehaviour
{
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

    [Header("Debug")]
    [FormerlySerializedAs("debugDisablePatternSpawn")]
    [FormerlySerializedAs("spawnProjectilePatterns")]
    [SerializeField] private bool spawnProjectilePatterns = true;
    [SerializeField] private bool debugVisualOnlyExplosion;

    private PotionData sourcePotionData;
    private Transform projectileOwner;
    private int bombInstanceId;

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

        if (debugVisualOnlyExplosion || !spawnProjectilePatterns)
        {
            Destroy(gameObject);
            return;
        }

        SpawnSingleProjectile();
        Destroy(gameObject);
    }

    private void SpawnSingleProjectile()
    {
        PotionPhaseSpec phase = ResolveShotPhase(out int phaseIndex);
        if (phase == null)
        {
            return;
        }

        Vector2 baseDirection = ResolveBaseDirection();
        Vector2 shotDirection = phaseIndex == 2 ? -baseDirection : baseDirection;

        float speed = ResolveProjectileSpeed(phase);
        float lifetime = ResolveProjectileLifetime(phase);
        float offsetUnits = Mathf.Max(0f, projectileSpawnOffset);
        Vector3 spawnPosition = transform.position + (Vector3)(shotDirection * offsetUnits);

        GameObject prefabToSpawn = ResolveProjectilePrefab(phase);
        GameObject projectileObj = prefabToSpawn != null
            ? Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity)
            : new GameObject("PotionPatternProjectile");

        if (prefabToSpawn == null)
        {
            projectileObj.transform.position = spawnPosition;
        }

        PotionProjectileController controller = projectileObj.GetComponent<PotionProjectileController>();
        if (controller == null)
        {
            controller = projectileObj.AddComponent<PotionProjectileController>();
        }

        Transform hitOwner = projectileOwner != null ? projectileOwner : transform;
        bool allowFallbackSprite = prefabToSpawn == null;
        float lineAngleDeg = Mathf.Atan2(shotDirection.y, shotDirection.x) * Mathf.Rad2Deg;

        controller.Init(
            hitOwner,
            phase,
            shotDirection,
            speed,
            lifetime,
            0f,
            null,
            allowFallbackSprite,
            false,
            bombInstanceId,
            phaseIndex,
            ProjectilePatternType.Fireworks,
            lineAngleDeg);
    }

    private PotionPhaseSpec ResolveShotPhase(out int phaseIndex)
    {
        PotionPhaseSpec phase1 = sourcePotionData != null ? sourcePotionData.GetPhase(0) : null;
        if (phase1 != null)
        {
            phaseIndex = 1;
            return phase1;
        }

        PotionPhaseSpec phase2 = sourcePotionData != null ? sourcePotionData.GetPhase(1) : null;
        if (phase2 != null)
        {
            phaseIndex = 2;
            return phase2;
        }

        phaseIndex = 1;
        return BuildFallbackPhase();
    }

    private float ResolveProjectileSpeed(PotionPhaseSpec phase)
    {
        if (phase == null)
        {
            return 6f;
        }

        return phase.projectileSpeed > 0f ? phase.projectileSpeed : 6f;
    }

    private float ResolveProjectileLifetime(PotionPhaseSpec phase)
    {
        if (phase == null)
        {
            return Mathf.Max(0.1f, defaultProjectileLifetime);
        }

        float scaledByPhase = Mathf.Max(0f, phase.duration) * Mathf.Max(0f, projectileLifetimeFromPhaseDurationScale);
        float baseLife = scaledByPhase > 0f ? scaledByPhase : defaultProjectileLifetime;
        return Mathf.Max(minProjectileLifetime, Mathf.Max(0.1f, baseLife));
    }

    private GameObject ResolveProjectilePrefab(PotionPhaseSpec phase)
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
}
