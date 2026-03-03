using System;
using System.Collections.Generic;
using UnityEngine;

public class PotionTestHarness : MonoBehaviour
{
    [Header("Auto Grant")]
    [SerializeField] private bool grantOnStart = true;
    [SerializeField] private int quantityPerPotion = 3;
    [SerializeField] private bool addDuplicateForStackTest = true;
    [SerializeField] private bool autoEquipFirstPotionToSlot1 = false;
    [SerializeField] private bool restrictAutoGrantToTestScenes = true;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode regrantHotkey = KeyCode.F6;
    [SerializeField] private KeyCode clearAndRegrantHotkey = KeyCode.F7;
    [SerializeField] private KeyCode logSummaryHotkey = KeyCode.F8;

    private Inventory inventory;
    private InventoryUI inventoryUI;
    private PlayerAttackSystem attackSystem;

    private void Start()
    {
        ResolveReferences();
        if (grantOnStart)
        {
            if (CanRunAutoGrantInCurrentScene())
            {
                GrantRepresentativePotions(clearExisting: false);
            }
            else
            {
                Debug.Log($"[PotionTestHarness] Auto-grant blocked in non-test scene: {gameObject.scene.name}");
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(regrantHotkey))
        {
            GrantRepresentativePotions(clearExisting: false);
        }

        if (Input.GetKeyDown(clearAndRegrantHotkey))
        {
            GrantRepresentativePotions(clearExisting: true);
        }

        if (Input.GetKeyDown(logSummaryHotkey))
        {
            LogPotionSummary();
        }
    }

    [ContextMenu("Grant Representative Potions")]
    public void GrantRepresentativePotions()
    {
        GrantRepresentativePotions(clearExisting: false);
    }

    [ContextMenu("Clear And Grant Representative Potions")]
    public void ClearAndGrantRepresentativePotions()
    {
        GrantRepresentativePotions(clearExisting: true);
    }

    private void GrantRepresentativePotions(bool clearExisting)
    {
        ResolveReferences();
        if (inventory == null)
        {
            Debug.LogWarning("[PotionTestHarness] Inventory not found.");
            return;
        }

        if (clearExisting)
        {
            ClearAllPotions();
        }

        List<PotionData> selected = SelectRepresentativePotions();
        if (selected.Count == 0)
        {
            Debug.LogWarning("[PotionTestHarness] No PotionData found in Resources/PotionData.");
            return;
        }

        int qty = Mathf.Max(1, quantityPerPotion);
        for (int i = 0; i < selected.Count; i++)
        {
            PotionData data = selected[i];
            if (data == null) continue;

            inventory.AddPotion(data, qty);
            if (addDuplicateForStackTest)
            {
                inventory.AddPotion(data, 1);
            }
        }

        if (autoEquipFirstPotionToSlot1)
        {
            EquipFirstPotionToSlot1();
        }

        RefreshUI();
        LogPotionSummary();
    }

    private void ResolveReferences()
    {
        if (inventory == null)
        {
            inventory = Inventory.Instance;
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
            }
        }

        if (inventoryUI == null)
        {
            inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        }

        if (attackSystem == null)
        {
            attackSystem = FindFirstObjectByType<PlayerAttackSystem>(FindObjectsInactive.Include);
        }
    }

    private void ClearAllPotions()
    {
        if (inventory == null) return;

        while (inventory.PotionItems.Count > 0)
        {
            Potion potion = inventory.PotionItems[0];
            inventory.RemovePotionCompletely(potion);
        }
    }

    private List<PotionData> SelectRepresentativePotions()
    {
        PotionData[] all = Resources.LoadAll<PotionData>("PotionData");
        List<PotionData> result = new List<PotionData>();
        if (all == null || all.Length == 0) return result;

        PotionData water = null;
        PotionData fire = null;
        PotionData electric = null;

        for (int i = 0; i < all.Length; i++)
        {
            PotionData data = all[i];
            if (data == null) continue;

            ElementType element = GetPrimaryElement(data);
            switch (element)
            {
                case ElementType.Fire:
                    if (fire == null) fire = data;
                    break;
                case ElementType.Electric:
                    if (electric == null) electric = data;
                    break;
                default:
                    if (water == null) water = data;
                    break;
            }
        }

        if (water != null) result.Add(water);
        if (fire != null) result.Add(fire);
        if (electric != null) result.Add(electric);

        if (result.Count == 0)
        {
            for (int i = 0; i < Mathf.Min(3, all.Length); i++)
            {
                if (all[i] != null) result.Add(all[i]);
            }
        }

        return result;
    }

    private static ElementType GetPrimaryElement(PotionData data)
    {
        if (data == null) return ElementType.Water;

        PotionPhaseSpec phase = data.GetPhase(0);
        if (phase != null)
        {
            return phase.primaryElement;
        }

        return data.element1 switch
        {
            Element.Fire => ElementType.Fire,
            Element.Lightning => ElementType.Electric,
            _ => ElementType.Water
        };
    }

    private void EquipFirstPotionToSlot1()
    {
        if (attackSystem == null || inventory == null) return;
        if (attackSystem.slots == null || attackSystem.slots.Count == 0) return;
        if (inventory.PotionItems.Count == 0) return;

        Potion potion = inventory.PotionItems[0];
        if (potion == null || potion.data == null) return;

        WeaponSlot slot = attackSystem.slots[0];
        slot.type = WeaponType.PotionBomb;
        slot.equippedPotion = potion;
        slot.count = potion.quantity;
        slot.specificPrefab = null;
        attackSystem.slots[0] = slot;
    }

    private void RefreshUI()
    {
        if (inventoryUI != null)
        {
            inventoryUI.RefreshUI();
        }

        WeaponSlotUI slotUI = FindFirstObjectByType<WeaponSlotUI>(FindObjectsInactive.Include);
        if (slotUI != null)
        {
            slotUI.ForceRefresh();
        }
    }

    private void LogPotionSummary()
    {
        ResolveReferences();
        if (inventory == null)
        {
            Debug.Log("[PotionTestHarness] Inventory missing.");
            return;
        }

        List<Potion> list = inventory.PotionItems;
        if (list == null || list.Count == 0)
        {
            Debug.Log("[PotionTestHarness] Potion inventory is empty.");
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            Potion p = list[i];
            if (p == null || p.data == null) continue;
            Debug.Log($"[PotionTestHarness] [{i}] {p.data.GetDisplayName()} x{p.quantity}");
        }
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
