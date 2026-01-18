using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public int HP;
    public int maxHP;
    public TMP_Text healthBar;

    void Start()
    {
        HP = maxHP;
        UpdateHealthBar();
    }

    // 필요할 때만 호출
    public void TakeDamage(int amount)
    {
        Debug.Log($"체력: {HP}");
        HP -= amount;
        if (HP > maxHP)
            HP = maxHP;

        if (HP < 0)
            HP = 0;

        UpdateHealthBar();

        if (HP == 0)
        {
            Destroy(gameObject);
        }
    }

    // ★ [추가된 부분] 체력 회복 함수
    public void Heal(int amount)
    {
        HP += amount;

        // 최대 체력을 넘지 않게 고정
        if (HP > maxHP)
        {
            HP = maxHP;
        }

        UpdateHealthBar();
        Debug.Log($"체력 회복! 현재 HP: {HP}");
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
            healthBar.text = "HP: " + HP + " / " + maxHP;
    }
}
