using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class HandOfTimeProjectile : MonoBehaviour
{
    public enum Axis { Horizontal, Vertical }

    private enum State
    {
        Waiting,
        Fired
    }

    private State state = State.Waiting;

    [Header("Lifetime")]
    [SerializeField] private float maxLifeTime = 10f;

    [Header("Offscreen Despawn")]
    [SerializeField] private float offscreenMargin = 0.2f;

    [Header("Damage")]
    [SerializeField] private int damage = 1;

    private Rigidbody2D rb;
    private Collider2D col;

    private Vector2 moveDir;
    private float speed;

    private float bornTime;
    private Camera cam;

    private float spawnProtection = 0.5f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col.isTrigger = true;
        col.enabled = false;

        cam = Camera.main;
        bornTime = Time.time;
    }

    public void BeginFire(Vector3 targetWorld, float speedWorldPerSec, Axis axis)
    {
        rb.position = transform.position;

        Vector2 delta = (Vector2)(targetWorld - transform.position);

        if (axis == Axis.Horizontal)
        {
            float sx = Mathf.Sign(Mathf.Abs(delta.x) < 0.0001f ? 1f : delta.x);
            moveDir = new Vector2(sx, 0f);
        }
        else
        {
            float sy = Mathf.Sign(Mathf.Abs(delta.y) < 0.0001f ? 1f : delta.y);
            moveDir = new Vector2(0f, sy);
        }

        speed = speedWorldPerSec;

        state = State.Fired;
        col.enabled = true;
    }

    private void FixedUpdate()
    {
        if (state != State.Fired) return;

        rb.MovePosition(rb.position + moveDir * speed * Time.fixedDeltaTime);

        if (Time.time - bornTime > maxLifeTime)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time - bornTime < spawnProtection)
            return;

        if (IsOffscreen())
        {
            Destroy(gameObject);
        }
    }

    private bool IsOffscreen()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);

        if (vp.z < 0f) return true;

        return
            vp.x < -offscreenMargin ||
            vp.x > 1 + offscreenMargin ||
            vp.y < -offscreenMargin ||
            vp.y > 1 + offscreenMargin;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state != State.Fired) return;
        if (!other.CompareTag("Player")) return;

        BossHitResolver.TryApplyBossHit(other, damage, transform.position);
    }
}
