using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class PotionSlot : MonoBehaviour
{
    [SerializeField] private Image topIMG;
    [SerializeField] private Image bottomIMG;
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
            topIMG = potion.data.topIMG;
            bottomIMG = potion.data.bottomIMG;
            topIMG.enabled = true;
            bottomIMG.enabled = true;
            
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
        quantityText.enabled = false;
    }
}
