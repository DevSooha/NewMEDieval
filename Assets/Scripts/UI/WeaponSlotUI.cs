using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WeaponSlotUI : MonoBehaviour
{
    [Header("Auto Find (optional)")]
    [SerializeField] private PlayerAttackSystem attackSystem;
    [SerializeField] private InventoryUI inventoryUI;

    [Header("Default Sprites")]
    [SerializeField] private Sprite meleeSprite;

    [Header("Slot Roots (optional)")]
    [SerializeField] private RectTransform slot1;
    [SerializeField] private RectTransform slot2;
    [SerializeField] private RectTransform slot3;
    [SerializeField] private RectTransform slot4;

    private readonly RectTransform[] slotRoots = new RectTransform[4];
    private readonly Image[] slotTopImages = new Image[4];
    private readonly Image[] slotBottomImages = new Image[4];
    private readonly TextMeshProUGUI[] slotCountTexts = new TextMeshProUGUI[4];

    private void Awake()
    {
        if (attackSystem == null)
        {
            attackSystem = FindFirstObjectByType<PlayerAttackSystem>(FindObjectsInactive.Include);
        }

        if (inventoryUI == null)
        {
            TryResolveInventoryUI();
        }

        if (slot1 == null || slot2 == null || slot3 == null || slot4 == null)
        {
            slot1 = FindSlot("Slot1");
            slot2 = FindSlot("Slot2");
            slot3 = FindSlot("Slot3");
            slot4 = FindSlot("Slot4");
        }

        slotRoots[0] = slot1;
        slotRoots[1] = slot2;
        slotRoots[2] = slot3;
        slotRoots[3] = slot4;

        for (int i = 0; i < slotRoots.Length; i++)
        {
            RectTransform slot = slotRoots[i];
            slotBottomImages[i] = EnsureSlotLayerImage(slot, "BottomHalf", 0);
            slotTopImages[i] = EnsureSlotLayerImage(slot, "TopHalf", 1);
            slotCountTexts[i] = EnsureSlotCountLabel(slot, $"Slot{i + 1}Count");

            AttachClickHandler(slot, i);
        }
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (attackSystem == null || attackSystem.slots == null)
        {
            return;
        }

        for (int i = 0; i < slotRoots.Length; i++)
        {
            ResolveSpritesForSlot(i, out Sprite topSprite, out Sprite bottomSprite);
            ApplySlotVisual(i, topSprite, bottomSprite);
            RefreshCountLabel(i);
        }
    }

    public void ForceRefresh()
    {
        Refresh();
    }

    private void ResolveSpritesForSlot(int index, out Sprite topSprite, out Sprite bottomSprite)
    {
        topSprite = null;
        bottomSprite = null;

        if (attackSystem == null || attackSystem.slots == null || attackSystem.slots.Count <= index)
        {
            return;
        }

        WeaponSlot slot = attackSystem.slots[index];

        if (slot.type == WeaponType.Melee)
        {
            topSprite = meleeSprite;
            bottomSprite = null;
            return;
        }

        if (slot.type == WeaponType.PotionBomb && slot.equippedPotion != null && slot.equippedPotion.data != null)
        {
            PotionData data = slot.equippedPotion.data;

            topSprite = data.topIMG != null
                ? data.topIMG
                : (data.icon != null ? data.icon : data.bottomIMG);

            bottomSprite = data.bottomIMG != null
                ? data.bottomIMG
                : null;

            if (bottomSprite == topSprite)
            {
                bottomSprite = null;
            }

            return;
        }

        if (slot.specificPrefab != null)
        {
            SpriteRenderer sr = slot.specificPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                topSprite = sr.sprite;
            }
        }
    }

    private void ApplySlotVisual(int index, Sprite topSprite, Sprite bottomSprite)
    {
        if (index < 0 || index >= slotTopImages.Length)
        {
            return;
        }

        Image top = slotTopImages[index];
        Image bottom = slotBottomImages[index];

        if (bottom != null)
        {
            bottom.sprite = bottomSprite;
            bottom.enabled = bottomSprite != null;
            bottom.color = Color.white;
        }

        if (top != null)
        {
            top.sprite = topSprite;
            top.enabled = topSprite != null;
            top.color = Color.white;
        }
    }

    private RectTransform FindSlot(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t as RectTransform : null;
    }

    private static Image EnsureSlotLayerImage(RectTransform slot, string name, int siblingIndex)
    {
        if (slot == null)
        {
            return null;
        }

        Transform existingTransform = slot.Find(name);
        Image image = existingTransform != null ? existingTransform.GetComponent<Image>() : null;

        if (image == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slot, false);
            image = go.GetComponent<Image>();
        }

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        image.preserveAspect = false;
        image.raycastTarget = false;
        image.enabled = false;
        image.canvasRenderer.SetAlpha(1f);

        int safeIndex = Mathf.Clamp(siblingIndex, 0, slot.childCount - 1);
        image.transform.SetSiblingIndex(safeIndex);

        return image;
    }

    private void AttachClickHandler(RectTransform slot, int index)
    {
        if (slot == null)
        {
            return;
        }

        EnsureClickArea(slot);

        WeaponSlotClickHandler handler = slot.GetComponent<WeaponSlotClickHandler>();
        if (handler == null)
        {
            handler = slot.gameObject.AddComponent<WeaponSlotClickHandler>();
        }

        handler.Init(this, index);
    }

    public void HandleSlotClick(int index)
    {
        HandleSlotClick(index, PointerEventData.InputButton.Left);
    }

    public void HandleSlotClick(int index, PointerEventData.InputButton button)
    {
        TryResolveInventoryUI();

        if (inventoryUI != null)
        {
            inventoryUI.OnWeaponSlotClicked(index, button);
        }
    }

    private static void EnsureClickArea(RectTransform slot)
    {
        if (slot == null)
        {
            return;
        }

        Transform existing = slot.Find("ClickArea");
        Image clickImage = existing != null ? existing.GetComponent<Image>() : null;

        if (clickImage == null)
        {
            GameObject go = new GameObject("ClickArea", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slot, false);
            clickImage = go.GetComponent<Image>();
        }

        RectTransform rect = clickImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        clickImage.color = new Color(1f, 1f, 1f, 0f);
        clickImage.raycastTarget = true;
        clickImage.preserveAspect = false;

        clickImage.transform.SetSiblingIndex(0);
    }

    private bool TryResolveInventoryUI()
    {
        if (inventoryUI != null)
        {
            return true;
        }

        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        return inventoryUI != null;
    }

    private static TextMeshProUGUI EnsureSlotCountLabel(RectTransform slot, string name)
    {
        if (slot == null)
        {
            return null;
        }

        Transform existingTransform = slot.Find(name);
        TextMeshProUGUI existing = existingTransform != null ? existingTransform.GetComponent<TextMeshProUGUI>() : null;
        if (existing != null)
        {
            ApplyCountLabelStyle(existing);
            return existing;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(slot, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(44f, 36f);
        rect.anchoredPosition = new Vector2(-3f, 2f);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        ApplyCountLabelStyle(text);

        return text;
    }

    private static void ApplyCountLabelStyle(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }

        text.alignment = TextAlignmentOptions.BottomRight;
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.35f;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        if (string.IsNullOrWhiteSpace(text.text))
        {
            text.text = string.Empty;
        }
    }

    private void RefreshCountLabel(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCountTexts.Length)
        {
            return;
        }

        TextMeshProUGUI label = slotCountTexts[slotIndex];
        if (label == null || attackSystem == null || attackSystem.slots == null || attackSystem.slots.Count <= slotIndex)
        {
            return;
        }

        // Show potion count only on slot 1.
        if (slotIndex != 0)
        {
            label.text = string.Empty;
            label.enabled = false;
            return;
        }

        WeaponSlot slot = attackSystem.slots[slotIndex];
        int count = 0;
        if (slot.type == WeaponType.PotionBomb && slot.equippedPotion != null)
        {
            count = Mathf.Clamp(slot.equippedPotion.quantity, 0, 99);
        }

        if (count > 0)
        {
            label.text = count.ToString();
            if (!label.enabled)
            {
                label.enabled = true;
            }
        }
        else
        {
            label.text = string.Empty;
            label.enabled = false;
        }
    }
}
