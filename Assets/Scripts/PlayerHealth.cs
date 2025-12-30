using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public int HP=10;
    public int maxHP=10;
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

    private void UpdateHealthBar()
    {
        if (healthBar != null)
            healthBar.text = "HP: " + HP + " / " + maxHP;
    }
}
