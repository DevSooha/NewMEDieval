using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [Header("Boss Stats")]
    public string bossName = " ";
    public int maxHP;
    public int currentHP;

    public ElementType currentElement = ElementType.Fire;

    private bool isDead = false;
    private bool isInvulnerable = false;

    private IBossDamageModifier damageModifier;
    private IBossPhaseHandler phaseHandler;

    public bool IsInvulnerable => isInvulnerable;
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;

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
        if (isDead || isInvulnerable) return;

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

    public void SetInvulnerable(bool value)
    {
        isInvulnerable = value;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || isDead) return;

        currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
        phaseHandler?.OnBossHpChanged(currentHP, maxHP);
    }

    void Die()
    {
        if (isDead || isInvulnerable) return;
        isDead = true;
        currentHP = 0;

        BossCombatBase combat = GetComponent<BossCombatBase>();
        if (combat == null)
        {
            combat = GetComponentInParent<BossCombatBase>();
        }

        combat?.NotifyBossDefeatedCleanup();

        Debug.Log($"[BOSS] {bossName} 처치됨.");

        if (BossManager.Instance != null)
        {
            BossManager.Instance.EndBossBattle();
        }

        Destroy(gameObject);
    }
}
