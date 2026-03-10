using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class FinalBossBedimmedWallProjectile : BossProjectile
{
    private BoxCollider2D hitCollider;
    private bool isLaunched;
    private Camera cachedCamera;
    private SpriteRenderer fallbackSpriteRenderer;
    private GameObject attachedVisualInstance;

    private void Awake()
    {
        hitCollider = GetComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
        fallbackSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Launch(Vector2 moveDirection, float moveSpeed, Vector2 colliderSize, int hitDamage, ElementType elementType)
    {
        Vector2 direction = moveDirection.sqrMagnitude < 0.0001f ? Vector2.right : moveDirection.normalized;
        SetMoveDirection(direction);
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(1, hitDamage);
        hitCollider.size = new Vector2(Mathf.Max(0.01f, colliderSize.x), Mathf.Max(0.01f, colliderSize.y));
        Setup(elementType);

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }
    }

    public void AttachVisualTemplate(GameObject visualTemplate)
    {
        if (attachedVisualInstance != null)
        {
            Destroy(attachedVisualInstance);
            attachedVisualInstance = null;
        }

        bool hasTemplate = visualTemplate != null;
        if (fallbackSpriteRenderer != null)
        {
            fallbackSpriteRenderer.enabled = !hasTemplate;
        }

        if (!hasTemplate)
        {
            return;
        }

        attachedVisualInstance = Instantiate(
            visualTemplate,
            transform.position,
            visualTemplate.transform.rotation,
            transform
        );
        attachedVisualInstance.name = $"{visualTemplate.name}_RuntimeVisual";
        attachedVisualInstance.transform.localPosition = Vector3.zero;
        attachedVisualInstance.SetActive(true);

        DisablePhysicsOnVisual(attachedVisualInstance);
        RestartVisualEffects(attachedVisualInstance);
    }

    public override void Setup(ElementType element)
    {
        projectileElement = element;
        isLaunched = true;
        CancelInvoke(nameof(DestroyProjectile));
    }

    protected override void Update()
    {
        if (!isLaunched) return;

        base.Update();

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null) return;

        Vector3 viewport = cachedCamera.WorldToViewportPoint(transform.position);
        bool outsideViewport = viewport.x < -0.3f || viewport.x > 1.3f || viewport.y < -0.3f || viewport.y > 1.3f;
        if (outsideViewport)
        {
            DestroyProjectile();
        }
    }

    protected override void DestroyProjectile()
    {
        CancelInvoke(nameof(DestroyProjectile));
        Destroy(gameObject);
    }

    public override void DespawnImmediate()
    {
        isLaunched = false;
        CancelInvoke(nameof(DestroyProjectile));
        Destroy(gameObject);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLaunched) return;
        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration
            );
            return;
        }

        if (other.CompareTag("Grass") && projectileElement == ElementType.Fire)
        {
            Destroy(other.gameObject);
        }
    }

    private static void DisablePhysicsOnVisual(GameObject visualRoot)
    {
        if (visualRoot == null) return;

        BossProjectile[] projectileScripts = visualRoot.GetComponentsInChildren<BossProjectile>(true);
        for (int i = 0; i < projectileScripts.Length; i++)
        {
            if (projectileScripts[i] != null)
            {
                Destroy(projectileScripts[i]);
            }
        }

        Collider2D[] colliders = visualRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                Destroy(colliders[i]);
            }
        }

        Rigidbody2D[] rigidbodies = visualRoot.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null)
            {
                Destroy(rigidbodies[i]);
            }
        }
    }

    private static void RestartVisualEffects(GameObject visualRoot)
    {
        if (visualRoot == null) return;

        ParticleSystem[] particleSystems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null) continue;

            system.Clear(true);
            system.Play(true);
        }
    }
}
