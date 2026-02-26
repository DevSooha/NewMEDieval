using UnityEngine;

// 이 스크립트는 아무데도 붙이지 마세요. 그냥 파일만 있으면 됩니다.
public abstract class BossCombatBase : MonoBehaviour
{
    public abstract void StartBattle();
}

public interface IBossDamageModifier
{
    float ModifyDamageMultiplier(ElementType attackType, float baseMultiplier);
}

public interface IBossPhaseHandler
{
    void OnBossHpChanged(int currentHp, int maxHp);
}
