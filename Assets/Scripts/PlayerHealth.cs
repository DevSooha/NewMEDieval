using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHP = 6;
    private int currentHP;

    // 1. 핵심: Observer 패턴을 위한 이벤트 선언
    // 현재 체력과 최대 체력을 UI에 알려주기 위해 두 개의 int를 전달합니다.
    public static Action<int, int> OnHealthChanged;
    public static Action OnPlayerDeath;

    void Start()
    {
        currentHP = maxHP;
        // 시작할 때 UI 초기화를 위해 현재 상태를 알림
        NotifyHealthChanged();
    }



    // 2. 데미지 로직
    public void TakeDamage(int amount)
    {
        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        NotifyHealthChanged(); // 체력이 변했음을 알림

        if (currentHP <= 0) Die();
    }

    public void Resurrect()
    {
        currentHP = maxHP; // 1. 데이터상 체력 100% 복구

        // 2. 중요: UI한테도 "체력 꽉 찼음"이라고 알림 (이거 안 하면 UI는 여전히 0칸으로 보임)
        NotifyHealthChanged();
    }

    // 3. 회복 로직
    public void Heal(int amount)
    {
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        NotifyHealthChanged();
    }

    private void NotifyHealthChanged()
    {
        // 구독자(UI 등)가 있다면 이벤트를 발생시킴
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    private void Die()
    {
        Debug.Log("플레이어 사망!");

        // 2. 사망 사실을 알림
        OnPlayerDeath?.Invoke();

        gameObject.SetActive(false);
    }
}