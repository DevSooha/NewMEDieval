using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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

        if (itemIcon != null) itemIcon.raycastTarget = false;
        if (slotBackground != null) slotBackground.raycastTarget = false;
        if (quantityText != null) quantityText.raycastTarget = false;

        EnsureClickArea();
    }

    public void Init(InventoryUI ui, int index)
    {
        inventoryUI = ui;
        SlotIndex = index;
    }

    public void OnClick(PointerEventData.InputButton button)
    {
        inventoryUI.OnMaterialSlotClicked(SlotIndex, button);
    }

    // Kept for prefab button event compatibility.
    public void OnClick()
    {
        OnClick(PointerEventData.InputButton.Right);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }
        OnClick(eventData.button);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentItem != null && inventoryUI != null)
        {
            Vector3 anchorPosition = eventData.position;
            RectTransform rect = transform as RectTransform;
            if (rect != null)
            {
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Camera cam = eventData != null ? eventData.enterEventCamera : null;
                anchorPosition = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
            }

            inventoryUI.ShowMaterialTooltip(currentItem, anchorPosition);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryUI != null)
        {
            inventoryUI.HideTooltip();
        }
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
