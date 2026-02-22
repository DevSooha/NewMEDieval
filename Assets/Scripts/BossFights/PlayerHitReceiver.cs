using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHitReceiver : MonoBehaviour
{
    [Header("Hit Settings")]
    [SerializeField] private int maxHP = 5;
    [SerializeField] private float invincibleSeconds = 1.0f;   // ✅ n초 (인스펙터 조절)
    [SerializeField] private float knockbackForce = 8.0f;
    [SerializeField] private float knockbackTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    private int hp;
    private bool isInvincible;
    private Coroutine invincibleCo;

    private Rigidbody2D rb;

    private Coroutine knockbackCo;
    
    private void Awake()
    {
        hp = maxHP;
        rb = GetComponent<Rigidbody2D>(); // 있으면 사용, 없어도 넉백은 Transform로 처리 가능
    }

    /// <summary>
    /// 공격자가 호출하는 함수. 무적 중이면 아무 것도 하지 않음(누적 X).
    /// </summary>
    public bool TryHit(int damage, Vector2 knockbackDir)
    {
        if (isInvincible)
        {
            if (verboseLog) Debug.Log("[PlayerHitReceiver] Hit ignored (invincible).");
            return false;
        }

        // 데미지
        hp = Mathf.Max(0, hp - damage);
        if (verboseLog) Debug.Log($"[PlayerHitReceiver] HIT! damage={damage} hp={hp}");

        // 넉백
        ApplyKnockback(knockbackDir);

        // 무적 시작 (누적 X: 이미 무적이면 return 했으므로 여기선 항상 새로 시작)
        if (invincibleCo != null) StopCoroutine(invincibleCo);
        invincibleCo = StartCoroutine(InvincibleRoutine(invincibleSeconds));

        return true;
    }



    private void ApplyKnockback(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        dir.Normalize();

        if (rb == null)
        {
            transform.position += (Vector3)(dir * 0.2f);
            return;
        }

        // 기존 넉백 코루틴 중단
        if (knockbackCo != null) StopCoroutine(knockbackCo);
        knockbackCo = StartCoroutine(KnockbackRoutine(dir));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir)
    {
        float t = 0f;

        // ✅ knockbackTime 동안 velocity를 강제로 유지 (이동 코드가 덮어써도 효과 보이게)
        while (t < knockbackTime)
        {
            rb.linearVelocity = dir * knockbackForce;
            t += Time.deltaTime;
            yield return null;
        }

        // 마무리로 속도 한번 정리 (원하면 제거 가능)
        rb.linearVelocity = Vector2.zero;
        knockbackCo = null;
    }

    private IEnumerator StopKnockbackAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    private IEnumerator InvincibleRoutine(float t)
    {
        isInvincible = true;
        // TODO: 깜빡임/피격 이펙트 여기서 처리 가능
        yield return new WaitForSeconds(t);
        isInvincible = false;
        invincibleCo = null;
    }
}