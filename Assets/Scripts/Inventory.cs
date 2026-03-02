using UnityEngine;
using System.Collections.Generic;

public class Inventory : Singleton<Inventory>
{
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] public int slotPerMaterialPage = 6;
    [SerializeField] public int slotPerPotionPage   = 5;

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
            (potion, data) => potion.data == data,
            potion => potion.quantity,
            (potion, value) => potion.quantity = value,
            data => data.isStackable,
            data => data.maxStack,
            (data, amount) => new Potion(data, amount)
        );

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
        NextPage(ref currentMaterialPage, items.Count, slotPerMaterialPage);
    }

    public void NextPotionPage()
    {
        NextPage(ref currentPotionPage, potions.Count, slotPerPotionPage);
    }

    public void SetItemPage(int page)
    {
        SetPage(ref currentMaterialPage, page, items.Count, slotPerMaterialPage);
    }

    public void SetPotionPage(int page)
    {
        SetPage(ref currentPotionPage, page, potions.Count, slotPerPotionPage);
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

                int addAmount = Mathf.Min(quantity, maxStack - getQuantity(entry));
                setQuantity(entry, getQuantity(entry) + addAmount);
                remaining -= addAmount;

                if (remaining <= 0)
                {
                    RefreshInventoryUi();
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

    private void RefreshInventoryUi()
    {
        if (inventoryUI != null)
        {
            inventoryUI.RefreshUI();
        }
    }

    public bool RemoveItem(int index, int quantity = 1)
    {
        items[index].quantity -= quantity;
        if (items[index].quantity <= 0)
        {
            items.RemoveAt(index);
        }
        return true;
    }
    public bool RemovePotion(int index, int quantity = 1)
    {
        potions[index].quantity -= quantity;
        if (potions[index].quantity <= 0)
        {
            potions.RemoveAt(index);
        }
        return true;
    }

    public bool RemovePotionCompletely(Potion potion)
    {
        if (potion == null) return false;
        return potions.Remove(potion);
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
