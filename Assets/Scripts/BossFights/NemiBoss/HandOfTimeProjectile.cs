using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class HandOfTimeProjectile : MonoBehaviour
{
    private enum State { Waiting, Fired }
    private State state = State.Waiting;

    [Header("Render (temp)")]
    [SerializeField] private SpriteRenderer sr;
    private Coroutine fadeRoutine;

    private Rigidbody2D rb;
    private Collider2D col;

    private Vector2 moveDir;
    private float speed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        col.isTrigger = true;
        col.enabled = false;                 // ✅ 대기 중 충돌 OFF
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        SetAlpha(0f);                        // ✅ 처음엔 안 보이게
    }

    public void BeginFadeIn(float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(duration));
    }

    private IEnumerator FadeRoutine(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(t / duration));
            yield return null;
        }
        SetAlpha(1f);
        fadeRoutine = null;
    }

    public void BeginFire(Vector3 targetWorld, float speedWorldPerSec)
    {
        Vector2 dir = (Vector2)(targetWorld - transform.position);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        moveDir = dir.normalized;
        speed = speedWorldPerSec;

        state = State.Fired;
        col.enabled = true;                  // ✅ 발사 시점부터 충돌 ON
    }

    private void Update()
    {
        if (state != State.Fired) return;
        rb.MovePosition((Vector2)transform.position + moveDir * speed * Time.deltaTime);
    }

    private void SetAlpha(float a)
    {
        if (sr == null) return;
        var c = sr.color;
        c.a = a;
        sr.color = c;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state != State.Fired) return; // ✅ 대기 중 무시

        if (!other.CompareTag("Player")) return;
        var player = other.GetComponent<Player>();
        if (player == null) return;

        // 데미지/무적 함수명 아직 미확정이라 SendMessage 버전 유지
        player.gameObject.SendMessage("TakeDamage", 1, SendMessageOptions.DontRequireReceiver);

        player.KnockBack(transform, 12f, 0.2f); // force는 2타일 정도 나오게 튜닝

        player.gameObject.SendMessage("SetInvincible", 0.3f, SendMessageOptions.DontRequireReceiver);
    }
}