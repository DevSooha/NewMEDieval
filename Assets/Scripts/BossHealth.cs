using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [Header("Boss Stats")]
    public string bossName = "세 마녀";
    public int maxHP = 12000;
    public int currentHP;

    public ElementType currentElement = ElementType.Fire;

    private bool isDead = false;

    // ★ 수정 1: 직접 연결할 변수 선언
    private ThreeWitchCombat mainBossScript;

    void Awake()
    {
        currentHP = maxHP;

        // ★ 수정 2: 내 오브젝트나 부모에게서 스크립트를 직접 찾아 연결 (싱글톤보다 안전함)
        mainBossScript = GetComponent<ThreeWitchCombat>();
        if (mainBossScript == null)
        {
            mainBossScript = GetComponentInParent<ThreeWitchCombat>();
        }
    }

    public void TakeDamage(int damage, ElementType attackType)
    {
        if (isDead) return;

        float multiplier = ElementManager.GetDamageMultiplier(attackType, currentElement);
        int finalDamage = Mathf.RoundToInt(damage * multiplier);

        currentHP -= finalDamage;

        // 페이즈 계산
        int nextPhase = 1;
        if (currentHP > 8000) nextPhase = 1;
        else if (currentHP > 4000) nextPhase = 2;
        else if (currentHP > 0) nextPhase = 3;

        // ★ 수정 3: 안전하게 연결된 스크립트 사용
        if (mainBossScript != null)
        {
            if (mainBossScript.phase != nextPhase)
            {
                Debug.Log($"[BOSS] 페이즈 변경! {mainBossScript.phase} -> {nextPhase}");
                mainBossScript.phase = nextPhase;
            }
        }
        else
        {
            // 만약 못 찾았으면 임시로 로그만 띄우고 게임은 안 멈추게 함
            Debug.LogWarning("BossHealth: ThreeWitchCombat 스크립트를 찾을 수 없습니다!");
        }

        Debug.Log($"[BOSS] 남은 체력: {currentHP} (페이즈: {nextPhase})");

        if (currentHP <= 0) Die();
    }

    void Die()
    {
        isDead = true;
        currentHP = 0;
        if (BossManager.Instance != null) BossManager.Instance.EndBossBattle();
        Destroy(gameObject);
    }
}