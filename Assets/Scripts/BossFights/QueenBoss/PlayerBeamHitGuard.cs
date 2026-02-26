using System.Collections;
using UnityEngine;

public class PlayerBeamHitGuard : MonoBehaviour
{
    [Header("Invincibility (seconds)")]
    [SerializeField] private float invincibleSeconds = 0.8f; // ✅ n초 인스펙터에서 조절

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 8f;   // 임펄스 크기(필요시 조절)
    [SerializeField] private float knockbackStunTime = 0.2f; // ✅ 넉백 시간 0.2s

    [Header("Damage")]
    [SerializeField] private int damage = 1; // 여기선 "맞으면 1회"만. 실제 HP는 다른 시스템이면 거기 연결

    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    private bool isInvincible;
    private Coroutine invincibleCo;
    private Player player; // 기존 Player.cs

    private void Awake()
    {
        player = GetComponent<Player>();
        if (player == null)
            Debug.LogError("[PlayerBeamHitGuard] Player component not found on same GameObject.");
    }

    /// <summary>
    /// 빔이 플레이어를 때릴 때 호출. 무적이면 false 반환(누적X).
    /// </summary>
    public bool TryHitFromBeam(Transform sender)
    {
        if (player == null) return false;

        // ✅ 무적 중이면 아무 것도 하지 않음(데미지/넉백/무적시간 연장 X)
        if (isInvincible)
        {
            if (verboseLog) Debug.Log("[PlayerBeamHitGuard] ignored (invincible)");
            return false;
        }

        // ✅ 데미지 처리: Player 내부를 못 건드리니, 여기서는 이벤트/로그만
        // 네가 나중에 HP 시스템 붙이면 여기서 연결하면 됨.
        if (verboseLog) Debug.Log($"[PlayerBeamHitGuard] HIT! damage={damage}");

        // ✅ 기존 Player의 넉백 함수 사용
        player.KnockBack(sender, knockbackForce, knockbackStunTime);

        // ✅ 무적 시작(누적 X)
        if (invincibleCo != null) StopCoroutine(invincibleCo);
        invincibleCo = StartCoroutine(InvincibleRoutine(invincibleSeconds));

        return true;
    }

    private IEnumerator InvincibleRoutine(float t)
    {
        isInvincible = true;
        yield return new WaitForSeconds(t);
        isInvincible = false;
        invincibleCo = null;
    }
}