using UnityEngine;

public class BedimmedWall : MonoBehaviour
{
    [Header("Hit Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackDistance = 1f;
    [SerializeField] private float knockbackDuration = 0.2f;

    private Transform targetTransform;
    private Rigidbody2D rb;
    private float moveSpeed = 0f;
    private float boxHalfSize = 0f;
    private bool isActive = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Activate(Transform target, float speed, float safeZoneSize)
    {
        targetTransform = target;
        moveSpeed = speed;
        boxHalfSize = safeZoneSize;
        isActive = true;
        gameObject.SetActive(true);

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (!isActive || targetTransform == null || rb == null) return;

        Vector3 currentTargetPos = targetTransform.position;
        Vector2 newPos = Vector2.MoveTowards(rb.position, currentTargetPos, moveSpeed * Time.deltaTime);

        // Rigidbody와 Transform 둘 다 즉시 동기화
        rb.position = newPos;
        transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

        float diffX = Mathf.Abs(newPos.x - currentTargetPos.x);
        float diffY = Mathf.Abs(newPos.y - currentTargetPos.y);

        if (diffX <= boxHalfSize && diffY <= boxHalfSize)
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        isActive = false;
        targetTransform = null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isActive && collision.CompareTag("Player"))
        {
            BossHitResolver.TryApplyBossHit(
                collision,
                damage,
                transform.position,
                knockbackDistance,
                knockbackDuration
            );
        }
    }

    private void OnDrawGizmos()
    {
        if (targetTransform == null) return;

        Gizmos.color = Color.green;
        Vector3 size = new Vector3(boxHalfSize * 2, boxHalfSize * 2, 1f);
        Gizmos.DrawWireCube(targetTransform.position, size);
    }
}
