using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PotionSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image topIMG;
    [SerializeField] private Image bottomIMG;
    [SerializeField] private Image frame;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image clickArea;
    
    private InventoryUI inventoryUI;  
    private Potion currentPotion;
    
    public int SlotIndex { get; set; }

    public void Init(InventoryUI ui, int index)
    {
        inventoryUI = ui;
        SlotIndex = index;
        EnsureClickArea();
    }

    public void OnClick()
    {
        if (inventoryUI != null)
        {
            inventoryUI.OnPotionSlotClicked(SlotIndex);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        OnClick();
    }

    public void SetPotion(Potion potion)
    {
        currentPotion = potion;
        
        if (potion != null && potion.data != null)
        {
            if (potion.data.topIMG != null && potion.data.topIMG != null)
            {
                topIMG.sprite = potion.data.topIMG;
                topIMG.enabled = true;
            }
            else
            {
                topIMG.enabled = false;
            }
            
            if (potion.data.bottomIMG != null && potion.data.bottomIMG != null)
            {
                bottomIMG.sprite = potion.data.bottomIMG;
                bottomIMG.enabled = true;
            }
            else
            {
                bottomIMG.enabled = false;
            }
            
            frame.enabled = true;
            
            if (potion.data.isStackable && potion.quantity > 1)
            {
                quantityText.text = potion.quantity.ToString();
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
        currentPotion = null;
        topIMG.enabled = false;
        bottomIMG.enabled = false;
        frame.enabled = false;
        quantityText.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentPotion != null && inventoryUI != null)
        {
            inventoryUI.ShowPotionTooltip(currentPotion, eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryUI != null)
        {
            inventoryUI.HideTooltip();
        }
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
}
