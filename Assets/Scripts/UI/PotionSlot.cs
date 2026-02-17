using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PotionSlot : MonoBehaviour
{
    [SerializeField] private Image topIMG;
    [SerializeField] private Image bottomIMG;
    [SerializeField] private Image frame;
    [SerializeField] private TextMeshProUGUI quantityText;
    
    private InventoryUI inventoryUI;  
    private Potion currentPotion;
    
    public int SlotIndex { get; set; }

    public void Init(InventoryUI ui, int index)
    {
        inventoryUI = ui;
        SlotIndex = index;
    }

    public void OnClick()
    {
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
    
    // 마우스 올렸을 때 호출될 메서드
    public void OnMouseEnter()
    {
        if (currentPotion != null && inventoryUI != null)
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

