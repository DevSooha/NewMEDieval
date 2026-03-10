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

    private Renderer[] renderers;
    private ParticleSystem[] particles;

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

        renderers = GetComponentsInChildren<Renderer>(true);
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void Start()
    {
        Debug.Log($"[Projectile] Spawned at {transform.position}");

        Debug.Log($"[Projectile] Scale {transform.localScale}");
        Debug.Log($"[Projectile] Rotation {transform.rotation.eulerAngles}");

        if (renderers.Length == 0)
        {
            Debug.LogError("[Projectile] Renderer 없음");
        }
        else
        {
            foreach (var r in renderers)
            {
                Debug.Log($"[Renderer] {r.name} layer={r.sortingLayerName} order={r.sortingOrder}");
            }
        }

        if (particles.Length == 0)
        {
            Debug.LogWarning("[Projectile] ParticleSystem 없음");
        }
        else
        {
            foreach (var ps in particles)
            {
                Debug.Log($"[Particle] {ps.name} playing={ps.isPlaying}");
            }
        }

        CheckCameraView();
    }

    private void OnDestroy()
    {
        Debug.Log("[Projectile] destroyed");
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

        Debug.Log($"[Projectile] BeginFire dir={moveDir} speed={speed}");
    }

    private void FixedUpdate()
    {
        if (state != State.Fired) return;

        rb.MovePosition(rb.position + moveDir * speed * Time.fixedDeltaTime);

        Debug.Log($"[Projectile] moving pos={rb.position}");

        if (Time.time - bornTime > maxLifeTime)
        {
            Debug.Log("[Projectile] Destroyed by lifetime");
            Destroy(gameObject);
            return;
        }

        if (Time.time - bornTime < spawnProtection)
            return;

        if (IsOffscreen())
        {
            Debug.Log("[Projectile] Destroyed by offscreen");
            Destroy(gameObject);
        }
    }

    private bool IsOffscreen()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);

        Debug.Log($"[Viewport] {vp}");

        if (vp.z < 0f)
        {
            Debug.Log("[Projectile] Behind camera");
            return true;
        }

        return
            vp.x < -offscreenMargin ||
            vp.x > 1 + offscreenMargin ||
            vp.y < -offscreenMargin ||
            vp.y > 1 + offscreenMargin;
    }

    private void CheckCameraView()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);

        bool visible =
            vp.x >= 0 && vp.x <= 1 &&
            vp.y >= 0 && vp.y <= 1 &&
            vp.z > 0;

        Debug.Log($"[CameraCheck] viewport={vp} visible={visible}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state != State.Fired) return;

        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<Player>();
        if (player == null) return;

        Debug.Log("[Projectile] Player hit");

        player.gameObject.SendMessage("TakeDamage", 1, SendMessageOptions.DontRequireReceiver);
        player.KnockBack(transform, 12f, 0.2f);
        player.gameObject.SendMessage("SetInvincible", 0.3f, SendMessageOptions.DontRequireReceiver);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
}