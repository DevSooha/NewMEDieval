using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private CraftUI craftUI;
    
    [SerializeField] private Transform materialContainer;
    [SerializeField] private Button materialPageButton;
 
    [SerializeField] private Transform potionContainer;
    [SerializeField] private Button potionPageButton;
    
    [SerializeField] private GameObject itemSlotPrefab;
    [SerializeField] private GameObject potionSlotPrefab;
    [SerializeField] private TextMeshProUGUI materialPageText;
    [SerializeField] private TextMeshProUGUI potionPageText;

    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI tooltipText;
    
    private InventorySlot[] materialSlots;
    private PotionSlot[] potionSlots;

    private void Awake()
    {   
        
        InitializeMaterialSlots();
        InitializePotionSlots();
        
        materialPageButton.onClick.AddListener(() => NextMaterialPage());
        potionPageButton.onClick.AddListener(() => NextPotionPage());
        
        HideTooltip();
        RefreshUI();
    }

    private void FixedUpdate()
    {
        RefreshUI();
    }

    private void InitializeMaterialSlots()
    {
        materialSlots = new InventorySlot[inventory.slotPerMaterialPage];
        
        for (int i = 0; i < inventory.slotPerMaterialPage; i++)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, materialContainer);
            InventorySlot slot = slotObj.GetComponent<InventorySlot>();
            slot.Init(this, i); // true = 재료 슬롯
            materialSlots[i] = slot;
        }
    }

    private void InitializePotionSlots()
    {
        potionSlots = new PotionSlot[inventory.slotPerPotionPage];
        
        for (int i = 0; i < inventory.slotPerPotionPage; i++)
        {
            GameObject slotObj = Instantiate(potionSlotPrefab, potionContainer);
            PotionSlot slot = slotObj.GetComponent<PotionSlot>();
            slot.Init(this, i); // false = 포션 슬롯
            potionSlots[i] = slot;
        }
    }

    private void NextMaterialPage()
    {
        inventory.NextItemPage();
        RefreshUI();
    }

    private void NextPotionPage()
    {
        inventory.NextPotionPage();
        RefreshUI();
    }

    // 재료 슬롯만 클릭 처리
    public void OnMaterialSlotClicked(int localIndex)
    {
        List<Item> items = inventory.MaterialItems;
        int globalIndex = inventory.currentMaterialPage * inventory.slotPerMaterialPage + localIndex;
        
        if (globalIndex < 0 || globalIndex >= items.Count)
            return;
        
        Item selectedItem = items[globalIndex];
        if (selectedItem == null || selectedItem.data == null)
            return;
        
        if (craftUI != null)
        {
            craftUI.OnMaterialSelected(selectedItem);
        }
    }

    // 포션 슬롯 클릭 (아무 동작 안함)
    public void OnPotionSlotClicked(int localIndex)
    {
        // 포션은 클릭해도 아무 일 없음
    }

    public void RefreshUI()
    {
        RefreshMaterialUI();
        RefreshPotionUI();
    }

    private void RefreshMaterialUI()
    {
        List<Item> currentPageItems = inventory.GetCurrentItems();
        
        for (int i = 0; i < materialSlots.Length; i++)
        {
            if (i < currentPageItems.Count)
                materialSlots[i].SetItem(currentPageItems[i]);
            else
                materialSlots[i].Clear();
        }
        
        int maxPage = Mathf.Max(1, inventory.MaxMaterialPage);
        materialPageText.text = $"{inventory.CurrentMaterialPage + 1} / {maxPage}";
    }

    private void RefreshPotionUI()
    {
        List<Potion> currentPagePotions = inventory.GetCurrentPotionss();
        
        for (int i = 0; i < potionSlots.Length; i++)
        {
            if (i < currentPagePotions.Count)
                potionSlots[i].SetPotion(currentPagePotions[i]);
            else
                potionSlots[i].Clear();
        }
        
        int maxPage = Mathf.Max(1, inventory.MaxPotionPage);
        potionPageText.text = $"{inventory.CurrentPotionPage + 1} / {maxPage}";
    }
    public void ShowPotionTooltip(Potion potion, Vector3 position)
    {
        if (potion == null || potion.data == null)
        {
            HideTooltip();
            return;
        }
        
        tooltipText.text = $"{potion.data.potionName}\n" +
                       $"Damage 1: {potion.data.damage1}\n" +
                       $"Damage 2: {potion.data.damage2}\n" +
                       $"Bullet Type 1: {potion.data.bulletType1}\n" +
                       $"Bullet Type 2: {potion.data.bulletType2}\n" +
                       $"Element 1: {potion.data.element1}\n" +
                       $"Element 2: {potion.data.element2}";
    
    tooltipPanel.SetActive(true);
    tooltipPanel.transform.position = position;
    }
    
    public void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
}
