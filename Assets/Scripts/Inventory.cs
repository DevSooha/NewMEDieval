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

        int remaining = quantity;

        if (itemData.isStackable)
        {
            foreach (Item item in items)
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
        items.Add(new Item(itemData, addNow));
        remaining -= addNow;
    }

        int newMaxPage = Mathf.CeilToInt((float)items.Count / slotPerMaterialPage);
        if (newMaxPage <= 0) newMaxPage = 1;

        if (currentMaterialPage >= newMaxPage)
        {
            currentMaterialPage = newMaxPage - 1;
        }
        return true;
    }
    public bool AddPotion(PotionData potionData, int quantity = 1)
    {

        int remaining = quantity;

        if (potionData.isStackable)
        {
            foreach (Potion potion in potions)
            {
                if (potion.data == potionData && potion.quantity < potionData.maxStack)
                {
                    int addAmount = Mathf.Min(quantity, potionData.maxStack - potion.quantity);
                    potion.quantity += addAmount;
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
        int addNow = potionData.isStackable ? Mathf.Min(remaining, potionData.maxStack) : 1;
        potions.Add(new Potion(potionData, addNow));
        remaining -= addNow;
    }

        int newMaxPage = Mathf.CeilToInt((float)potions.Count / slotPerPotionPage);
        if (newMaxPage <= 0) newMaxPage = 1;

        if (currentPotionPage >= newMaxPage)
        {
            currentPotionPage = newMaxPage - 1;
        }
        return true;
    }
    public List<Item> GetCurrentItems()
    {
        List<Item> pageItems = new List<Item>();
        int startIndex = currentMaterialPage * slotPerMaterialPage;
        int endIndex   = Mathf.Min(startIndex + slotPerMaterialPage, items.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            pageItems.Add(items[i]);
        }

        return pageItems;
    }
    public List<Potion> GetCurrentPotionss()
    {
        List<Potion> pagePotions = new List<Potion>();
        int startIndex = currentPotionPage * slotPerPotionPage;
        int endIndex   = Mathf.Min(startIndex + slotPerPotionPage, potions.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            pagePotions.Add(potions[i]);
        }

        return pagePotions;
    }

    public void NextItemPage()
    {
        int maxPage = Mathf.Max(1, Mathf.CeilToInt((float)items.Count / slotPerMaterialPage));
        currentMaterialPage++;

        if (currentMaterialPage >= maxPage)
            currentMaterialPage = 0;
    }
    public void NextPotionPage()
    {
        int maxPage = Mathf.Max(1, Mathf.CeilToInt((float)potions.Count / slotPerPotionPage));
        currentPotionPage++;
        if (currentPotionPage >= maxPage)
            currentPotionPage = 0;
    }

    public void SetItemPage(int page)
    {
        int maxPage = Mathf.Max(1, Mathf.CeilToInt((float)items.Count / slotPerMaterialPage));
        currentMaterialPage = Mathf.Clamp(page, 0, maxPage - 1);
    }
    public void SetPotionPage(int page)
    {
        int maxPage = Mathf.Max(1, Mathf.CeilToInt((float)potions.Count / slotPerPotionPage));
        currentPotionPage = Mathf.Clamp(page, 0, maxPage - 1);
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
