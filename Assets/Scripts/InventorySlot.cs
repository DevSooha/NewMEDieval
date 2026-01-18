using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image slotBackground;
    
    private InventoryUI inventoryUI;  
    private ItemCategory category;     
    private Item currentItem;
    
    public int SlotIndex { get; set; }


    public void Init(InventoryUI ui, int index, ItemCategory cat)
    {
        inventoryUI = ui;
        SlotIndex = index;
        category = cat; 
    }

    public void OnClick()
    {
        // category와 함께 전달
        inventoryUI.OnSlotClicked(category, SlotIndex);
    }


    public void SetItem(Item item)
    {
        currentItem = item;
        
        if (item != null && item.data != null)
        {
            itemIcon.sprite = item.data.icon;
            itemIcon.enabled = true;
            
            if (item.data.isStackable && item.quantity > 1)
            {
                quantityText.text = item.quantity.ToString();
                quantityText.enabled = true;
            }
            else
            {
                quantityText.enabled = false;
            }
        }
    }

    public void Clear()
    {
        currentItem = null;
        itemIcon.enabled = false;
        quantityText.enabled = false;
    }
    
}
