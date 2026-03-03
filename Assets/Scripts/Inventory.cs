using UnityEngine;
using System.Collections.Generic;

public class Inventory : Singleton<Inventory>
{
    [SerializeField] public int slotPerMaterialPage = 6;
    [SerializeField] public int slotPerPotionPage   = 5;
    public event System.Action Changed;

    private readonly List<Item> items = new();
    private readonly List<Potion> potions   = new();

    public int currentMaterialPage = 0;
    public int currentPotionPage   = 0;

    public int CurrentMaterialPage => currentMaterialPage;
    public int CurrentPotionPage   => currentPotionPage;

    public int MaxMaterialPage => Mathf.CeilToInt((float)items.Count / slotPerMaterialPage);
    public int MaxPotionPage   => Mathf.CeilToInt((float)potions.Count / slotPerPotionPage);

    public int SelectedIndex { get; private set; } = -1;

    public List<Item> MaterialItems => items;
    public List<Potion> PotionItems   => potions;

    public bool AddItem(ItemData itemData, int quantity = 1)
    {
        return AddEntry(
            items,
            itemData,
            quantity,
            ref currentMaterialPage,
            slotPerMaterialPage,
            (item, data) => item.data == data,
            item => item.quantity,
            (item, value) => item.quantity = value,
            data => data.isStackable,
            data => data.maxStack,
            (data, amount) => new Item(data, amount)
        );

    }

    public bool AddPotion(PotionData potionData, int quantity = 1)
    {
        return AddEntry(
            potions,
            potionData,
            quantity,
            ref currentPotionPage,
            slotPerPotionPage,
            (potion, data) => AreSamePotionData(potion.data, data),
            potion => potion.quantity,
            (potion, value) => potion.quantity = value,
            data => data.isStackable,
            data => data.maxStack,
            (data, amount) => new Potion(data, amount)
        );

    }

    private static bool AreSamePotionData(PotionData existing, PotionData incoming)
    {
        if (existing == incoming) return true;
        if (existing == null || incoming == null) return false;

        // Crafted potions are runtime ScriptableObject instances, so compare recipe outcome fields.
        return string.Equals(existing.GetDisplayName(), incoming.GetDisplayName(), System.StringComparison.Ordinal)
            && existing.temperature == incoming.temperature
            && existing.damage1 == incoming.damage1
            && existing.damage2 == incoming.damage2
            && existing.bulletType1 == incoming.bulletType1
            && existing.bulletType2 == incoming.bulletType2
            && string.Equals(existing.GetPhase(0)?.ingredientId ?? string.Empty, incoming.GetPhase(0)?.ingredientId ?? string.Empty, System.StringComparison.Ordinal)
            && string.Equals(existing.GetPhase(1)?.ingredientId ?? string.Empty, incoming.GetPhase(1)?.ingredientId ?? string.Empty, System.StringComparison.Ordinal);
    }

    public List<Item> GetCurrentItems()
    {
        return GetCurrentPageSlice(items, currentMaterialPage, slotPerMaterialPage);
    }

    public List<Potion> GetCurrentPotions()
    {
        return GetCurrentPageSlice(potions, currentPotionPage, slotPerPotionPage);
    }

    public List<Potion> GetCurrentPotionss()
    {
        return GetCurrentPotions();
    }

    public void NextItemPage()
    {
        int prev = currentMaterialPage;
        NextPage(ref currentMaterialPage, items.Count, slotPerMaterialPage);
        if (prev != currentMaterialPage) NotifyChanged();
    }

    public void NextPotionPage()
    {
        int prev = currentPotionPage;
        NextPage(ref currentPotionPage, potions.Count, slotPerPotionPage);
        if (prev != currentPotionPage) NotifyChanged();
    }

    public void SetItemPage(int page)
    {
        int prev = currentMaterialPage;
        SetPage(ref currentMaterialPage, page, items.Count, slotPerMaterialPage);
        if (prev != currentMaterialPage) NotifyChanged();
    }

    public void SetPotionPage(int page)
    {
        int prev = currentPotionPage;
        SetPage(ref currentPotionPage, page, potions.Count, slotPerPotionPage);
        if (prev != currentPotionPage) NotifyChanged();
    }

    private bool AddEntry<TEntry, TData>(
        List<TEntry> targetList,
        TData data,
        int quantity,
        ref int currentPage,
        int slotsPerPage,
        System.Func<TEntry, TData, bool> isSameData,
        System.Func<TEntry, int> getQuantity,
        System.Action<TEntry, int> setQuantity,
        System.Func<TData, bool> isStackable,
        System.Func<TData, int> getMaxStack,
        System.Func<TData, int, TEntry> createEntry)
    {
        if (data == null || quantity <= 0) return false;

        int remaining = quantity;
        int maxStack = getMaxStack(data);

        if (isStackable(data))
        {
            foreach (TEntry entry in targetList)
            {
                if (!isSameData(entry, data) || getQuantity(entry) >= maxStack)
                {
                    continue;
                }

                int addAmount = Mathf.Min(remaining, maxStack - getQuantity(entry));
                setQuantity(entry, getQuantity(entry) + addAmount);
                remaining -= addAmount;

                if (remaining <= 0)
                {
                    NotifyChanged();
                    return true;
                }
            }
        }

        while (remaining > 0)
        {
            int addNow = isStackable(data) ? Mathf.Min(remaining, maxStack) : 1;
            targetList.Add(createEntry(data, addNow));
            remaining -= addNow;
        }

        ClampPage(ref currentPage, targetList.Count, slotsPerPage);
        NotifyChanged();
        return true;
    }

    private List<TEntry> GetCurrentPageSlice<TEntry>(List<TEntry> source, int currentPage, int slotsPerPage)
    {
        List<TEntry> pageEntries = new List<TEntry>();
        int startIndex = currentPage * slotsPerPage;
        int endIndex = Mathf.Min(startIndex + slotsPerPage, source.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            pageEntries.Add(source[i]);
        }

        return pageEntries;
    }

    private static int GetMaxPage(int totalCount, int slotsPerPage)
    {
        return Mathf.Max(1, Mathf.CeilToInt((float)totalCount / slotsPerPage));
    }

    private static void NextPage(ref int currentPage, int totalCount, int slotsPerPage)
    {
        int maxPage = GetMaxPage(totalCount, slotsPerPage);
        currentPage++;
        if (currentPage >= maxPage)
        {
            currentPage = 0;
        }
    }

    private static void SetPage(ref int currentPage, int page, int totalCount, int slotsPerPage)
    {
        int maxPage = GetMaxPage(totalCount, slotsPerPage);
        currentPage = Mathf.Clamp(page, 0, maxPage - 1);
    }

    private static void ClampPage(ref int currentPage, int totalCount, int slotsPerPage)
    {
        int maxPage = GetMaxPage(totalCount, slotsPerPage);
        if (currentPage >= maxPage)
        {
            currentPage = maxPage - 1;
        }
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    public bool RemoveItem(int index, int quantity = 1)
    {
        if (index < 0 || index >= items.Count || quantity <= 0) return false;

        items[index].quantity -= quantity;
        if (items[index].quantity <= 0)
        {
            items.RemoveAt(index);
        }

        ClampPage(ref currentMaterialPage, items.Count, slotPerMaterialPage);
        NotifyChanged();
        return true;
    }

    public bool RemoveItem(Item item, int quantity = 1)
    {
        if (item == null) return false;
        int index = items.IndexOf(item);
        if (index < 0) return false;

        if (quantity <= 0)
        {
            items.RemoveAt(index);
            ClampPage(ref currentMaterialPage, items.Count, slotPerMaterialPage);
            NotifyChanged();
            return true;
        }

        return RemoveItem(index, quantity);
    }

    public bool RemovePotion(int index, int quantity = 1)
    {
        if (index < 0 || index >= potions.Count || quantity <= 0) return false;

        potions[index].quantity -= quantity;
        if (potions[index].quantity <= 0)
        {
            potions.RemoveAt(index);
        }

        ClampPage(ref currentPotionPage, potions.Count, slotPerPotionPage);
        NotifyChanged();
        return true;
    }

    public bool RemovePotion(Potion potion, int quantity = 1)
    {
        if (potion == null) return false;
        int index = potions.IndexOf(potion);
        if (index < 0) return false;

        if (quantity <= 0)
        {
            potions.RemoveAt(index);
            ClampPage(ref currentPotionPage, potions.Count, slotPerPotionPage);
            NotifyChanged();
            return true;
        }

        return RemovePotion(index, quantity);
    }

    public bool RemovePotionCompletely(Potion potion)
    {
        return RemovePotion(potion, 0);
    }

    public bool RemoveItemCompletely(Item item)
    {
        return RemoveItem(item, 0);
    }

    public bool ContainsPotion(Potion potion)
    {
        return potion != null && potions.Contains(potion);
    }

    public bool ContainsItem(Item item)
    {
        return item != null && items.Contains(item);
    }

    public void MarkDirty()
    {
        NotifyChanged();
    }

    public void SelectItem(int index)
    {

        if (index < 0 || index >= items.Count)
        {
            SelectedIndex = -1;
        }
        else
        {
            SelectedIndex = index;
        }
    }
}
