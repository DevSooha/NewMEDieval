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

    private void Awake()
    {
        EnsureImageRefs();
        EnsureVisualOrder();
        EnsureClickArea();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureImageRefs();
    }
#endif

    public void Init(InventoryUI ui, int index)
    {
        inventoryUI = ui;
        SlotIndex = index;
        EnsureClickArea();
    }

    public void OnClick()
    {
        OnClick(PointerEventData.InputButton.Right);
    }

    public void OnClick(PointerEventData.InputButton button)
    {
        if (inventoryUI != null)
        {
            inventoryUI.OnPotionSlotClicked(SlotIndex, button);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left
            && eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }
        OnClick(eventData.button);
    }

    public void SetPotion(Potion potion)
    {
        EnsureImageRefs();
        EnsureVisualOrder();

        currentPotion = potion;
        
        if (potion != null && potion.data != null)
        {
            PotionVisualParts visualParts = PotionVisualResolver.Resolve(potion.data);
            bool hasFullVisualSet = visualParts.Top != null && visualParts.Bottom != null && visualParts.Frame != null;

            Sprite topSprite = hasFullVisualSet ? visualParts.Top : null;
            if (topIMG != null && topSprite != null)
            {
                topIMG.sprite = topSprite;
                topIMG.color = Color.white;
                topIMG.enabled = true;
            }
            else if (topIMG != null)
            {
                topIMG.enabled = false;
            }
            
            Sprite bottomSprite = hasFullVisualSet ? visualParts.Bottom : null;
            if (bottomIMG != null && bottomSprite != null)
            {
                bottomIMG.sprite = bottomSprite;
                bottomIMG.color = Color.white;
                bottomIMG.enabled = true;
            }
            else if (bottomIMG != null)
            {
                bottomIMG.enabled = false;
            }
            
            if (frame != null)
            {
                frame.sprite = hasFullVisualSet ? visualParts.Frame : null;
                if (frame.sprite != null)
                {
                    frame.color = Color.white;
                }

                frame.enabled = frame.sprite != null;
            }
            
            if (quantityText != null && potion.data.isStackable && potion.quantity > 1)
            {
                quantityText.text = potion.quantity.ToString();
                quantityText.enabled = true;
            }
            else if (quantityText != null)
            {
                quantityText.enabled = false;
            }
        }
        else
        {
            Clear();
        }
    }
    
    public void Clear()
    {
        EnsureImageRefs();
        EnsureVisualOrder();

        currentPotion = null;
        if (topIMG != null) topIMG.enabled = false;
        if (bottomIMG != null) bottomIMG.enabled = false;
        if (frame != null) frame.enabled = false;
        if (quantityText != null) quantityText.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentPotion != null && inventoryUI != null)
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

            inventoryUI.ShowPotionTooltip(currentPotion, anchorPosition);
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

    private void EnsureImageRefs()
    {
        if (topIMG == null)
        {
            topIMG = FindImageByName("TopHalf");
        }

        if (bottomIMG == null)
        {
            bottomIMG = FindImageByName("BottomHalf");
        }

        if (frame == null)
        {
            frame = FindImageByName("Frame");
        }

        if (quantityText == null)
        {
            quantityText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private Image FindImageByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform child = transform.Find(objectName);
        if (child != null)
        {
            return child.GetComponent<Image>();
        }

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform t = allChildren[i];
            if (t == null) continue;

            if (string.Equals(t.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return t.GetComponent<Image>();
            }
        }

        return null;
    }

    private void EnsureVisualOrder()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (topIMG == null || bottomIMG == null)
        {
            return;
        }

        Transform parent = topIMG.transform.parent;
        if (parent == null || bottomIMG.transform.parent != parent)
        {
            return;
        }

        bottomIMG.transform.SetSiblingIndex(0);
        topIMG.transform.SetSiblingIndex(1);

        if (frame != null && frame.transform.parent == parent)
        {
            frame.transform.SetAsLastSibling();
        }

        if (quantityText != null && quantityText.transform.parent == parent)
        {
            quantityText.transform.SetAsLastSibling();
        }
    }
}
