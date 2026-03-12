using UnityEngine;

[DisallowMultipleComponent]
public class BriefCandleRay : BossProjectile
{
    [Header("Beam Settings")]
    [SerializeField] private float extendSpeed = 18f;
    [SerializeField] private float maxLength = 10f;
    [SerializeField] private float colliderWidth = 1.42f;
    [SerializeField] private float initialColliderLength = 0.1f;

    private float currentLength;
    private BoxCollider2D boxCollider;
    private ParticleSystem[] particleSystems;

    private void Awake()
    {
        EnsureCollider();
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    public override void Setup(ElementType element)
    {
        base.Setup(ElementType.Light);

        EnsureCollider();
        currentLength = 0f;
        boxCollider.size = new Vector2(colliderWidth, initialColliderLength);
        boxCollider.offset = Vector2.zero;

        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null) continue;

            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }
    }

    protected override void Update()
    {
        if (boxCollider == null) return;

        if (currentLength < maxLength)
        {
            currentLength += extendSpeed * Time.deltaTime;
            currentLength = Mathf.Min(currentLength, maxLength);

            boxCollider.size = new Vector2(colliderWidth, currentLength);
            boxCollider.offset = new Vector2(0f, -currentLength / 2f);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Boss")) return;

        if (other.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(
                other,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration,
                applyKnockbackOnHit,
                -(Vector2)transform.up
            );
        }
        else if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
        {
            maxLength = currentLength;
        }
    }

    private void EnsureCollider()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        boxCollider.isTrigger = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float previewLength = Application.isPlaying ? currentLength : maxLength;
        previewLength = Mathf.Max(previewLength, initialColliderLength);
        float previewWidth = colliderWidth;

        Vector3 center = transform.position + (transform.up * (-previewLength * 0.5f));
        Vector3 size = new Vector3(previewWidth, previewLength, 0.05f);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.85f);
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(-previewWidth * 0.5f, 0f, 0f),
            new Vector3(previewWidth * 0.5f, 0f, 0f));
        Gizmos.DrawLine(
            new Vector3(-previewWidth * 0.5f, -previewLength, 0f),
            new Vector3(previewWidth * 0.5f, -previewLength, 0f));

        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
