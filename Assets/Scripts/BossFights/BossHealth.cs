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

    public void ResetToFull()
    {
        isDead = false;
        isInvulnerable = false;
        currentHP = maxHP;
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
            // QS-78: 같은 방에 살아있는 보스가 남아 있으면(sum_3 듀얼 보스) 전투를 유지한다.
            // 이 보스는 위에서 isDead=true가 되어 검사에서 자동 제외된다.
            if (HasAliveBossInRoom(transform.root))
            {
                Debug.Log($"[BOSS] {bossName} 처치 — 같은 방에 생존 보스가 남아 전투를 유지합니다.");
            }
            else
            {
                BossManager.Instance.EndBossBattle();
            }
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 해당 방(room root) 안에 살아있는 활성 보스가 남아 있는지 검사한다.
    /// 방 프리팹은 부모 없이 씬 루트에 Instantiate되므로 transform.root가 방 인스턴스다.
    /// 비활성 보스(미개전 방의 대기 보스)는 FindObjectsByType 기본 동작으로 제외된다.
    /// </summary>
    public static bool HasAliveBossInRoom(Transform roomRoot)
    {
        if (roomRoot == null) return false;

        BossHealth[] bosses = FindObjectsByType<BossHealth>(FindObjectsSortMode.None);
        foreach (BossHealth boss in bosses)
        {
            if (boss == null || boss.isDead || boss.currentHP <= 0) continue;
            if (boss.transform.root != roomRoot) continue;
            return true;
        }

        return false;
    }
}
