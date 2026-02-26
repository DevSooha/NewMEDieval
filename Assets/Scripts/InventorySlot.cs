using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image slotBackground;
    
    private InventoryUI inventoryUI;  
    private Item currentItem;
    
    public int SlotIndex { get; set; }
    private Potion currentPotion;


    public void Init(InventoryUI ui, int index)
    {
        inventoryUI = ui;
        SlotIndex = index;
    }

    public void OnClick()
    {
        inventoryUI.OnMaterialSlotClicked(SlotIndex);
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
    public void OnMouseEnter()
    {
        if (inventoryUI != null)
        {
            inventoryUI.ShowPotionTooltip(currentPotion, transform.position);
        }
    }
    
    // 마우스 벗어났을 때 호출될 메서드
    public void OnMouseExit()
    {
        if (inventoryUI != null)
        {
            inventoryUI.HideTooltip();
        }
    }
}
