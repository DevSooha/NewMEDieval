using System;
using System.Collections.Generic;
using UnityEngine;

public static class PotionDesignCatalog
{
    private sealed class IngredientTempDesign
    {
        public int damage;
        public ElementType subElement = ElementType.None;
        public bool ignoreSelfHitPenalty;
        public float projectileSpeed = 8f;
        public float fireInterval = 0.25f;
        public float rotationSpeedDegPerSec = 30f;
        public readonly List<StatusEffectSpec> playerEffects = new();
        public readonly List<StatusEffectSpec> enemyEffects = new();
    }

    private sealed class IngredientDesign
    {
        public string id;
        public string prefix;
        public string suffix;
        public ElementType primary;
        public IngredientShapeType shape;
        public readonly Dictionary<PotionTemperature, IngredientTempDesign> tempMap = new();
    }

    private static readonly Dictionary<string, IngredientDesign> Designs = BuildDesigns();

    public static PotionData CraftPotion(ItemData first, ItemData second, PotionTemperature temperature)
    {
        if (first == null || second == null) return null;

        IngredientDesign firstDesign = ResolveDesign(first);
        IngredientDesign secondDesign = ResolveDesign(second);

        PotionData crafted = ScriptableObject.CreateInstance<PotionData>();
        crafted.isCraftedRuntimeData = true;
        crafted.temperature = temperature;
        crafted.topIMG = first.topSprite != null ? first.topSprite : first.icon;
        crafted.bottomIMG = second.bottomSprite != null ? second.bottomSprite : second.icon;
        crafted.frameIMG = PotionVisualResolver.ResolveCraftFrame(
            first,
            second,
            firstDesign.primary,
            secondDesign.primary);
        crafted.icon = crafted.topIMG != null ? crafted.topIMG : crafted.bottomIMG;

        string tempText = temperature switch
        {
            PotionTemperature.Low => "저온",
            PotionTemperature.Mid => "중온",
            PotionTemperature.High => "고온",
            _ => "실패"
        };

        crafted.craftedDisplayName = $"[{tempText}] {firstDesign.prefix}{secondDesign.suffix} 물약";
        crafted.potionName = crafted.craftedDisplayName;
        crafted.itemName = crafted.craftedDisplayName;

        crafted.phase1 = BuildPhase(firstDesign, temperature, true);
        crafted.phase2 = BuildPhase(secondDesign, temperature, false);

        crafted.damage1 = crafted.phase1 != null ? crafted.phase1.baseDamage : 0;
        crafted.damage2 = crafted.phase2 != null ? crafted.phase2.baseDamage : 0;
        crafted.element1 = ToLegacyElement(crafted.phase1 != null ? crafted.phase1.primaryElement : ElementType.Water);
        crafted.element2 = ToLegacyElement(crafted.phase2 != null ? crafted.phase2.primaryElement : ElementType.Water);
        crafted.bulletType1 = ToLegacyBullet(crafted.phase1 != null ? crafted.phase1.patternType : ProjectilePatternType.Fireworks);
        crafted.bulletType2 = ToLegacyBullet(crafted.phase2 != null ? crafted.phase2.patternType : ProjectilePatternType.Fireworks);

        return crafted;
    }

    public static bool IsKnownIngredient(ItemData data)
    {
        if (data == null) return false;
        return Designs.ContainsKey(Normalize(data.GetIngredientId()));
    }

    private static IngredientDesign ResolveDesign(ItemData data)
    {
        string key = Normalize(data.GetIngredientId());
        if (Designs.TryGetValue(key, out IngredientDesign design))
        {
            return design;
        }

        IngredientDesign fallback = new IngredientDesign
        {
            id = data.GetIngredientId(),
            prefix = string.IsNullOrWhiteSpace(data.topName) ? data.name : data.topName,
            suffix = string.IsNullOrWhiteSpace(data.bottomName) ? data.name : data.bottomName,
            primary = data.GetPrimaryElementType(),
            shape = data.shapeType
        };

        fallback.tempMap[PotionTemperature.Low] = new IngredientTempDesign { damage = Math.Max(1, data.topDamage) };
        fallback.tempMap[PotionTemperature.Mid] = new IngredientTempDesign { damage = Math.Max(1, data.topDamage + data.bottomDamage) };
        fallback.tempMap[PotionTemperature.High] = new IngredientTempDesign { damage = Math.Max(1, data.bottomDamage) };
        return fallback;
    }

    private static PotionPhaseSpec BuildPhase(IngredientDesign design, PotionTemperature temperature, bool isFirstPhase)
    {
        if (!design.tempMap.TryGetValue(temperature, out IngredientTempDesign tempDesign))
        {
            if (!design.tempMap.TryGetValue(PotionTemperature.Mid, out tempDesign))
            {
                tempDesign = new IngredientTempDesign { damage = 100 };
            }
        }

        PotionPhaseSpec phase = new PotionPhaseSpec
        {
            ingredientId = design.id,
            patternType = design.shape switch
            {
                IngredientShapeType.AfterimageBomb => ProjectilePatternType.AfterimageBomb,
                IngredientShapeType.Tornado => ProjectilePatternType.Tornado,
                _ => ProjectilePatternType.Fireworks
            },
            useCardinalDirections = isFirstPhase,
            duration = 8f,
            fireInterval = tempDesign.fireInterval,
            projectileSpeed = tempDesign.projectileSpeed,
            rotationSpeedDegPerSec = tempDesign.rotationSpeedDegPerSec,
            baseDamage = tempDesign.damage,
            primaryElement = design.primary,
            subElement = tempDesign.subElement,
            damageTarget = DamageTargetType.Both,
            ignoreSelfHitPenalty = tempDesign.ignoreSelfHitPenalty,
            healsPlayerOnSelfHit = ContainsEffect(tempDesign.playerEffects, StatusEffectType.HealPlayerFlat)
        };

        foreach (StatusEffectSpec effect in tempDesign.playerEffects)
        {
            phase.onPlayerHitEffects.Add(Clone(effect));
        }

        foreach (StatusEffectSpec effect in tempDesign.enemyEffects)
        {
            phase.onEnemyHitEffects.Add(Clone(effect));
        }

        return phase;
    }

    private static bool ContainsEffect(List<StatusEffectSpec> effects, StatusEffectType type)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].effectType == type) return true;
        }
        return false;
    }

    private static StatusEffectSpec Clone(StatusEffectSpec src)
    {
        if (src == null) return null;
        return new StatusEffectSpec
        {
            effectType = src.effectType,
            duration = src.duration,
            magnitude = src.magnitude,
            interval = src.interval
        };
    }

    private static Dictionary<string, IngredientDesign> BuildDesigns()
    {
        Dictionary<string, IngredientDesign> map = new();

        Add(map, NewIngredient(
            "Pastelbloom", "파스텔", "블룸", ElementType.Water, IngredientShapeType.Fireworks,
            low: Temp(50,
                player: new[] { Fx(StatusEffectType.StealthInvulnerable, 7f, 0.3f) }),
            mid: Temp(0,
                player: new[] { Fx(StatusEffectType.HealPlayerFlat, 0f, 1f) },
                enemy: new[] { Fx(StatusEffectType.HealEnemyCurrentHpPercent, 0f, 0.8f) }),
            high: Temp(100)
        ));

        Add(map, NewIngredient(
            "OILPEASOAK", "오일", "피소크", ElementType.Water, IngredientShapeType.Tornado,
            low: Temp(220,
                player: new[] { Fx(StatusEffectType.PlayerInputDelay, 6f, 2f) }),
            mid: Temp(100,
                enemy: new[] { Fx(StatusEffectType.EnemyStun, 5f, 1f) }),
            high: Temp(140)
        ));

        Add(map, NewIngredient(
            "MORBIDMIRE", "몰비드", "마이어", ElementType.Water, IngredientShapeType.AfterimageBomb,
            low: Temp(0,
                enemy: new[] { Fx(StatusEffectType.EnemyStun, 8f, 1f) }),
            mid: Temp(120, sub: ElementType.Dark),
            high: Temp(0, sub: ElementType.Poison,
                enemy: new[] { Fx(StatusEffectType.PoisonDot, 10f, 5f, 2f) })
        ));

        Add(map, NewIngredient(
            "MISTYFOG", "미스티", "포그", ElementType.Water, IngredientShapeType.AfterimageBomb,
            low: Temp(100,
                player: new[] { Fx(StatusEffectType.PlayerMoveSpeedMultiplier, 16f, 2f) }),
            mid: Temp(0,
                player: new[] { Fx(StatusEffectType.HealPlayerFlat, 0f, 1f) },
                enemy: new[] { Fx(StatusEffectType.HealEnemyCurrentHpPercent, 0f, 0.8f) }),
            high: Temp(100,
                player: new[] { Fx(StatusEffectType.StealthOnly, 15f, 0.3f) })
        ));

        Add(map, NewIngredient(
            "Edenicash", "에데닉", "애쉬", ElementType.Fire, IngredientShapeType.AfterimageBomb,
            low: Temp(250,
                player: new[] { Fx(StatusEffectType.BlindBlack, 0.5f, 1f) }),
            mid: Temp(120),
            high: Temp(200,
                player: new[]
                {
                    Fx(StatusEffectType.PlayerRedStateContactBurn, 6f, 50f, 0.5f),
                    Fx(StatusEffectType.PlayerKnockbackImmune, 6f, 1f)
                })
        ));

        Add(map, NewIngredient(
            "Dustflare", "더스트", "플레어", ElementType.Fire, IngredientShapeType.Fireworks,
            low: Temp(50, projectileSpeed: 4f),
            mid: Temp(100),
            high: Temp(200,
                enemy: new[] { Fx(StatusEffectType.EnemyMoveSpeedMultiplier, 10f, 2f) })
        ));

        Add(map, NewIngredient(
            "HALLOWBLAZE", "할로우", "블레이즈", ElementType.Fire, IngredientShapeType.Tornado,
            low: Temp(140, sub: ElementType.Light, rotationSpeed: 15f),
            mid: Temp(140, sub: ElementType.Light, rotationSpeed: 60f),
            high: Temp(300, sub: ElementType.Light,
                player: new[] { Fx(StatusEffectType.BlindWhite, 0.5f, 1f) })
        ));

        Add(map, NewIngredient(
            "HelioSpark", "헬리오", "스파크", ElementType.Electric, IngredientShapeType.Fireworks,
            low: Temp(30, ignoreSelfPenalty: true),
            mid: Temp(100,
                player: new[] { Fx(StatusEffectType.PlayerMoveSpeedMultiplier, 15f, 2f) }),
            high: Temp(50,
                enemy: new[] { Fx(StatusEffectType.EnemyStun, 5f, 1f) })
        ));

        Add(map, NewIngredient(
            "Gaseel", "가스", "이일", ElementType.Electric, IngredientShapeType.AfterimageBomb,
            low: Temp(100, sub: ElementType.Poison),
            mid: Temp(150, sub: ElementType.Dark),
            high: Temp(200,
                player: new[] { Fx(StatusEffectType.PlayerInputReverse, 10f, 1f) })
        ));

        Add(map, NewIngredient(
            "FALLWING", "폴", "윙", ElementType.Electric, IngredientShapeType.Tornado,
            low: Temp(100),
            mid: Temp(60,
                enemy: new[] { Fx(StatusEffectType.EnemyKnockback, 0f, 64f) }),
            high: Temp(100,
                player: new[] { Fx(StatusEffectType.PlayerStun, 3f, 1f) },
                enemy: new[] { Fx(StatusEffectType.EnemyStun, 3f, 1f) })
        ));

        return map;
    }

    private static IngredientDesign NewIngredient(
        string id,
        string prefix,
        string suffix,
        ElementType primary,
        IngredientShapeType shape,
        IngredientTempDesign low,
        IngredientTempDesign mid,
        IngredientTempDesign high)
    {
        IngredientDesign design = new IngredientDesign
        {
            id = id,
            prefix = prefix,
            suffix = suffix,
            primary = primary,
            shape = shape
        };

        design.tempMap[PotionTemperature.Low] = low;
        design.tempMap[PotionTemperature.Mid] = mid;
        design.tempMap[PotionTemperature.High] = high;
        return design;
    }

    private static IngredientTempDesign Temp(
        int damage,
        ElementType sub = ElementType.None,
        bool ignoreSelfPenalty = false,
        float projectileSpeed = 8f,
        float fireInterval = 0.25f,
        float rotationSpeed = 30f,
        StatusEffectSpec[] player = null,
        StatusEffectSpec[] enemy = null)
    {
        IngredientTempDesign temp = new IngredientTempDesign
        {
            damage = damage,
            subElement = sub,
            ignoreSelfHitPenalty = ignoreSelfPenalty,
            projectileSpeed = projectileSpeed,
            fireInterval = fireInterval,
            rotationSpeedDegPerSec = rotationSpeed
        };

        if (player != null)
        {
            for (int i = 0; i < player.Length; i++) temp.playerEffects.Add(player[i]);
        }

        if (enemy != null)
        {
            for (int i = 0; i < enemy.Length; i++) temp.enemyEffects.Add(enemy[i]);
        }

        return temp;
    }

    private static StatusEffectSpec Fx(StatusEffectType type, float duration, float magnitude, float interval = 0f)
    {
        return new StatusEffectSpec
        {
            effectType = type,
            duration = duration,
            magnitude = magnitude,
            interval = interval
        };
    }

    private static void Add(Dictionary<string, IngredientDesign> map, IngredientDesign design)
    {
        string norm = Normalize(design.id);
        map[norm] = design;
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string upper = raw.Trim().ToUpperInvariant();
        upper = upper.Replace(" ", string.Empty);
        upper = upper.Replace("_", string.Empty);
        upper = upper.Replace("-", string.Empty);
        upper = upper.Replace("/", string.Empty);
        return upper;
    }

    private static BulletType ToLegacyBullet(ProjectilePatternType patternType)
    {
        return patternType switch
        {
            ProjectilePatternType.AfterimageBomb => BulletType.Bomb,
            ProjectilePatternType.Tornado => BulletType.Spiral,
            _ => BulletType.Fireworks
        };
    }

    private static Element ToLegacyElement(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => Element.Fire,
            ElementType.Electric => Element.Lightning,
            _ => Element.Water
        };
    }
}
