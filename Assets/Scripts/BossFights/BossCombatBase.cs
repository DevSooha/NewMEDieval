using UnityEngine;

// �� ��ũ��Ʈ�� �ƹ����� ������ ������. �׳� ���ϸ� ������ �˴ϴ�.
public abstract class BossCombatBase : MonoBehaviour
{
    
    [Header("Default Knockback Settings")]
    [SerializeField] protected float defaultKnockbackForce = 8f;
    [SerializeField] protected float defaultKnockbackStunTime = 0.2f;
    
    public abstract void StartBattle();
    
    protected void Knockback(Player player, Transform sender, float? forceOverride = null, float? stunOverride = null)
    {
        if (player == null || sender == null) return;

        float force = forceOverride ?? defaultKnockbackForce;
        float stun  = stunOverride  ?? defaultKnockbackStunTime;

        player.KnockBack(sender, force, stun);
    }


}


public interface IBossDamageModifier
{
    float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier);
}

public interface IBossPhaseHandler
{
    void OnBossHpChanged(int currentHp, int maxHp);
}
