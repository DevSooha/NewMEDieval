using System;
using System.Collections.Generic;
using UnityEngine;

public class GrantPotionCoverageToInventoryTest : MonoBehaviour
{
    private const int FirstPotionSlotIndex = 1;
    private const int LastPotionSlotIndex = 3;
    private const int EquipCycleCount = 3;

    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerAttackSystem attackSystem;

    [Header("Grant Settings")]
    [SerializeField] private string itemResourcesFolder = "ItemData";
    [SerializeField] private int quantityPerPotion = 3;
    [SerializeField] private bool clearExistingPotionsBeforeGrant = false;
    [SerializeField] private bool autoEquipToPotionSlots = true;
    [SerializeField] private bool grantOnStart = true;
    [SerializeField] private bool restrictAutoGrantToTestScenes = true;
    [SerializeField] private KeyCode grantHotkey = KeyCode.F8;
    [SerializeField] private bool logSelectedPotions = true;

    private bool grantedOnStart;
    private int nextAutoEquipRecipeIndex;

    private void Start()
    {
        ResolveInventory();

        if (!grantOnStart) return;

        if (CanRunAutoGrantInCurrentScene())
        {
            GrantCoveragePotions();
            grantedOnStart = true;
            return;
        }

        Debug.Log($"[GrantPotionCoverageToInventoryTest] Auto-grant blocked in non-test scene: {gameObject.scene.name}");
    }

    private void Update()
    {
        if (!Input.GetKeyDown(grantHotkey)) return;

        ResolveInventory();
        ResolveAttackSystem();

        if (inventory == null)
        {
            Debug.LogWarning("[GrantPotionCoverageToInventoryTest] Inventory reference is missing.");
            return;
        }

        if (grantedOnStart)
        {
            CycleEquippedCoveragePotions();
            return;
        }

        GrantCoveragePotions();
    }

    [ContextMenu("Grant Coverage Potions")]
    public void GrantCoveragePotions()
    {
        ResolveInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[GrantPotionCoverageToInventoryTest] Inventory reference is missing.");
            return;
        }

        List<ItemData> ingredients = LoadKnownIngredients();
        if (ingredients.Count == 0)
        {
            Debug.LogWarning($"[GrantPotionCoverageToInventoryTest] No known ItemData found in Resources/{itemResourcesFolder}");
            return;
        }

        List<PotionRecipeEntry> recipes = BuildIngredientTraitRecipeList(ingredients);
        if (recipes.Count == 0)
        {
            Debug.LogWarning("[GrantPotionCoverageToInventoryTest] Failed to build crafted potion entries.");
            return;
        }

        if (clearExistingPotionsBeforeGrant)
        {
            ClearAllPotions();
        }

        int quantity = Mathf.Max(1, quantityPerPotion);
        for (int i = 0; i < recipes.Count; i++)
        {
            inventory.AddPotion(recipes[i].potionData, quantity);
        }

        if (autoEquipToPotionSlots)
        {
            AutoEquipPotionSlots(
                recipes,
                nextAutoEquipRecipeIndex,
                addBackPreviousToInventory: !clearExistingPotionsBeforeGrant);
        }

        LogSelection(recipes, quantity);
        grantedOnStart = true;
    }

    private List<ItemData> LoadKnownIngredients()
    {
        ItemData[] all = Resources.LoadAll<ItemData>(itemResourcesFolder);
        List<ItemData> result = new();
        if (all == null || all.Length == 0) return result;

        for (int i = 0; i < all.Length; i++)
        {
            ItemData data = all[i];
            if (data == null) continue;
            if (data.category == ItemCategory.Potion) continue;
            if (!PotionDesignCatalog.IsKnownIngredient(data)) continue;
            result.Add(data);
        }

        return result;
    }

    private sealed class PotionRecipeEntry
    {
        public int index;
        public ItemData first;
        public ItemData second;
        public PotionTemperature temperature;
        public PotionData potionData;
    }

    private static List<PotionRecipeEntry> BuildIngredientTraitRecipeList(List<ItemData> ingredients)
    {
        List<PotionRecipeEntry> recipes = new();
        PotionTemperature[] temperatures =
        {
            PotionTemperature.Low,
            PotionTemperature.Mid,
            PotionTemperature.High
        };
        ingredients.Sort(CompareIngredients);

        int index = 1;
        for (int t = 0; t < temperatures.Length; t++)
        {
            PotionTemperature temperature = temperatures[t];
            for (int i = 0; i < ingredients.Count; i++)
            {
                for (int j = i; j < ingredients.Count; j++)
                {
                    ItemData first = ingredients[i];
                    ItemData second = ingredients[j];
                    PotionData crafted = PotionDesignCatalog.CraftPotion(first, second, temperature);
                    if (crafted == null) continue;
                    if (!HasMeaningfulEffect(crafted)) continue;

                    recipes.Add(new PotionRecipeEntry
                    {
                        index = index++,
                        first = first,
                        second = second,
                        temperature = temperature,
                        potionData = crafted
                    });
                }
            }
        }

        return recipes;
    }

    private static int CompareIngredients(ItemData left, ItemData right)
    {
        return string.Compare(left.GetIngredientId(), right.GetIngredientId(), StringComparison.Ordinal);
    }

    private static bool HasMeaningfulEffect(PotionData potionData)
    {
        return PhaseHasMeaningfulEffect(potionData?.GetPhase(0))
               || PhaseHasMeaningfulEffect(potionData?.GetPhase(1));
    }

    private static bool PhaseHasMeaningfulEffect(PotionPhaseSpec phase)
    {
        if (phase == null)
        {
            return false;
        }

        if (phase.healsPlayerOnSelfHit || phase.ignoreSelfHitPenalty)
        {
            return true;
        }

        return HasNonNoneEffect(phase.onPlayerHitEffects) || HasNonNoneEffect(phase.onEnemyHitEffects);
    }

    private static bool HasNonNoneEffect(List<StatusEffectSpec> effects)
    {
        if (effects == null)
        {
            return false;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffectSpec effect = effects[i];
            if (effect != null && effect.effectType != StatusEffectType.None)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearAllPotions()
    {
        while (inventory.PotionItems.Count > 0)
        {
            Potion potion = inventory.PotionItems[0];
            inventory.RemovePotionCompletely(potion);
        }
    }

    private void ResolveInventory()
    {
        if (inventory != null) return;

        inventory = Inventory.Instance;
        if (inventory != null) return;

        inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
    }

    private void ResolveAttackSystem()
    {
        if (attackSystem != null) return;

        attackSystem = FindFirstObjectByType<PlayerAttackSystem>(FindObjectsInactive.Include);
    }

    private void AutoEquipPotionSlots(bool addBackPreviousToInventory)
    {
        List<ItemData> ingredients = LoadKnownIngredients();
        List<PotionRecipeEntry> recipes = BuildIngredientTraitRecipeList(ingredients);
        AutoEquipPotionSlots(recipes, nextAutoEquipRecipeIndex, addBackPreviousToInventory);
    }

    private void AutoEquipPotionSlots(
        List<PotionRecipeEntry> recipes,
        int startRecipeIndex,
        bool addBackPreviousToInventory)
    {
        ResolveAttackSystem();
        if (attackSystem == null || inventory == null)
        {
            Debug.LogWarning("[GrantPotionCoverageToInventoryTest] Auto-equip skipped: PlayerAttackSystem or Inventory is missing.");
            return;
        }

        ClearEquippedPotionSlots(addBackPreviousToInventory);

        if (recipes == null || recipes.Count == 0)
        {
            Debug.LogWarning("[GrantPotionCoverageToInventoryTest] Auto-equip skipped: no recipes available.");
            return;
        }

        int recipeCount = recipes.Count;
        int startIndex = recipeCount > 0 ? Mathf.Clamp(startRecipeIndex, 0, recipeCount - 1) : 0;
        int equippedCount = 0;
        for (int slotIndex = FirstPotionSlotIndex; slotIndex <= LastPotionSlotIndex; slotIndex++)
        {
            Potion potion = FindNextCoveragePotion(recipes, startIndex, equippedCount);
            if (potion == null)
            {
                break;
            }

            if (attackSystem.TryEquipPotionToSlot(potion, slotIndex, returnPreviousToInventory: false))
            {
                equippedCount++;
            }
        }

        if (recipeCount > 0)
        {
            nextAutoEquipRecipeIndex = (startIndex + Mathf.Max(1, equippedCount > 0 ? equippedCount : EquipCycleCount)) % recipeCount;
        }

        Debug.Log($"[GrantPotionCoverageToInventoryTest] Auto-equipped {equippedCount} potion slots.");
    }

    private void CycleEquippedCoveragePotions()
    {
        List<ItemData> ingredients = LoadKnownIngredients();
        List<PotionRecipeEntry> recipes = BuildIngredientTraitRecipeList(ingredients);
        if (recipes.Count == 0)
        {
            Debug.LogWarning("[GrantPotionCoverageToInventoryTest] Cycle skipped: no recipes available.");
            return;
        }

        AutoEquipPotionSlots(recipes, nextAutoEquipRecipeIndex, addBackPreviousToInventory: true);
    }

    private void ClearEquippedPotionSlots(bool addBackPreviousToInventory)
    {
        if (attackSystem == null || attackSystem.slots == null)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < attackSystem.slots.Count; slotIndex++)
        {
            WeaponSlot slot = attackSystem.slots[slotIndex];
            if (slot == null || slot.type != WeaponType.PotionBomb || slot.equippedPotion == null)
            {
                continue;
            }

            attackSystem.TryUnequipPotionFromSlot(slotIndex, addBackToInventory: addBackPreviousToInventory);
        }
    }

    private Potion FindNextCoveragePotion(List<PotionRecipeEntry> recipes, int startRecipeIndex, int offset)
    {
        if (recipes == null || recipes.Count == 0 || inventory == null)
        {
            return null;
        }

        int count = recipes.Count;
        for (int i = 0; i < count; i++)
        {
            int recipeIndex = (startRecipeIndex + offset + i) % count;
            PotionData targetData = recipes[recipeIndex].potionData;
            Potion potion = FindPotionInInventory(targetData);
            if (potion != null)
            {
                return potion;
            }
        }

        return null;
    }

    private Potion FindPotionInInventory(PotionData targetData)
    {
        if (inventory == null || inventory.PotionItems == null || targetData == null)
        {
            return null;
        }

        for (int i = 0; i < inventory.PotionItems.Count; i++)
        {
            Potion potion = inventory.PotionItems[i];
            if (potion == null || potion.data == null)
            {
                continue;
            }

            if (AreSamePotionData(potion.data, targetData))
            {
                return potion;
            }
        }

        return null;
    }

    private static bool AreSamePotionData(PotionData left, PotionData right)
    {
        if (left == right) return true;
        if (left == null || right == null) return false;

        return string.Equals(left.GetDisplayName(), right.GetDisplayName(), StringComparison.Ordinal)
            && left.temperature == right.temperature
            && left.damage1 == right.damage1
            && left.damage2 == right.damage2
            && left.bulletType1 == right.bulletType1
            && left.bulletType2 == right.bulletType2
            && string.Equals(left.GetPhase(0)?.ingredientId ?? string.Empty, right.GetPhase(0)?.ingredientId ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(left.GetPhase(1)?.ingredientId ?? string.Empty, right.GetPhase(1)?.ingredientId ?? string.Empty, StringComparison.Ordinal);
    }

    private void LogSelection(List<PotionRecipeEntry> recipes, int quantity)
    {
        Debug.Log($"[GrantPotionCoverageToInventoryTest] Granted {recipes.Count} crafted potions x{quantity}.");

        if (!logSelectedPotions) return;

        for (int i = 0; i < recipes.Count; i++)
        {
            PotionRecipeEntry recipe = recipes[i];
            Debug.Log($"[GrantPotionCoverageToInventoryTest] #{recipe.index} {FormatRecipeLog(recipe)}");
        }
    }

    private static string FormatRecipeLog(PotionRecipeEntry recipe)
    {
        PotionPhaseSpec phase1 = recipe.potionData != null ? recipe.potionData.GetPhase(0) : null;
        PotionPhaseSpec phase2 = recipe.potionData != null ? recipe.potionData.GetPhase(1) : null;

        return $"{recipe.potionData.GetDisplayName()} | temp={recipe.temperature} | first={recipe.first.GetIngredientId()} | second={recipe.second.GetIngredientId()} | phase1={DescribePhase(phase1)} | phase2={DescribePhase(phase2)}";
    }

    private static string DescribePhase(PotionPhaseSpec phase)
    {
        if (phase == null) return "none";

        string playerEffects = DescribeEffects(phase.onPlayerHitEffects);
        string enemyEffects = DescribeEffects(phase.onEnemyHitEffects);
        string selfHit = phase.healsPlayerOnSelfHit ? "healSelf" : phase.ignoreSelfHitPenalty ? "ignoreSelfPenalty" : "defaultSelfHit";

        return $"{phase.patternType}/{phase.primaryElement}->{phase.subElement} | {selfHit} | player[{playerEffects}] enemy[{enemyEffects}]";
    }

    private static string DescribeEffects(List<StatusEffectSpec> effects)
    {
        if (effects == null || effects.Count == 0) return "none";

        List<string> labels = new();
        for (int i = 0; i < effects.Count; i++)
        {
            StatusEffectSpec effect = effects[i];
            if (effect == null || effect.effectType == StatusEffectType.None) continue;
            labels.Add(effect.effectType.ToString());
        }

        return labels.Count == 0 ? "none" : string.Join(",", labels);
    }

    private bool CanRunAutoGrantInCurrentScene()
    {
        if (!restrictAutoGrantToTestScenes)
        {
            return true;
        }

        string sceneName = gameObject.scene.name;
        return !string.IsNullOrWhiteSpace(sceneName)
               && sceneName.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
