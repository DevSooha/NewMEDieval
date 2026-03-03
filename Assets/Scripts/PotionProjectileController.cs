using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class PotionProjectileController : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "EnemyBullet";
    [SerializeField] private int sortingOrder = 50;
    private static Sprite fallbackSprite;

    private Vector2 moveDirection;
    private float moveSpeed;
    private float lived;
    private float rotationSpeedDegPerSec;
    private bool moveInLocalSpace;

    private Transform owner;
    private PotionPhaseSpec phaseSpec;
    private bool initialized;

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

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.12f;

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

        ApplySortingToRenderers();

        initialized = true;
        lived = 0f;
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

        if (Mathf.Abs(rotationSpeedDegPerSec) > 0.01f)
        {
            moveDirection = Quaternion.Euler(0f, 0f, -rotationSpeedDegPerSec * dt) * moveDirection;
            moveDirection.Normalize();
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!initialized || other == null) return;

        bool consumedByCombat = PotionHitResolver.TryResolveHit(this, other);
        bool consumedByEnvironment = !consumedByCombat && PotionHitResolver.TryResolveEnvironmentHit(this, other);

        if (!consumedByCombat && !consumedByEnvironment)
        {
            return;
        }

        Destroy(gameObject);
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
        int sortingLayerId = SortingLayer.NameToID(sortingLayerName);
        bool hasValidLayer = sortingLayerId != 0 || sortingLayerName == "Default";

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null) continue;
            if (hasValidLayer)
            {
                sr.sortingLayerID = sortingLayerId;
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
            }
            psr.sortingOrder = sortingOrder;
        }
    }
}
