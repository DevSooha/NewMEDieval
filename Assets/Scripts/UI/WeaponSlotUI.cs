using System.Collections;
using UnityEngine;
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

    private Image[] slotImages = new Image[4];
    private Sprite[] lastSprites = new Sprite[4];
    private Coroutine[] fadeRoutines = new Coroutine[4];
    [SerializeField] private float fadeDuration = 0.08f;

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

        slotImages[0] = EnsureSlotImage(slot1, "Slot1Icon");
        slotImages[1] = EnsureSlotImage(slot2, "Slot2Icon");
        slotImages[2] = EnsureSlotImage(slot3, "Slot3Icon");
        slotImages[3] = EnsureSlotImage(slot4, "Slot4Icon");

        AttachClickHandler(slot1, 0);
        AttachClickHandler(slot2, 1);
        AttachClickHandler(slot3, 2);
        AttachClickHandler(slot4, 3);
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (attackSystem == null) return;
        if (attackSystem.slots == null) return;

        for (int i = 0; i < 4; i++)
        {
            Sprite next = GetSpriteForSlot(i);
            if (slotImages[i] == null) continue;

            if (lastSprites[i] != next)
            {
                lastSprites[i] = next;
                StartFadeSwap(i, next);
            }
        }
    }

    private Sprite GetSpriteForSlot(int index)
    {
        if (attackSystem.slots.Count <= index) return null;
        WeaponSlot slot = attackSystem.slots[index];

        if (slot.type == WeaponType.Melee)
        {
            return meleeSprite;
        }

        if (slot.type == WeaponType.PotionBomb && slot.equippedPotion != null && slot.equippedPotion.data != null)
        {
            if (slot.equippedPotion.data.topIMG != null) return slot.equippedPotion.data.topIMG;
            if (slot.equippedPotion.data.bottomIMG != null) return slot.equippedPotion.data.bottomIMG;
        }

        if (slot.specificPrefab != null)
        {
            SpriteRenderer sr = slot.specificPrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) return sr.sprite;
        }

        return null;
    }

    private RectTransform FindSlot(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t as RectTransform : null;
    }

    private Image EnsureSlotImage(RectTransform slot, string name)
    {
        if (slot == null) return null;

        Image existing = slot.GetComponentInChildren<Image>();
        if (existing != null && existing.gameObject != slot.gameObject)
        {
            ConfigureImageRect(existing.rectTransform);
            existing.preserveAspect = false;
            existing.raycastTarget = true;
            existing.canvasRenderer.SetAlpha(1f);
            return existing;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(slot, false);
        Image image = go.GetComponent<Image>();

        ConfigureImageRect(image.rectTransform);
        image.preserveAspect = false;
        image.raycastTarget = true;
        image.enabled = false;
        image.canvasRenderer.SetAlpha(1f);

        return image;
    }

    private void ConfigureImageRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void AttachClickHandler(RectTransform slot, int index)
    {
        if (slot == null) return;

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
        TryResolveInventoryUI();

        if (inventoryUI != null)
        {
            inventoryUI.OnWeaponSlotClicked(index);
        }
    }

    private void StartFadeSwap(int index, Sprite next)
    {
        Image img = slotImages[index];
        if (img == null) return;

        if (fadeRoutines[index] != null)
        {
            StopCoroutine(fadeRoutines[index]);
        }

        fadeRoutines[index] = StartCoroutine(FadeSwapRoutine(img, next));
    }

    private IEnumerator FadeSwapRoutine(Image img, Sprite next)
    {
        if (img == null) yield break;

        img.CrossFadeAlpha(0f, fadeDuration, false);
        yield return new WaitForSeconds(fadeDuration);

        img.sprite = next;
        img.enabled = next != null;

        img.CrossFadeAlpha(1f, fadeDuration, false);
        yield return new WaitForSeconds(fadeDuration);
    }

    private void EnsureClickArea(RectTransform slot)
    {
        Image img = slot.GetComponent<Image>();
        if (img == null)
        {
            img = slot.gameObject.AddComponent<Image>();
        }

        img.color = new Color(1f, 1f, 1f, 0f);
        img.raycastTarget = true;
        img.preserveAspect = false;
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
}
