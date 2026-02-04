using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Inventory : Singleton<Inventory>
{
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private int slotPerMaterialPage = 6;
    [SerializeField] private int slotPerPotionPage   = 5;

    private List<Item> materialItems = new List<Item>();
    private List<Item> potionItems   = new List<Item>();

    private int currentMaterialPage = 0;
    private int currentPotionPage   = 0;

    public int CurrentMaterialPage => currentMaterialPage;
    public int CurrentPotionPage   => currentPotionPage;

    public int MaxMaterialPage => Mathf.CeilToInt((float)materialItems.Count / slotPerMaterialPage);
    public int MaxPotionPage   => Mathf.CeilToInt((float)potionItems.Count / slotPerPotionPage);

    public int SelectedIndex { get; private set; } = -1;

    public List<Item> MaterialItems => materialItems;
    public List<Item> PotionItems   => potionItems;

    // ---------- 공통 헬퍼들 ----------

    private List<Item> GetList(ItemCategory category)
    {
        return category switch
        {
            ItemCategory.Material => materialItems,
            ItemCategory.Potion   => potionItems,
            _                     => materialItems
        };
    }

    private int GetSlotPerPage(ItemCategory category)
    {
        return category switch
        {
            ItemCategory.Material => slotPerMaterialPage,
            ItemCategory.Potion   => slotPerPotionPage,
            _                     => slotPerMaterialPage
        };
    }

    private ref int GetCurrentPageRef(ItemCategory category)
    {
        if (category == ItemCategory.Material)
            return ref currentMaterialPage;
        else
            return ref currentPotionPage;
    }


    public bool AddItem(ItemData itemData, int quantity = 1)
    {
        ItemCategory category = itemData.category;   // 무조건 ItemData 기준

        List<Item> list     = GetList(category);
        int slotPerPage     = GetSlotPerPage(category);
        ref int currentPage = ref GetCurrentPageRef(category);

        int remaining = quantity;

        if (itemData.isStackable)
        {
            foreach (Item item in list)
            {
                if (item.data == itemData && item.quantity < itemData.maxStack)
                {
                    int addAmount = Mathf.Min(quantity, itemData.maxStack - item.quantity);
                    item.quantity += addAmount;
                    remaining -= addAmount;

                    if (remaining <= 0)
                    {
                        inventoryUI.RefreshUI();
                        return true;
                    }
                    
                }
            }
        }
        while (remaining > 0)
    {
        int addNow = itemData.isStackable ? Mathf.Min(remaining, itemData.maxStack) : 1;
        list.Add(new Item(itemData, addNow));
        remaining -= addNow;
    }

        int newMaxPage = Mathf.CeilToInt((float)list.Count / slotPerPage);
        if (newMaxPage <= 0) newMaxPage = 1;

        if (currentPage >= newMaxPage)
        {
            currentPage = newMaxPage - 1;
        }
        return true;
    }
    public List<Item> GetCurrentItems(ItemCategory category)
    {
        List<Item> source   = GetList(category);
        int slotPerPage     = GetSlotPerPage(category);
        int currentPage     = GetCurrentPageRef(category);

        List<Item> pageItems = new List<Item>();
        int startIndex = currentPage * slotPerPage;
        int endIndex   = Mathf.Min(startIndex + slotPerPage, source.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            pageItems.Add(source[i]);
        }

        return pageItems;
    }

    public void NextPage(ItemCategory category)
    {
        List<Item> list      = GetList(category);
        int slotPerPage      = GetSlotPerPage(category);
        ref int currentPage  = ref GetCurrentPageRef(category);

        int maxPage = Mathf.Max(1, Mathf.CeilToInt((float)list.Count / slotPerPage));
        currentPage++;

        if (currentPage >= maxPage)
            currentPage = 0;
    }

    public void SetPage(ItemCategory category, int page)
    {
        List<Item> list      = GetList(category);
        int slotPerPage      = GetSlotPerPage(category);
        ref int currentPage  = ref GetCurrentPageRef(category);

        int maxPage = Mathf.Max(1, Mathf.CeilToInt((float)list.Count / slotPerPage));
        currentPage = Mathf.Clamp(page, 0, maxPage - 1);
    }

    public bool RemoveItem(ItemCategory category, int index, int quantity = 1)
    {
        List<Item> list = GetList(category);
        if (index < 0 || index >= list.Count) return false;

        list[index].quantity -= quantity;
        if (list[index].quantity <= 0)
        {
            list.RemoveAt(index);
        }
        return true;
    }
    public void SelectItem(ItemCategory category, int index)
{
    List<Item> list = GetList(category);

    if (index < 0 || index >= list.Count)
    {
        SelectedIndex = -1;
    }
    else
    {
        SelectedIndex = index;
    }
}
}
