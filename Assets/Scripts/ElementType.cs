using UnityEngine;

public enum ElementType
{
    None,
    Fire,       // 불
    Water,      // 물
    Electric    // 전기
}

public static class ElementManager
{
    // 공격자(attack)가 방어자(defend)를 때릴 때의 배율 계산
    public static float GetDamageMultiplier(ElementType attack, ElementType defend)
    {
        if (attack == ElementType.None || defend == ElementType.None) return 1.0f;

        // 같은 속성 = 1배
        if (attack == defend) return 1.0f;

        // 상성 로직 (기획서 약점 기준 역산)
        // 불 마녀의 약점은 물 -> 즉, 물 공격이 불에게 2배
        switch (attack)
        {
            case ElementType.Water:
                if (defend == ElementType.Fire) return 2.0f;     // 물 -> 불 (2배)
                if (defend == ElementType.Electric) return 0.5f; // 물 -> 전기 (0.5배)
                break;
            case ElementType.Fire:
                if (defend == ElementType.Electric) return 2.0f; // 불 -> 전기 (2배)
                if (defend == ElementType.Water) return 0.5f;    // 불 -> 물 (0.5배)
                break;
            case ElementType.Electric:
                if (defend == ElementType.Water) return 2.0f;    // 전기 -> 물 (2배)
                if (defend == ElementType.Fire) return 0.5f;     // 전기 -> 불 (0.5배)
                break;
        }

        return 1.0f; // 그 외
    }
}