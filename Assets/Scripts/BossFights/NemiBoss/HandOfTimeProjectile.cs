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

        // Rigidbody 설정
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Collider 설정
        col.isTrigger = true;
        col.enabled = false;

        cam = Camera.main;
        bornTime = Time.time;
    }
    
    void Start()
    {
        Debug.Log("Projectile spawned at " + transform.position);
    }

    private void OnDestroy()
    {
        Debug.Log("Projectile destroyed");
    }

    /// <summary>
    /// 발사 시작
    /// </summary>
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

        // spawn 직후에는 offscreen 체크 안함
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

        var player = other.GetComponent<Player>();
        if (player == null) return;

        player.gameObject.SendMessage("TakeDamage", 1, SendMessageOptions.DontRequireReceiver);
        player.KnockBack(transform, 12f, 0.2f);
        player.gameObject.SendMessage("SetInvincible", 0.3f, SendMessageOptions.DontRequireReceiver);
    }
}