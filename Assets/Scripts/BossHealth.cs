using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [Header("Boss Stats")]
    public string bossName = "세 마녀";
    public int maxHP = 600;
    public int currentHP;

    public ElementType currentElement = ElementType.Fire;

    private bool isDead = false;

    void Start()
    {
        currentHP = maxHP;
    }

    // 속성 정보를 함께 받는 데미지 함수
    public void TakeDamage(int damage, ElementType attackType)
    {
        if (isDead) return;

        float multiplier = ElementManager.GetDamageMultiplier(attackType, currentElement);
        int finalDamage = Mathf.RoundToInt(damage * multiplier);

        currentHP -= finalDamage;

        // 로그로 상성 결과 확인
        string hitType = (multiplier > 1.0f) ? "효과가 굉장했다! (x2)" : (multiplier < 1.0f) ? "효과가 별로다... (x0.5)" : "보통 (x1)";
        Debug.Log($"[BOSS] {hitType} 데미지: {finalDamage} (원래: {damage}) / 남은 체력: {currentHP}");

        if (currentHP <= 0) Die();
    }

    void Die()
    {
        isDead = true;
        currentHP = 0;
        Debug.Log($"[BOSS] {bossName} 사망!");
        if (BossManager.Instance != null) BossManager.Instance.EndBossBattle();
        Destroy(gameObject);
    }
}