using UnityEngine;

public enum ElementType
{
    None,
    Fire,
    Water,
    Electric,
    Light,
    Dark,
    Poison
}

public static class ElementManager
{
    // ������(attack)�� �����(defend)�� ���� ���� ���� ���
    public static float GetDamageMultiplier(ElementType attack, ElementType defend)
    {
        if (attack == ElementType.None || defend == ElementType.None) return 1.0f;

        // ���� �Ӽ� = 1��
        if (attack == defend) return 1.0f;

        switch (attack)
        {
            case ElementType.Water:
                if (defend == ElementType.Fire) return 2.0f;
                if (defend == ElementType.Electric) return 0.5f;
                break;
            case ElementType.Fire:
                if (defend == ElementType.Electric) return 2.0f;
                if (defend == ElementType.Water) return 0.5f;
                break;
            case ElementType.Electric:
                if (defend == ElementType.Water) return 2.0f;
                if (defend == ElementType.Fire) return 0.5f;
                break;
            case ElementType.Light:
                if (defend == ElementType.Dark) return 2.0f;
                break;
            case ElementType.Dark:
                if (defend == ElementType.Light) return 2.0f;
                break;
        }

        return 1.0f;
    }

    public static float GetCombinedDamageMultiplier(
        ElementType primaryAttack,
        ElementType subAttack,
        ElementType defenderPrimary)
    {
        float multiplier = GetDamageMultiplier(primaryAttack, defenderPrimary);

        if (subAttack == ElementType.Light && defenderPrimary == ElementType.Dark)
        {
            multiplier *= 2f;
        }
        else if (subAttack == ElementType.Dark && defenderPrimary == ElementType.Light)
        {
            multiplier *= 2f;
        }

        return multiplier;
    }
}
