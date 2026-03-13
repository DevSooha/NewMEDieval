using System;
using System.Collections.Generic;
using UnityEngine;

public enum BulletType
{
    Fireworks,
    Bomb,
    Spiral
}

public enum PotionEffect
{
    None,
    HealingBullets,
    Stealth,
    PlayerSpeed2X,
    EnemySpeed2X,
    EnemyStun,
    BulletSpeedDown
}

public enum PotionTemperature
{
    Failure,
    Low,
    Mid,
    High
}

public enum ProjectilePatternType
{
    Fireworks,
    AfterimageBomb,
    Tornado
}

public enum DamageTargetType
{
    EnemyOnly,
    PlayerOnly,
    Both
}

public enum StatusEffectType
{
    None,
    HealPlayerFlat,
    HealEnemyCurrentHpPercent,
    StealthOnly,
    StealthInvulnerable,
    PlayerMoveSpeedMultiplier,
    PlayerStun,
    EnemyMoveSpeedMultiplier,
    EnemyStun,
    PlayerInputReverse,
    PlayerInputDelay,
    BlindBlack,
    BlindWhite,
    EnemyKnockback,
    PoisonDot,
    PlayerRedStateContactBurn,
    PlayerKnockbackImmune
}

[Serializable]
public class StatusEffectSpec
{
    public StatusEffectType effectType = StatusEffectType.None;
    public float duration;
    public float magnitude;
    public float interval;
}

[Serializable]
public class PotionPhaseSpec
{
    [Header("Identity")]
    public string ingredientId;
    public PotionTemperature temperature = PotionTemperature.Mid;

    [Header("Pattern")]
    public ProjectilePatternType patternType = ProjectilePatternType.Fireworks;
    public bool useCardinalDirections = true;
    public float duration = 8f;
    public float initialSpawnDelay = 0f;
    public float fireInterval = 0.25f;
    public float projectileSpeed = 8f;
    public float rotationSpeedDegPerSec = 30f;
    public float orbitAngularSpeedDegPerSec = 30f;

    [Header("Damage/Element")]
    public int baseDamage = 100;
    public ElementType primaryElement = ElementType.Water;
    public ElementType subElement = ElementType.None;
    public DamageTargetType damageTarget = DamageTargetType.Both;

    [Header("Self-hit Rule")]
    public bool healsPlayerOnSelfHit;
    public bool ignoreSelfHitPenalty;

    [Header("Effects")]
    public List<StatusEffectSpec> onPlayerHitEffects = new();
    public List<StatusEffectSpec> onEnemyHitEffects = new();
}

[Serializable]
public class LegacyPatternData
{
    public int damage;
    public int effectTime;
    public int bulletCount;
    public float bulletSpacing;
    public float bulletSpeed;
    public float fireInterval;
    public float totalDuration;
    public int bulletType;
    public int element;
    public int damageTarget;
    public int bulletEffect;
    public int potionEffect;
}

[CreateAssetMenu(fileName = "PotionData", menuName = "Data/Potion")]
public class PotionData : ScriptableObject
{
    [Header("Legacy Flat Fields")]
    public string potionName;
    public int damage1;
    public int damage2;
    public int effectTime;
    public BulletType bulletType1;
    public BulletType bulletType2;
    public PotionEffect potionEffect1;
    public PotionEffect potionEffect2;
    public Element element1;
    public Element element2;
    public Sprite topIMG;
    public Sprite bottomIMG;
    public Sprite frameIMG;

    [Header("Legacy Asset Compatibility")]
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    public GameObject specificPrefab;
    public List<LegacyPatternData> patterns = new();
    public PotionEffect potionEffect;

    [Header("Crafted Runtime")]
    public bool isCraftedRuntimeData;
    public string craftedDisplayName;
    public PotionTemperature temperature = PotionTemperature.Mid;
    public PotionPhaseSpec phase1;
    public PotionPhaseSpec phase2;

    [Header("Inventory")]
    public int maxStack = 99;
    public bool isStackable = true;
    public ItemCategory category = ItemCategory.Potion;

    public string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(craftedDisplayName)) return craftedDisplayName;
        if (!string.IsNullOrWhiteSpace(potionName)) return potionName;
        if (!string.IsNullOrWhiteSpace(itemName)) return itemName;
        return name;
    }

    public PotionPhaseSpec GetPhase(int index)
    {
        if (index == 0 && phase1 != null) return phase1;
        if (index == 1 && phase2 != null) return phase2;

        if (patterns != null && patterns.Count > index)
        {
            return BuildPhaseFromLegacy(patterns[index], index == 0);
        }

        return null;
    }

    private static PotionPhaseSpec BuildPhaseFromLegacy(LegacyPatternData legacy, bool isFirstPhase)
    {
        if (legacy == null) return null;

        PotionPhaseSpec spec = new PotionPhaseSpec
        {
            patternType = legacy.bulletType switch
            {
                0 => ProjectilePatternType.Fireworks,
                1 => ProjectilePatternType.AfterimageBomb,
                2 => ProjectilePatternType.Tornado,
                _ => ProjectilePatternType.Fireworks
            },
            useCardinalDirections = isFirstPhase,
            duration = legacy.totalDuration > 0f ? legacy.totalDuration : 8f,
            fireInterval = legacy.fireInterval > 0f ? legacy.fireInterval : 0.25f,
            projectileSpeed = legacy.bulletSpeed > 0f ? legacy.bulletSpeed : 8f,
            baseDamage = legacy.damage,
            primaryElement = legacy.element switch
            {
                0 => ElementType.Water,
                1 => ElementType.Fire,
                2 => ElementType.Electric,
                3 => ElementType.Light,
                4 => ElementType.Dark,
                5 => ElementType.Poison,
                _ => ElementType.Water
            },
            damageTarget = legacy.damageTarget switch
            {
                1 => DamageTargetType.PlayerOnly,
                2 => DamageTargetType.Both,
                _ => DamageTargetType.EnemyOnly
            },
            healsPlayerOnSelfHit = legacy.bulletEffect == 1
        };

        return spec;
    }
}

public class Potion
{
    public PotionData data;
    public int quantity;

    public Potion(PotionData data, int quantity = 1)
    {
        this.data = data;
        this.quantity = quantity;
    }
}
