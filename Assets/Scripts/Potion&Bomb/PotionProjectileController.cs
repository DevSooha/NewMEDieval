using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class PotionProjectileController : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float offscreenMargin = 0.2f;
    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "EnemyBullet";
    [SerializeField] private int sortingOrder = 50;
    [SerializeField] private bool logRenderWarnings = true;
    private static Sprite fallbackSprite;

    private Vector2 moveDirection;
    private float moveSpeed;
    private float lived;
    private float rotationSpeedDegPerSec;
    private bool moveInLocalSpace;
    private bool useTornadoOrbit;
    private float movementStartDelay;
    private float tornadoLinearDuration;
    private float tornadoOrbitAngularSpeedDegPerSec;
    private Transform tornadoOrbitCenter;
    private Vector3 tornadoOrbitOffset;
    private float tornadoOrbitAngleDeg;
    private bool tornadoOrbitStarted;

    private Transform owner;
    private PotionPhaseSpec phaseSpec;
    private bool initialized;
    private CircleCollider2D hitCollider;
    private bool contactDamageEnabled;
    private Camera cachedCamera;
    private bool offscreenDamageDisabled;

    private int sourceBombId;
    private int phaseIndex;
    private ProjectilePatternType patternType;
    private float lineAngleDeg;

    public Transform Owner => owner;
    public PotionPhaseSpec PhaseSpec => phaseSpec;
    public int SourceBombId => sourceBombId;
    public int PhaseIndex => phaseIndex;
    public ProjectilePatternType PatternType => patternType;
    public float LineAngleDeg => lineAngleDeg;

    public void Init(
        Transform ownerTransform,
        PotionPhaseSpec spec,
        Vector2 direction,
        float speed,
        float lifeSeconds,
        float rotateDegPerSec = 0f,
        Sprite sprite = null,
        bool allowFallbackSprite = true,
        bool useLocalSpaceMovement = false,
        int sourceBombInstanceId = 0,
        int sourcePhaseIndex = 0,
        ProjectilePatternType sourcePatternType = ProjectilePatternType.Fireworks,
        float sourceLineAngleDeg = 0f)
    {
        owner = ownerTransform;
        phaseSpec = spec;
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        moveSpeed = Mathf.Max(0f, speed);
        lifetime = Mathf.Max(0.1f, lifeSeconds);
        rotationSpeedDegPerSec = rotateDegPerSec;
        moveInLocalSpace = useLocalSpaceMovement;

        sourceBombId = sourceBombInstanceId;
        phaseIndex = sourcePhaseIndex;
        patternType = sourcePatternType;
        lineAngleDeg = sourceLineAngleDeg;
        useTornadoOrbit = false;
        movementStartDelay = 0f;
        tornadoLinearDuration = 0f;
        tornadoOrbitAngularSpeedDegPerSec = 0f;
        tornadoOrbitCenter = null;
        tornadoOrbitOffset = Vector3.zero;
        tornadoOrbitAngleDeg = 0f;
        tornadoOrbitStarted = false;
        offscreenDamageDisabled = false;

        hitCollider = GetComponent<CircleCollider2D>();
        hitCollider.isTrigger = true;
        hitCollider.radius = 0.12f;
        contactDamageEnabled = true;
        cachedCamera = Camera.main;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.enabled = true;
        }
        else if (allowFallbackSprite)
        {
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = GetFallbackSprite();
            renderer.color = new Color(0.3f, 0.9f, 1f, 0.75f);
            renderer.enabled = true;
        }

        EnsureVisibleRenderer();
        ApplySortingToRenderers();

        initialized = true;
        lived = 0f;
    }

    public void ConfigureTornadoOrbit(Transform orbitCenter, float orbitStartDelaySeconds, float orbitAngularSpeedDegPerSecond)
    {
        if (orbitCenter == null)
        {
            useTornadoOrbit = false;
            return;
        }

        useTornadoOrbit = true;
        tornadoOrbitCenter = orbitCenter;
        tornadoLinearDuration = Mathf.Max(0f, orbitStartDelaySeconds);
        tornadoOrbitAngularSpeedDegPerSec = orbitAngularSpeedDegPerSecond;
        tornadoOrbitStarted = false;
    }

    public void SetMovementStartDelay(float delaySeconds)
    {
        movementStartDelay = Mathf.Max(0f, delaySeconds);
        contactDamageEnabled = movementStartDelay <= 0f;
    }

    private void Update()
    {
        if (!initialized)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        lived += dt;
        if (lived >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (IsOffscreen())
        {
            contactDamageEnabled = false;
            offscreenDamageDisabled = true;
            if (patternType != ProjectilePatternType.AfterimageBomb)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (lived < movementStartDelay)
        {
            return;
        }

        if (!contactDamageEnabled && !offscreenDamageDisabled)
        {
            contactDamageEnabled = true;
        }

        if (Mathf.Abs(rotationSpeedDegPerSec) > 0.01f)
        {
            moveDirection = Quaternion.Euler(0f, 0f, -rotationSpeedDegPerSec * dt) * moveDirection;
            moveDirection.Normalize();
        }

        if (useTornadoOrbit)
        {
            if (!tornadoOrbitStarted && lived >= tornadoLinearDuration)
            {
                BeginTornadoOrbit();
            }

            if (tornadoOrbitStarted)
            {
                UpdateTornadoOrbit(dt);
                return;
            }
        }

        if (moveInLocalSpace && transform.parent != null)
        {
            transform.localPosition += (Vector3)(moveDirection * moveSpeed * dt);
        }
        else
        {
            transform.position += (Vector3)(moveDirection * moveSpeed * dt);
        }
    }

    private void BeginTornadoOrbit()
    {
        if (tornadoOrbitCenter == null)
        {
            useTornadoOrbit = false;
            return;
        }

        tornadoOrbitStarted = true;
        tornadoOrbitOffset = transform.position - tornadoOrbitCenter.position;
        if (tornadoOrbitOffset.sqrMagnitude <= 0.0001f)
        {
            tornadoOrbitOffset = Vector3.up * 0.01f;
        }

        tornadoOrbitAngleDeg = Mathf.Atan2(tornadoOrbitOffset.y, tornadoOrbitOffset.x) * Mathf.Rad2Deg;
    }

    private void UpdateTornadoOrbit(float dt)
    {
        if (tornadoOrbitCenter == null)
        {
            Destroy(gameObject);
            return;
        }

        float radius = tornadoOrbitOffset.magnitude;
        tornadoOrbitAngleDeg -= tornadoOrbitAngularSpeedDegPerSec * dt;
        float radians = tornadoOrbitAngleDeg * Mathf.Deg2Rad;
        Vector3 center = tornadoOrbitCenter.position;
        Vector3 nextOffset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * radius;
        transform.position = center + nextOffset;
        tornadoOrbitOffset = nextOffset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHandleTrigger(other);
    }

    private void TryHandleTrigger(Collider2D other)
    {
        if (!initialized || other == null || !contactDamageEnabled) return;

        bool consumedByCombat = PotionHitResolver.TryResolveHit(this, other);
        bool consumedByEnvironment = !consumedByCombat && PotionHitResolver.TryResolveEnvironmentHit(this, other);

        if (!consumedByCombat && !consumedByEnvironment)
        {
            return;
        }

        Destroy(gameObject);
    }

    private bool IsOffscreen()
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return false;
        }

        Vector3 viewport = cachedCamera.WorldToViewportPoint(transform.position);
        if (viewport.z < 0f)
        {
            return true;
        }

        return viewport.x < -offscreenMargin
            || viewport.x > 1f + offscreenMargin
            || viewport.y < -offscreenMargin
            || viewport.y > 1f + offscreenMargin;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null) return fallbackSprite;

        Texture2D tex = Texture2D.whiteTexture;
        fallbackSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f);
        return fallbackSprite;
    }

    private void ApplySortingToRenderers()
    {
        string resolvedSortingLayerName = ResolveSortingLayerName(sortingLayerName);
        int sortingLayerId = SortingLayer.NameToID(resolvedSortingLayerName);
        bool hasValidLayer = sortingLayerId != 0 || resolvedSortingLayerName == "Default";

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null) continue;
            if (hasValidLayer)
            {
                sr.sortingLayerID = sortingLayerId;
                sr.sortingLayerName = resolvedSortingLayerName;
            }
            sr.sortingOrder = sortingOrder;
        }

        ParticleSystemRenderer[] particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer psr = particleRenderers[i];
            if (psr == null) continue;
            if (hasValidLayer)
            {
                psr.sortingLayerID = sortingLayerId;
                psr.sortingLayerName = resolvedSortingLayerName;
            }
            psr.sortingOrder = sortingOrder;
        }
    }

    private void EnsureVisibleRenderer()
    {
        if (HasAnyEnabledRenderer())
        {
            return;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = GetFallbackSprite();
        renderer.color = new Color(0.3f, 0.9f, 1f, 0.75f);
        renderer.enabled = true;

        if (logRenderWarnings)
        {
            Debug.LogWarning($"[PotionProjectile] No enabled renderers found on '{name}'. Applied fallback sprite.", this);
        }
    }

    private bool HasAnyEnabledRenderer()
    {
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        ParticleSystemRenderer[] particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer renderer = particleRenderers[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private string ResolveSortingLayerName(string requestedLayer)
    {
        string requested = string.IsNullOrWhiteSpace(requestedLayer) ? "Default" : requestedLayer;
        int requestedId = SortingLayer.NameToID(requested);
        if (requestedId != 0 || requested == "Default")
        {
            return requested;
        }

        if (logRenderWarnings)
        {
            Debug.LogWarning($"[PotionProjectile] Sorting layer '{requested}' not found. Falling back to 'Default'.", this);
        }

        return "Default";
    }
}
