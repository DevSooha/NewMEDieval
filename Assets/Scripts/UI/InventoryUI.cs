using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private CraftUI craftUI;
    [SerializeField] private PlayerAttackSystem attackSystem;

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
    [SerializeField] private Vector2 tooltipPadding = new Vector2(12f, 10f);
    [SerializeField] private float tooltipMaxWidth = 260f;
    [SerializeField] private float tooltipScreenPadding = 8f;

    private InventorySlot[] materialSlots;
    private PotionSlot[] potionSlots;

    [Header("Weapon Slot Select")]
    [SerializeField] private RectTransform weaponSlotRoot;
    [SerializeField] private RectTransform potionInventoryRoot;
    [SerializeField] private Image weaponSlotDimmer;
    [SerializeField] private float dimmerAlpha = 0.55f;

    private Potion pendingPotion;
    private int pendingPotionIndex = -1;
    private bool isSelectingWeaponSlot = false;

    private void Start()
    {
        InitializeMaterialSlots();
        InitializePotionSlots();

        materialPageButton.onClick.AddListener(() => NextMaterialPage());
        potionPageButton.onClick.AddListener(() => NextPotionPage());

        HideTooltip();
        RefreshUI();
        EnsureWeaponSlotDimmer();
        DisableContainerRaycastTargets();
    }

    private void OnDisable()
    {
        CancelWeaponSlotSelection();
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
            slot.Init(this, i);
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
            slot.Init(this, i);
            potionSlots[i] = slot;
        }
    }

    private void NextMaterialPage()
    {
        TurnPage(inventory.NextItemPage);
    }

    private void NextPotionPage()
    {
        TurnPage(inventory.NextPotionPage);
    }

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

    public void OnPotionSlotClicked(int localIndex)
    {
        List<Potion> potions = inventory.PotionItems;
        int globalIndex = inventory.currentPotionPage * inventory.slotPerPotionPage + localIndex;

        if (globalIndex < 0 || globalIndex >= potions.Count)
            return;

        Potion selectedPotion = potions[globalIndex];
        if (selectedPotion == null || selectedPotion.data == null)
            return;

        if (isSelectingWeaponSlot && pendingPotionIndex == globalIndex)
        {
            CancelWeaponSlotSelection();
            return;
        }

        BeginWeaponSlotSelection(selectedPotion, globalIndex);
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
        EnsureTooltipLayout();
        tooltipPanel.transform.SetAsLastSibling();

        RectTransform panelRect = tooltipPanel.GetComponent<RectTransform>();
        Canvas canvas = tooltipPanel.GetComponentInParent<Canvas>();
        if (panelRect != null && canvas != null)
        {
            RectTransform canvasRect = canvas.transform as RectTransform;
            if (panelRect.parent != canvas.transform)
            {
                panelRect.SetParent(canvas.transform, false);
            }

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, position, cam, out Vector2 localPoint))
            {
                Vector2 size = panelRect.sizeDelta;
                Vector2 canvasSize = canvasRect.rect.size;

                float pad = Mathf.Max(0f, tooltipScreenPadding);
                float minX = -canvasSize.x * 0.5f + pad;
                float maxX = canvasSize.x * 0.5f - pad - size.x;
                float minY = -canvasSize.y * 0.5f + pad + size.y;
                float maxY = canvasSize.y * 0.5f - pad;

                localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
                localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

                panelRect.anchoredPosition = localPoint;
            }
        }
        else
        {
            tooltipPanel.transform.position = position;
        }
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private void EnsureTooltipLayout()
    {
        if (tooltipPanel == null || tooltipText == null) return;

        RectTransform panelRect = tooltipPanel.GetComponent<RectTransform>();
        RectTransform textRect = tooltipText.rectTransform;
        if (panelRect == null || textRect == null) return;

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 1f);

        float maxWidth = tooltipMaxWidth > 0f ? tooltipMaxWidth : 260f;
        Vector2 preferred = tooltipText.GetPreferredValues(tooltipText.text, maxWidth, 0f);
        Vector2 size = new Vector2(preferred.x + (tooltipPadding.x * 2f), preferred.y + (tooltipPadding.y * 2f));
        panelRect.sizeDelta = size;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(tooltipPadding.x, tooltipPadding.y);
        textRect.offsetMax = new Vector2(-tooltipPadding.x, -tooltipPadding.y);
    }

    private void BeginWeaponSlotSelection(Potion potion, int potionIndex)
    {
        pendingPotion = potion;
        pendingPotionIndex = potionIndex;
        isSelectingWeaponSlot = true;
        SetWeaponSlotDimmed(true);
        BringSelectionToFront();
    }

    private void CancelWeaponSlotSelection()
    {
        pendingPotion = null;
        pendingPotionIndex = -1;
        isSelectingWeaponSlot = false;
        SetWeaponSlotDimmed(false);
    }

    public void OnWeaponSlotClicked(int slotIndex)
    {
        if (!isSelectingWeaponSlot) return;

        if (attackSystem == null)
        {
            attackSystem = FindFirstObjectByType<PlayerAttackSystem>(FindObjectsInactive.Include);
        }

        if (attackSystem == null || attackSystem.slots == null || attackSystem.slots.Count <= slotIndex)
        {
            CancelWeaponSlotSelection();
            return;
        }

        WeaponSlot slot = attackSystem.slots[slotIndex];

        if (slot.equippedPotion != null)
        {
            if (inventory != null)
            {
                if (!inventory.PotionItems.Contains(slot.equippedPotion))
                {
                    inventory.AddPotion(slot.equippedPotion.data, slot.equippedPotion.quantity);
                }
            }

            slot.equippedPotion = null;
            slot.count = -1;
            slot.type = slotIndex == 0 ? WeaponType.Melee : WeaponType.None;
            slot.specificPrefab = null;
        }

        if (pendingPotion != null)
        {
            slot.type = WeaponType.PotionBomb;
            slot.equippedPotion = pendingPotion;
            slot.count = pendingPotion.quantity;
            slot.specificPrefab = null;

            if (inventory != null)
            {
                inventory.PotionItems.Remove(pendingPotion);
            }
        }

        attackSystem.slots[slotIndex] = slot;
        CancelWeaponSlotSelection();
        RefreshUI();
    }

    private void EnsureWeaponSlotDimmer()
    {
        if (weaponSlotDimmer != null) return;

        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        if (weaponSlotRoot == null)
        {
            WeaponSlotUI ui = FindFirstObjectByType<WeaponSlotUI>(FindObjectsInactive.Include);
            if (ui != null) weaponSlotRoot = ui.transform as RectTransform;
        }

        if (potionInventoryRoot == null)
        {
            TryResolvePotionInventoryRoot();
        }

        GameObject dimObj = new GameObject("WeaponSlotDimmer", typeof(RectTransform), typeof(Image));
        dimObj.transform.SetParent(canvas.transform, false);

        weaponSlotDimmer = dimObj.GetComponent<Image>();
        weaponSlotDimmer.color = new Color(0f, 0f, 0f, dimmerAlpha);
        weaponSlotDimmer.raycastTarget = true;

        RectTransform rect = dimObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        weaponSlotDimmer.gameObject.SetActive(false);
    }

    private void SetWeaponSlotDimmed(bool active)
    {
        EnsureWeaponSlotDimmer();
        if (weaponSlotDimmer == null) return;

        weaponSlotDimmer.gameObject.SetActive(active);
        weaponSlotDimmer.raycastTarget = false;
        if (active)
        {
            weaponSlotDimmer.transform.SetAsLastSibling();
            if (weaponSlotRoot != null)
            {
                weaponSlotRoot.SetAsLastSibling();
            }
            if (potionInventoryRoot != null)
            {
                potionInventoryRoot.SetAsLastSibling();
            }
        }
    }

    private void BringSelectionToFront()
    {
        if (weaponSlotDimmer != null && weaponSlotDimmer.gameObject.activeSelf)
        {
            weaponSlotDimmer.transform.SetAsLastSibling();
        }

        if (potionInventoryRoot == null)
        {
            TryResolvePotionInventoryRoot();
        }

        if (potionInventoryRoot != null)
        {
            potionInventoryRoot.SetAsLastSibling();
        }

        if (weaponSlotRoot != null)
        {
            weaponSlotRoot.SetAsLastSibling();
        }
    }

    private void DisableContainerRaycastTargets()
    {
        DisableRaycastOnImage(materialContainer);
        DisableRaycastOnImage(potionContainer);

        TryResolvePotionInventoryRoot();

        if (potionInventoryRoot != null)
        {
            DisableRaycastOnImage(potionInventoryRoot);
        }

        Transform materialRoot = transform.Find("MaterialInventory");
        if (materialRoot != null)
        {
            DisableRaycastOnImage(materialRoot);
        }
    }

    private void DisableRaycastOnImage(Transform target)
    {
        if (target == null) return;
        Image img = target.GetComponent<Image>();
        if (img != null)
        {
            img.raycastTarget = false;
        }
    }

    private void TurnPage(System.Action changePageAction)
    {
        if (changePageAction == null)
        {
            return;
        }

        changePageAction.Invoke();
        RefreshUI();
    }

    private bool TryResolvePotionInventoryRoot()
    {
        if (potionInventoryRoot != null)
        {
            return true;
        }

        Transform found = transform.Find("PotionInventory");
        if (found != null)
        {
            potionInventoryRoot = found as RectTransform;
        }

        return potionInventoryRoot != null;
    }
}
