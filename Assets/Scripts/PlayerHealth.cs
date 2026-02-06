using UnityEngine;
using System; // Action�� ����ϱ� ���� �ʿ�

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHP = 6;
    private int currentHP;

    public static Action<int, int> OnHealthChanged;
    public static Action OnPlayerDeath;

    void Start()
    {
        currentHP = maxHP;
        NotifyHealthChanged();
    }



    // 2. ������ ����
    public void TakeDamage(int amount)
    {
        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        NotifyHealthChanged(); // ü���� �������� �˸�

        if (currentHP <= 0) Die();
    }

    public void Resurrect()
    {
        currentHP = maxHP; // 1. �����ͻ� ü�� 100% ����

        // 2. �߿�: UI���׵� "ü�� �� á��"�̶�� �˸� (�̰� �� �ϸ� UI�� ������ 0ĭ���� ����)
        NotifyHealthChanged();
    }

    // 3. ȸ�� ����
    public void Heal(int amount)
    {
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        NotifyHealthChanged();
    }

    private void NotifyHealthChanged()
    {
        // ������(UI ��)�� �ִٸ� �̺�Ʈ�� �߻���Ŵ
        OnHealthChanged?.Invoke(currentHP, maxHP);
    }

    private void Die()
    {
        Debug.Log("�÷��̾� ���!");

        // 2. ��� ����� �˸�
        OnPlayerDeath?.Invoke();

        gameObject.SetActive(false);
    }
}