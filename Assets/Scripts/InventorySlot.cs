using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image slotBackground;
    [SerializeField] private Button button;
    [SerializeField] private Image clickArea;
    
    private InventoryUI inventoryUI;  
    private Item currentItem;
    
    public int SlotIndex { get; set; }
    private Potion currentPotion;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.enabled = false;
        }

        EnsureClickArea();
    }

    public void Init(InventoryUI ui, int index)
    {
        inventoryUI = ui;
        SlotIndex = index;
    }

    public void OnClick()
    {
        inventoryUI.OnMaterialSlotClicked(SlotIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        OnClick();
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

    private void EnsureClickArea()
    {
        if (clickArea == null)
        {
            clickArea = GetComponent<Image>();
        }

        if (clickArea == null)
        {
            clickArea = gameObject.AddComponent<Image>();
        }

        clickArea.color = new Color(1f, 1f, 1f, 0f);
        clickArea.raycastTarget = true;
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
