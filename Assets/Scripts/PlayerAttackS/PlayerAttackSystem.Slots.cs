using System.Collections.Generic;
using UnityEngine;

public partial class PlayerAttackSystem
{
    public void EquipPotionFromInventory(Potion potion)
    {
        const int defaultPotionSlotIndex = 1;
        TryEquipPotionToSlot(potion, defaultPotionSlotIndex, returnPreviousToInventory: true);
    }

    public bool TryEquipPotionToSlot(Potion potion, int slotIndex, bool returnPreviousToInventory = true)
    {
        if (potion == null || potion.data == null)
        {
            return false;
        }

        EnsureCoreSlots();
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            return false;
        }

        WeaponSlot slot = slots[slotIndex] ?? new WeaponSlot();
        if (slot.type == WeaponType.Melee)
        {
            return false;
        }

        if (slot.type == WeaponType.PotionBomb && slot.equippedPotion != null)
        {
            if (!returnPreviousToInventory)
            {
                return false;
            }

            ReturnPotionToInventory(slot.equippedPotion);
        }

        slot.type = WeaponType.PotionBomb;
        slot.equippedPotion = potion;
        slot.count = Mathf.Max(1, potion.quantity);
        slot.specificPrefab = null;
        slots[slotIndex] = slot;

        RemovePotionFromInventory(potion);
        NotifyWeaponSlotsChanged(compactSlots: false);
        return true;
    }

    public bool TryUnequipPotionFromSlot(int slotIndex, bool addBackToInventory = true)
    {
        EnsureCoreSlots();
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            return false;
        }

        WeaponSlot slot = slots[slotIndex];
        if (slot == null || slot.type != WeaponType.PotionBomb || slot.equippedPotion == null)
        {
            return false;
        }

        Potion potion = slot.equippedPotion;
        slot.equippedPotion = null;
        slot.specificPrefab = null;
        slot.count = -1;
        slot.type = WeaponType.None;
        slots[slotIndex] = slot;

        if (addBackToInventory)
        {
            ReturnPotionToInventory(potion);
        }

        NotifyWeaponSlotsChanged(compactSlots: true);
        return true;
    }

    void UseAmmo(int amount)
    {
        if (slots.Count == 0) return;
        amount = Mathf.Max(0, amount);
        if (amount <= 0) return;

        WeaponSlot slot = slots[0];
        if (slot.type != WeaponType.PotionBomb) return;
        bool consumedAny = false;
        bool shouldNormalizeAfterUse = false;

        if (slot.equippedPotion != null)
        {
            int currentQty = Mathf.Max(0, slot.equippedPotion.quantity);
            int consumedAmount = Mathf.Min(amount, currentQty);
            slot.equippedPotion.quantity = currentQty - consumedAmount;
            consumedAny = consumedAmount > 0;
            if (slot.equippedPotion.quantity <= 0)
            {
                slot.equippedPotion = null;
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.specificPrefab = null;
                shouldNormalizeAfterUse = true;
            }
            else
            {
                slot.count = slot.equippedPotion.quantity;
            }
        }
        else
        {
            if (slot.count != -1)
            {
                slot.count -= amount;
                consumedAny = true;
            }

            if (slot.count <= 0)
            {
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.specificPrefab = null;
                shouldNormalizeAfterUse = true;
            }
        }

        slots[0] = slot;

        if (consumedAny || shouldNormalizeAfterUse)
        {
            if (shouldNormalizeAfterUse)
            {
                // Bring melee/usable slot back to the front after potion depletion.
                NormalizeWeaponSlots(compactSlots: true);
            }
            RefreshWeaponAndInventoryUI();
        }
    }

    void RotateWeaponSlots()
    {
        EnsureCoreSlots();
        NormalizeWeaponSlots(compactSlots: true);
        if (CountUsableSlots() <= 1) return;

        int currentIndex = 0;
        int nextIndex = FindNextNonEmptyIndex(currentIndex);
        if (nextIndex <= 0) return;
        RotateListToIndex(nextIndex);
        RefreshWeaponAndInventoryUI();
    }

    int FindNextNonEmptyIndex(int startIndex)
    {
        int count = slots.Count;
        for (int i = 1; i < count; i++)
        {
            int idx = (startIndex + i) % count;
            if (idx == 0)
            {
                continue;
            }

            WeaponSlot slot = slots[idx];
            if (slot == null || slot.type == WeaponType.None)
            {
                continue;
            }

            if (slot.type == WeaponType.PotionBomb && (slot.equippedPotion == null || slot.equippedPotion.quantity <= 0))
            {
                continue;
            }

            return idx;
        }

        return -1;
    }

    void RotateListToIndex(int index)
    {
        if (index <= 0 || index >= slots.Count)
        {
            return;
        }

        List<WeaponSlot> rotated = new List<WeaponSlot>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            rotated.Add(slots[(index + i) % slots.Count]);
        }

        slots.Clear();
        slots.AddRange(rotated);
    }

    void CompactSlots()
    {
        NormalizeWeaponSlots(compactSlots: true);
    }

    void SyncPotionSlotCounts()
    {
        EnsureCoreSlots();
        bool slotChanged = false;
        bool shouldCompact = false;
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (slot.type != WeaponType.PotionBomb)
            {
                continue;
            }

            if (slot.equippedPotion == null)
            {
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.specificPrefab = null;
                slots[i] = slot;
                slotChanged = true;
                shouldCompact = true;
                continue;
            }

            int qty = Mathf.Max(0, slot.equippedPotion.quantity);
            if (slot.count != qty)
            {
                slot.count = qty;
                slotChanged = true;
            }

            if (qty == 0)
            {
                slot.type = WeaponType.None;
                slot.count = -1;
                slot.equippedPotion = null;
                slot.specificPrefab = null;
                slotChanged = true;
                shouldCompact = true;
            }

            slots[i] = slot;
        }

        if (slotChanged)
        {
            if (shouldCompact)
            {
                NormalizeWeaponSlots(compactSlots: true);
            }
            RefreshWeaponAndInventoryUI();
        }

        // Weapon status UI removed.
    }

    void RemovePotionFromInventory(Potion potion)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null || potion == null) return;

        inv.RemovePotionCompletely(potion);
    }

    void ReturnPotionToInventory(Potion potion)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null || potion == null || potion.data == null)
        {
            return;
        }

        int qty = Mathf.Max(0, potion.quantity);
        if (qty <= 0 || inv.ContainsPotion(potion))
        {
            return;
        }

        inv.AddPotion(potion.data, qty);
    }

    void RefreshWeaponSlotUI()
    {
        WeaponSlotUI slotUI = FindFirstObjectByType<WeaponSlotUI>(FindObjectsInactive.Include);
        if (slotUI != null)
        {
            slotUI.ForceRefresh();
        }
    }

    void RefreshWeaponAndInventoryUI()
    {
        RefreshWeaponSlotUI();
        if (inventoryUI != null)
        {
            inventoryUI.RefreshUI();
        }
    }

    public void NotifyWeaponSlotsChanged(bool compactSlots = true)
    {
        NormalizeWeaponSlots(compactSlots);
        RefreshWeaponAndInventoryUI();
    }

    void EnsureCoreSlots()
    {
        if (slots == null)
        {
            slots = new List<WeaponSlot>();
        }

        if (slots.Count == 0)
        {
            slots.Add(new WeaponSlot { type = WeaponType.Melee, count = -1 });
        }

        while (slots.Count < 4)
        {
            slots.Add(new WeaponSlot { type = WeaponType.None, count = -1 });
        }

        while (slots.Count > 4)
        {
            slots.RemoveAt(slots.Count - 1);
        }

        WeaponSlot first = slots[0];
        if (first == null)
        {
            first = new WeaponSlot();
        }

        if (first.type == WeaponType.None)
        {
            first.type = WeaponType.Melee;
            first.count = -1;
            first.equippedPotion = null;
            first.specificPrefab = null;
            slots[0] = first;
        }
    }

    public bool IsCurrentSlotPotion()
    {
        EnsureCoreSlots();
        if (slots == null || slots.Count == 0)
        {
            return false;
        }

        WeaponSlot slot = slots[0];
        if (slot == null)
        {
            return false;
        }

        return slot.type == WeaponType.PotionBomb
            && slot.equippedPotion != null
            && slot.equippedPotion.quantity > 0;
    }

    void NormalizeWeaponSlots(bool compactSlots = true)
    {
        EnsureCoreSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null)
            {
                slot = new WeaponSlot();
            }

            SanitizeSlot(slot);
            slots[i] = slot;
        }

        if (!compactSlots)
        {
            return;
        }

        List<WeaponSlot> compacted = new List<WeaponSlot>(slots.Count);
        bool meleeIncluded = false;
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null || slot.type == WeaponType.None)
            {
                continue;
            }

            if (slot.type == WeaponType.Melee)
            {
                if (meleeIncluded)
                {
                    continue;
                }

                meleeIncluded = true;
            }

            compacted.Add(slot);
        }

        if (compacted.Count == 0)
        {
            compacted.Add(new WeaponSlot { type = WeaponType.Melee, count = -1 });
        }

        while (compacted.Count < 4)
        {
            compacted.Add(new WeaponSlot { type = WeaponType.None, count = -1 });
        }

        slots.Clear();
        slots.AddRange(compacted);
    }

    private static void SanitizeSlot(WeaponSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        switch (slot.type)
        {
            case WeaponType.Melee:
                slot.count = -1;
                slot.equippedPotion = null;
                return;
            case WeaponType.PotionBomb:
            {
                if (slot.equippedPotion == null)
                {
                    ClearSlot(slot);
                    return;
                }

                int qty = Mathf.Max(0, slot.equippedPotion.quantity);
                if (qty <= 0)
                {
                    ClearSlot(slot);
                    return;
                }

                slot.count = qty;
                return;
            }
            default:
                ClearSlot(slot);
                return;
        }
    }

    private static void ClearSlot(WeaponSlot slot)
    {
        slot.equippedPotion = null;
        slot.specificPrefab = null;
        slot.count = -1;
        slot.type = WeaponType.None;
    }

    private int CountUsableSlots()
    {
        int count = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            WeaponSlot slot = slots[i];
            if (slot == null || slot.type == WeaponType.None)
            {
                continue;
            }

            if (slot.type == WeaponType.PotionBomb && (slot.equippedPotion == null || slot.equippedPotion.quantity <= 0))
            {
                continue;
            }

            count++;
        }

        return count;
    }
}
