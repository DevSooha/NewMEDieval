using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [Header("Boss Stats")]
    public string bossName = " ";
    public int maxHP;
    public int currentHP;

    public ElementType currentElement = ElementType.Fire;

    private bool isDead = false;

    private IBossDamageModifier damageModifier;
    private IBossPhaseHandler phaseHandler;

    void Awake()
    {
        currentHP = maxHP;

        damageModifier = GetComponent<IBossDamageModifier>();
        if (damageModifier == null) damageModifier = GetComponentInParent<IBossDamageModifier>();

        phaseHandler = GetComponent<IBossPhaseHandler>();
        if (phaseHandler == null) phaseHandler = GetComponentInParent<IBossPhaseHandler>();
    }

    public void TakeDamage(float damage, ElementType attackType)
    {
        TakeDamage(Mathf.RoundToInt(damage), attackType);
    }

    public void TakeDamage(int damage, ElementType attackType)
    {
        if (isDead) return;

        float multiplier = ElementManager.GetDamageMultiplier(attackType, currentElement);

        if (damageModifier != null)
        {
            multiplier = damageModifier.ModifyDamageMultiplier(attackType, multiplier);
        }

        int finalDamage = Mathf.RoundToInt(damage * multiplier);
        currentHP -= finalDamage;

        phaseHandler?.OnBossHpChanged(currentHP, maxHP);

        Debug.Log($"[BOSS] {bossName} HP: {currentHP} (Dmg: {finalDamage}, Type: {attackType})");

        if (currentHP <= 0) Die();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHP = 0;

        Debug.Log($"[BOSS] {bossName} Ã³Ä¡µÊ.");

        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }

        Destroy(gameObject);
    }
}
