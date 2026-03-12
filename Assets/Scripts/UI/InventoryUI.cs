using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [SerializeField] private Vector2 tooltipAnchorOffset = new Vector2(-3f, 3f);

    private InventorySlot[] materialSlots;
    private PotionSlot[] potionSlots;

    [Header("Weapon Slot Select")]
    [SerializeField] private RectTransform weaponSlotRoot;
    [SerializeField] private RectTransform potionInventoryRoot;
    [SerializeField] private Image weaponSlotDimmer;
    [SerializeField] private float dimmerAlpha = 0.70f;

    private Potion pendingPotion;
    private int pendingPotionIndex = -1;
    private bool isSelectingWeaponSlot = false;
    private int pendingUnequipSlotIndex = -1;
    private float pendingUnequipExpireTime;
    private const float UnequipConfirmWindowSeconds = 1f;
    private bool slotsInitialized;
    private Inventory subscribedInventory;

    private void Start()
    {
        EnsureInitialized();
        if (!slotsInitialized)
        {
            return;
        }

        HideTooltip();
        RefreshUI();
        EnsureWeaponSlotDimmer();
        DisableContainerRaycastTargets();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        if (!slotsInitialized)
        {
            return;
        }

        SyncInventorySubscription();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
        CancelWeaponSlotSelection();
    }

    private void Update()
    {
        if (pendingUnequipSlotIndex >= 0 && Time.unscaledTime > pendingUnequipExpireTime)
        {
            ClearPendingUnequip();
        }

        if (!isSelectingWeaponSlot)
        {
            return;
        }

        if (Input.GetMouseButtonDown(1) && !IsPointerOverWeaponSlot())
        {
            CancelWeaponSlotSelection();
        }
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
        if (inventory == null) return;
        TurnPage(inventory.NextItemPage);
    }

    private void NextPotionPage()
    {
        if (inventory == null) return;
        TurnPage(inventory.NextPotionPage);
    }

    private void EnsureInitialized()
    {
        EnsureRuntimeReferences();
        SyncInventorySubscription();

        if (!slotsInitialized)
        {
            if (inventory == null || materialContainer == null || potionContainer == null || itemSlotPrefab == null || potionSlotPrefab == null)
            {
                return;
            }

            InitializeMaterialSlots();
            InitializePotionSlots();
            slotsInitialized = true;
        }

        BindPageButtons();
    }

    private void EnsureRuntimeReferences()
    {
        if (inventory == null)
        {
            inventory = Inventory.Instance;
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
            }
        }

        if (craftUI == null)
        {
            craftUI = FindFirstObjectByType<CraftUI>(FindObjectsInactive.Include);
        }

        if (materialContainer == null)
        {
            Transform found = FindChildRecursive(transform, "MaterialContent");
            if (found != null) materialContainer = found;
        }

        if (potionContainer == null)
        {
            Transform found = FindChildRecursive(transform, "PotionContent");
            if (found != null) potionContainer = found;
        }

        if (materialPageButton == null)
        {
            Transform found = FindChildRecursive(transform, "MIButton");
            if (found != null) materialPageButton = found.GetComponent<Button>();
        }

        if (potionPageButton == null)
        {
            Transform found = FindChildRecursive(transform, "PIButton");
            if (found != null) potionPageButton = found.GetComponent<Button>();
        }

        if (materialPageText == null)
        {
            Transform found = FindChildRecursive(transform, "MIPageCount");
            if (found != null) materialPageText = found.GetComponent<TextMeshProUGUI>();
        }

        if (potionPageText == null)
        {
            Transform found = FindChildRecursive(transform, "PIPageCount");
            if (found != null) potionPageText = found.GetComponent<TextMeshProUGUI>();
        }

        if (weaponSlotRoot == null)
        {
            WeaponSlotUI ui = FindFirstObjectByType<WeaponSlotUI>(FindObjectsInactive.Include);
            if (ui != null) weaponSlotRoot = ui.transform as RectTransform;
        }
    }

    private void SyncInventorySubscription()
    {
        if (inventory == subscribedInventory)
        {
            return;
        }

        UnsubscribeFromInventory();

        if (inventory != null)
        {
            inventory.Changed += HandleInventoryChanged;
            subscribedInventory = inventory;
        }
    }

    private void UnsubscribeFromInventory()
    {
        if (subscribedInventory == null)
        {
            return;
        }

        subscribedInventory.Changed -= HandleInventoryChanged;
        subscribedInventory = null;
    }

    private void HandleInventoryChanged()
    {
        RefreshUI();
    }

    private void BindPageButtons()
    {
        if (materialPageButton != null)
        {
            materialPageButton.onClick.RemoveListener(NextMaterialPage);
            materialPageButton.onClick.AddListener(NextMaterialPage);
        }

        if (potionPageButton != null)
        {
            potionPageButton.onClick.RemoveListener(NextPotionPage);
            potionPageButton.onClick.AddListener(NextPotionPage);
        }
    }

    public void OnMaterialSlotClicked(int localIndex)
    {
        if (inventory == null) return;

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

    public void OnMaterialSlotClicked(int localIndex, PointerEventData.InputButton button)
    {
        if (button != PointerEventData.InputButton.Right)
        {
            return;
        }

        OnMaterialSlotClicked(localIndex);
    }

    public void OnPotionSlotClicked(int localIndex)
    {
        OnPotionSlotClicked(localIndex, PointerEventData.InputButton.Right);
    }

    public void OnPotionSlotClicked(int localIndex, PointerEventData.InputButton button)
    {
        if (button != PointerEventData.InputButton.Right)
        {
            return;
        }

        if (inventory == null) return;

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

        ClearPendingUnequip();
        BeginWeaponSlotSelection(selectedPotion, globalIndex);
    }

    public void RefreshUI()
    {
        if (!slotsInitialized || inventory == null) return;

        RefreshMaterialUI();
        RefreshPotionUI();
    }

    private void RefreshMaterialUI()
    {
        if (materialSlots == null) return;

        List<Item> currentPageItems = inventory.GetCurrentItems();

        for (int i = 0; i < materialSlots.Length; i++)
        {
            if (i < currentPageItems.Count)
                materialSlots[i].SetItem(currentPageItems[i]);
            else
                materialSlots[i].Clear();
        }

        int maxPage = Mathf.Max(1, inventory.MaxMaterialPage);
        if (materialPageText != null)
        {
            materialPageText.text = $"{inventory.CurrentMaterialPage + 1} / {maxPage}";
        }
    }

    private void RefreshPotionUI()
    {
        if (potionSlots == null) return;

        List<Potion> currentPagePotions = inventory.GetCurrentPotionss();

        for (int i = 0; i < potionSlots.Length; i++)
        {
            if (i < currentPagePotions.Count)
                potionSlots[i].SetPotion(currentPagePotions[i]);
            else
                potionSlots[i].Clear();
        }

        int maxPage = Mathf.Max(1, inventory.MaxPotionPage);
        if (potionPageText != null)
        {
            potionPageText.text = $"{inventory.CurrentPotionPage + 1} / {maxPage}";
        }
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
        PositionTooltipAt(position);
    }

    public void ShowMaterialTooltip(Item item, Vector3 position)
    {
        if (item == null || item.data == null)
        {
            HideTooltip();
            return;
        }

        ItemData data = item.data;
        string nameText = data.GetIngredientId();
        if (!string.IsNullOrWhiteSpace(data.topName) || !string.IsNullOrWhiteSpace(data.bottomName))
        {
            nameText = $"{data.topName}{data.bottomName}".Trim();
        }

        string description = string.IsNullOrWhiteSpace(data.description) ? "No description." : data.description;
        tooltipText.text = $"{nameText}\n" +
                           $"Element: {data.element}\n" +
                           $"Count: {item.quantity}\n" +
                           description;

        tooltipPanel.SetActive(true);
        EnsureTooltipLayout();
        tooltipPanel.transform.SetAsLastSibling();
        PositionTooltipAt(position);
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
        panelRect.pivot = new Vector2(0f, 0f);

        float maxWidth = tooltipMaxWidth > 0f ? tooltipMaxWidth : 260f;
        Vector2 preferred = tooltipText.GetPreferredValues(tooltipText.text, maxWidth, 0f);
        Vector2 size = new Vector2(preferred.x + (tooltipPadding.x * 2f), preferred.y + (tooltipPadding.y * 2f));
        panelRect.sizeDelta = size;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(tooltipPadding.x, tooltipPadding.y);
        textRect.offsetMax = new Vector2(-tooltipPadding.x, -tooltipPadding.y);
    }

    private void PositionTooltipAt(Vector3 screenPosition)
    {
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
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, cam, out Vector2 localPoint))
            {
                localPoint += tooltipAnchorOffset;

                Vector2 size = panelRect.sizeDelta;
                Vector2 canvasSize = canvasRect.rect.size;

                float pad = Mathf.Max(0f, tooltipScreenPadding);
                float minX = -canvasSize.x * 0.5f + pad;
                float maxX = canvasSize.x * 0.5f - pad - size.x;
                float minY = -canvasSize.y * 0.5f + pad;
                float maxY = canvasSize.y * 0.5f - pad - size.y;

                localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
                localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

                panelRect.anchoredPosition = localPoint;
            }
        }
        else
        {
            tooltipPanel.transform.position = screenPosition;
        }
    }

    private void BeginWeaponSlotSelection(Potion potion, int potionIndex)
    {
        if (potion == null || potion.data == null)
        {
            return;
        }

        pendingPotion = potion;
        pendingPotionIndex = potionIndex;
        isSelectingWeaponSlot = true;
        ClearPendingUnequip();
        SetWeaponSlotDimmed(true);
        BringSelectionToFront();
    }

    private void CancelWeaponSlotSelection()
    {
        pendingPotion = null;
        pendingPotionIndex = -1;
        isSelectingWeaponSlot = false;
        ClearPendingUnequip();
        SetWeaponSlotDimmed(false);
    }

    public void OnWeaponSlotClicked(int slotIndex)
    {
        OnWeaponSlotClicked(slotIndex, PointerEventData.InputButton.Left);
    }

    public void OnWeaponSlotClicked(int slotIndex, PointerEventData.InputButton button)
    {
        if (attackSystem == null)
        {
            attackSystem = FindFirstObjectByType<PlayerAttackSystem>(FindObjectsInactive.Include);
        }

        if (attackSystem == null || attackSystem.slots == null || attackSystem.slots.Count <= slotIndex)
        {
            CancelWeaponSlotSelection();
            return;
        }

        if (isSelectingWeaponSlot)
        {
            TryEquipPendingPotion(slotIndex);
            return;
        }

        if (button == PointerEventData.InputButton.Right)
        {
            TryHandleSlotUnequip(slotIndex);
        }
    }

    private void TryEquipPendingPotion(int slotIndex)
    {
        if (pendingPotion == null || pendingPotion.data == null)
        {
            CancelWeaponSlotSelection();
            return;
        }

        if (attackSystem == null || attackSystem.slots == null || slotIndex < 0 || slotIndex >= attackSystem.slots.Count)
        {
            return;
        }

        WeaponSlot targetSlot = attackSystem.slots[slotIndex];
        if (targetSlot != null && targetSlot.type != WeaponType.None)
        {
            return;
        }

        bool equipped = attackSystem.TryEquipPotionToSlot(
            pendingPotion,
            slotIndex,
            returnPreviousToInventory: false);
        if (!equipped)
        {
            return;
        }

        ClearPendingUnequip();
        CancelWeaponSlotSelection();
    }

    private void TryHandleSlotUnequip(int slotIndex)
    {
        if (attackSystem == null || attackSystem.slots == null || attackSystem.slots.Count <= slotIndex)
        {
            return;
        }

        WeaponSlot slot = attackSystem.slots[slotIndex];
        if (slot.equippedPotion == null || slot.type != WeaponType.PotionBomb)
        {
            ClearPendingUnequip();
            return;
        }

        bool confirmed = pendingUnequipSlotIndex == slotIndex && Time.unscaledTime <= pendingUnequipExpireTime;
        if (!confirmed)
        {
            pendingUnequipSlotIndex = slotIndex;
            pendingUnequipExpireTime = Time.unscaledTime + UnequipConfirmWindowSeconds;
            return;
        }

        bool unequipped = attackSystem.TryUnequipPotionFromSlot(slotIndex, addBackToInventory: true);
        if (!unequipped)
        {
            return;
        }

        ClearPendingUnequip();
    }

    private void ClearPendingUnequip()
    {
        pendingUnequipSlotIndex = -1;
        pendingUnequipExpireTime = 0f;
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

        float alpha = Mathf.Max(0.7f, dimmerAlpha);
        weaponSlotDimmer.color = new Color(0f, 0f, 0f, alpha);
        weaponSlotDimmer.gameObject.SetActive(active);
        weaponSlotDimmer.raycastTarget = active;
        if (active)
        {
            weaponSlotDimmer.transform.SetAsLastSibling();
            if (weaponSlotRoot != null)
            {
                weaponSlotRoot.SetAsLastSibling();
            }
        }
    }

    private void BringSelectionToFront()
    {
        if (weaponSlotDimmer != null && weaponSlotDimmer.gameObject.activeSelf)
        {
            weaponSlotDimmer.transform.SetAsLastSibling();
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

    private bool IsPointerOverWeaponSlot()
    {
        if (weaponSlotRoot == null || EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, hits);
        for (int i = 0; i < hits.Count; i++)
        {
            GameObject hitObject = hits[i].gameObject;
            if (hitObject == null) continue;

            Transform hitTransform = hitObject.transform;
            if (hitTransform == weaponSlotRoot || hitTransform.IsChildOf(weaponSlotRoot))
            {
                return true;
            }
        }

        return false;
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

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && t.name == childName)
            {
                return t;
            }
        }

        return null;
    }
}
