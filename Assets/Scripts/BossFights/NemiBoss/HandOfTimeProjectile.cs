using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HandOfTimeProjectile : MonoBehaviour
{
    public enum Axis { Horizontal, Vertical }
    private enum State { Waiting, Fired }
    private State state = State.Waiting;

    [Header("Render")]
    [SerializeField] private SpriteRenderer sr;

    [Header("Lifetime / Despawn")]
    [Tooltip("카메라 화면 밖으로 얼마나 벗어나면 파괴할지(월드 단위)")]
    [SerializeField] private float offscreenMargin = 2f;

    [Tooltip("안전장치: 너무 오래 살아있으면 파괴")]
    [SerializeField] private float maxLifeTime = 10f;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;   // Sprite+Collider가 붙어있는 자식(Visual)
    [SerializeField] private bool spriteIsHorizontalByDefault = true; // ✅ 1x3은 true로 두면 됨
    
    private Coroutine fadeRoutine;

    private Rigidbody2D rb;
    private Collider2D col;

    private Vector2 moveDir;
    private float speed;

    private Camera cam;
    private float bornTime;

    private void Awake()
    {
        // 1) Rigidbody2D는 루트에 있어야 함
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("[HandOfTimeProjectile] Rigidbody2D missing on root.");
            enabled = false;
            return;
        }

        // 2) SpriteRenderer / Collider2D는 자식(Visual)에서 찾는다
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>(true);
        col = GetComponentInChildren<Collider2D>(true);

        if (sr == null)
        {
            Debug.LogError("[HandOfTimeProjectile] SpriteRenderer not found in children.");
            enabled = false;
            return;
        }

        if (col == null)
        {
            Debug.LogError("[HandOfTimeProjectile] Collider2D not found in children.");
            enabled = false;
            return;
        }

        // 3) 콜라이더는 트리거 + 기본 비활성(페이드인 동안 충돌 OFF)
        col.isTrigger = true;
        col.enabled = false;

        // 4) Rigidbody2D 세팅(패턴 탄막 = 키네마틱 이동)
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 5) 처음엔 투명
        SetAlpha(0f);

        // 6) 파괴 판단용
        cam = Camera.main;
        bornTime = Time.time;
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

    /// <summary>
    /// axis에 따라 방향을 "수평/수직"으로만 스냅해서 발사한다.
    /// </summary>
    public void BeginFire(Vector3 targetWorld, float speedWorldPerSec, Axis axis)
    {
        // kinematic에서도 혹시 모를 첫 프레임 튐 방지
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
        col.enabled = true; // 발사 시점부터 충돌 ON
        
        // ✅ 1x3이 "가로 기본"이면: Vertical일 때 90도, Horizontal일 때 0도
        if (visualRoot != null)
        {
            float z = 0f;

            if (spriteIsHorizontalByDefault)
                z = (axis == Axis.Vertical) ? 90f : 0f;
            else
                z = (axis == Axis.Horizontal) ? 90f : 0f; // (세로 기본 스프라이트일 때)

            visualRoot.localRotation = Quaternion.Euler(0f, 0f, z);
        }
    }

    private void FixedUpdate()
    {
        if (state != State.Fired) return;

        rb.MovePosition(rb.position + moveDir * speed * Time.fixedDeltaTime);

        // 오래 살면 제거(안전장치)
        if (Time.time - bornTime > maxLifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // 화면 밖이면 제거(맵 끝까지 가다가 결국 카메라 밖으로 빠짐)
        if (IsOffscreen())
        {
            Destroy(gameObject);
            return;
        }
    }

    private bool IsOffscreen()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false; // 카메라 없으면 파괴 판단 못함

        Vector3 vp = cam.WorldToViewportPoint(transform.position);

        // vp.x/y가 0~1 범위 밖이면 화면 밖
        // margin은 viewport가 아니라 월드 기준으로 쓰고 싶으면 별도 계산이 필요하지만,
        // 여기선 단순/안정적으로 viewport 기반 + z 체크로 충분함.
        if (vp.z < 0f) return true;

        return (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f);
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
        if (state != State.Fired) return; // 대기 중 무시

        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<Player>();
        if (player == null) return;

        player.gameObject.SendMessage("TakeDamage", 1, SendMessageOptions.DontRequireReceiver);
        player.KnockBack(transform, 12f, 0.2f);
        player.gameObject.SendMessage("SetInvincible", 0.3f, SendMessageOptions.DontRequireReceiver);

        // 맞으면 사라지게 할지 여부는 선택
        // Destroy(gameObject);
    }
}