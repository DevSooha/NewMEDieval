using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class CraftUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum PotionTemp { Failure, LowTemp, MidTemp, HighTemp }

    [SerializeField] private Image potSlot1Image;
    [SerializeField] private Image potSlot2Image;
    [SerializeField] private ItemData itemData;
    [SerializeField] private Inventory inventory;
    [SerializeField] private InventoryUI inventoryUI;
    //[SerializeField] private GameObject itemUI;

    [SerializeField] private Image gaugeBar;          
    [SerializeField] private Image needleMarker;      
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private GameObject oxygenGauge;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;
    //[SerializeField] private Transform container;
    

    private Item slot1Item;
    private Item slot2Item;
    //private InventorySlot[] craftSlots;

    private float gaugeValue = 0f;      
    private float gameTimer = 6f;       
    private bool isGameActive = false;
    private bool isDragging = false;
    private bool gameStarted = false;

    private const float GAUGE_HEIGHT = 224f;
    private const float GAUGE_UP_SPEED = 28f;   
    private const float GAUGE_DOWN_SPEED = 10f;

    private int nextReplaceIndex = 0;
    private const float CORNER_BUTTON_SIZE = 32f;
    private const float CORNER_BUTTON_MARGIN = 16f;

    private void OnEnable()
    {
        EnsureInventoryUI();
        SetInventoryVisible(true);
        EnsureCloseButton();
        EnsureResetButton();
        SetCloseButtonVisible(true);
        SetResetButtonVisible(true);
    }

    private void OnDisable()
    {
        SetCloseButtonVisible(false);
        SetResetButtonVisible(false);
        SetInventoryVisible(false);
        ResetCraftingState();
    }

    private void Start()
    {
        EnsureInventoryUI();
        EnsureCloseButton();
        EnsureResetButton();
        SetCloseButtonVisible(true);
        SetResetButtonVisible(true);
        oxygenGauge.gameObject.SetActive(false);
        if (resultText != null)
            resultText.gameObject.SetActive(false);
    }
    private void FixedUpdate()
    {
        if (isGameActive && Input.GetMouseButton(0) && isDragging == true)
    {
        gaugeValue += GAUGE_UP_SPEED * Time.deltaTime;
    }
    }
    //private void InitializeSlots()
    //{
    //    int slotCount = 2;
        
    //    InventorySlot[] slots = new InventorySlot[slotCount];
        
    //    for (int i = 0; i < slotCount; i++)
    //    {
    //        GameObject slotObj = Instantiate(itemUI, container);
    //       InventorySlot slot = slotObj.GetComponent<InventorySlot>();
    //        slots[i] = slot;
    //    }
    //}
    public void OnMaterialSelected(Item item)
    {
        if (item == null || item.data == null) return;

        if (nextReplaceIndex == 0)
        {
            slot1Item = item;
            potSlot1Image.sprite = item.data.icon;
            potSlot1Image.enabled = true;
            nextReplaceIndex = 1;
        }
        else
        {
            slot2Item = item;
            potSlot2Image.sprite = item.data.icon;
            potSlot2Image.enabled = true;
            nextReplaceIndex = 0;
        }
        Debug.Log("솥에 아이템 추가!");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!gameStarted)
        {
            if (slot1Item == null || slot2Item == null)
            {
                Debug.Log("재료가 부족합니다!");
                return;
            }

            oxygenGauge.SetActive(true);
            isGameActive = true;
            gameStarted = true;
            gaugeValue = 0f;
            gameTimer = 6f;
            Debug.Log("풀무 게임 시작!");
        }

        if (isGameActive)
        {
            isDragging = true;
        }
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    private void Update()
    {
        if (!isGameActive) return;

        gameTimer -= Time.deltaTime;
        timerText.text = Mathf.Max(0, gameTimer).ToString("F1");

        if (!isDragging)
        {
            gaugeValue -= GAUGE_DOWN_SPEED * Time.deltaTime;
        }

        gaugeValue = Mathf.Clamp(gaugeValue, 0f, 100f);

        UpdateGaugeUI();

        if (gameTimer <= 0)
        {
            EndGame();
        }
    }

    private void UpdateGaugeUI()
    {
        float needleY = (gaugeValue / 100f) * GAUGE_HEIGHT - GAUGE_HEIGHT / 2f;
        needleMarker.rectTransform.localPosition = new Vector3(27.5f, needleY, 0);
    }

    private void EndGame()
    {
        isGameActive = false;
        isDragging = false;

        PotionTemp potionTemp = DeterminePotionTemp(gaugeValue);
        string resultName = GetPotionName(potionTemp);

        if (resultText != null)
        {
            resultText.text = resultName;
            resultText.gameObject.SetActive(true);
        }

        PotionData craftedPotion = CraftPotion(slot1Item, slot2Item);
        inventory.AddPotion(craftedPotion, 1);

        Debug.Log($"게임 종료! 게이지: {gaugeValue:F1}");

        RemoveUsedMaterials();

        Invoke(nameof(CloseGame), 1.5f);
    }
    private void RemoveUsedMaterials()
    {
        if (slot1Item != null)
        {
            int index = inventory.MaterialItems.IndexOf(slot1Item);
            if (index >= 0)
            {
                inventory.RemoveItem(index, 1);
            }
        }

        if (slot2Item != null)
        {
            int index = inventory.MaterialItems.IndexOf(slot2Item);
            if (index >= 0)
            {
                inventory.RemoveItem(index, 1);
            }
        }
    }
    private void CloseGame()
    {
        oxygenGauge.SetActive(false);
        if (resultText != null)
            resultText.gameObject.SetActive(false);
        gameStarted = false;
        ClearSlots();
    }

    public void ForceCloseImmediate(bool clearSlots = false)
    {
        CancelInvoke(nameof(CloseGame));

        isGameActive = false;
        isDragging = false;
        gameStarted = false;

        if (oxygenGauge != null)
            oxygenGauge.SetActive(false);

        if (resultText != null)
            resultText.gameObject.SetActive(false);

        ClearSlots();
        gaugeValue = 0f;
        gameTimer = 6f;

        SetCloseButtonVisible(false);
        SetResetButtonVisible(false);
        gameObject.SetActive(false);
    }

    private void EnsureCloseButton()
    {
        closeButton = EnsureCornerButton(
            closeButton,
            "CloseButton",
            "X",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-CORNER_BUTTON_MARGIN, -CORNER_BUTTON_MARGIN)
        );

        closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        closeButton.onClick.AddListener(OnCloseButtonClicked);
    }

    private void EnsureResetButton()
    {
        resetButton = EnsureCornerButton(
            resetButton,
            "ResetButton",
            "R",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(CORNER_BUTTON_MARGIN, -CORNER_BUTTON_MARGIN)
        );

        resetButton.onClick.RemoveListener(OnResetButtonClicked);
        resetButton.onClick.AddListener(OnResetButtonClicked);
    }

    private void OnCloseButtonClicked()
    {
        RequestCloseConfirm();
    }

    private void OnResetButtonClicked()
    {
        RequestResetConfirm();
    }

    private void SetCloseButtonVisible(bool visible)
    {
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(visible);
        }
    }

    private void SetResetButtonVisible(bool visible)
    {
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(visible);
        }
    }
    public void ClearSlots()
    {
        slot1Item = null;
        slot2Item = null;
        potSlot1Image.enabled = false;
        potSlot2Image.enabled = false;
        nextReplaceIndex = 0;
        gameStarted = false;
    }

    private void ResetCraftingState()
    {
        CancelInvoke(nameof(CloseGame));
        isGameActive = false;
        isDragging = false;
        gameStarted = false;
        gaugeValue = 0f;
        gameTimer = 6f;

        if (oxygenGauge != null)
            oxygenGauge.SetActive(false);

        if (resultText != null)
            resultText.gameObject.SetActive(false);

        ClearSlots();
    }

    public void RequestResetConfirm()
    {
        if (UIManager.Instance == null)
        {
            ResetCraftingState();
            return;
        }

        UIManager.Instance.ShowSelectPanel(
            "Discard the potion in progress?",
            "Yes",
            () => { ResetCraftingState(); },
            "No",
            () => { }
        );
    }

    public void RequestCloseConfirm()
    {
        if (IsResetState())
        {
            ForceCloseImmediate();
            return;
        }

        if (UIManager.Instance == null)
        {
            ForceCloseImmediate();
            return;
        }

        UIManager.Instance.ShowSelectPanel(
            "Exit crafting? Changes will not be saved.",
            "Yes",
            () =>
            {
                ResetCraftingState();
                ForceCloseImmediate();
            },
            "No",
            () => { }
        );
    }

    private bool IsResetState()
    {
        return slot1Item == null
            && slot2Item == null
            && !gameStarted
            && !isGameActive
            && !isDragging;
    }

    private Button EnsureCornerButton(Button button, string name, string labelText, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition)
    {
        if (button == null)
        {
            Transform existing = transform.Find(name);
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
            }
        }

        if (button == null)
        {
            GameObject buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            Transform parentForButton = transform;
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                parentForButton = parentCanvas.transform;
            }

            buttonObj.transform.SetParent(parentForButton, false);

            Image bg = buttonObj.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            button = buttonObj.GetComponent<Button>();

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(buttonObj.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 20f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
        }

        Transform targetParent = transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            targetParent = canvas.transform;
        }
        button.transform.SetParent(targetParent, false);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(CORNER_BUTTON_SIZE, CORNER_BUTTON_SIZE);

        return button;
    }
    private void EnsureInventoryUI()
    {
        if (inventoryUI != null) return;

        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
    }

    private void SetInventoryVisible(bool visible)
    {
        if (inventoryUI == null) return;
        inventoryUI.gameObject.SetActive(visible);
    }
    public static PotionTemp DeterminePotionTemp(float gaugeValue)
    {
        if (gaugeValue < 25f)
            return PotionTemp.Failure;
        else if (gaugeValue < 50f)
            return PotionTemp.LowTemp;
        else if (gaugeValue < 75f)
            return PotionTemp.MidTemp;
        else
            return PotionTemp.HighTemp;
    }
    public static string GetPotionName(PotionTemp type)
    {
        return type switch
        {
            PotionTemp.Failure => "FAILED",
            PotionTemp.LowTemp => "LOW TEMP POTION",
            PotionTemp.MidTemp => "MID TEMP POTION",
            PotionTemp.HighTemp => "HIGH TEMP POTION",
            _ => "Unknown"
        };
    }
    public PotionData CraftPotion(Item first, Item second)
    {
        //cp: crafted potion
        PotionData cp = ScriptableObject.CreateInstance<PotionData>();
        cp.potionName = first.data.topName + second.data.bottomName;
        cp.damage1 = first.data.topDamage;
        cp.damage2 = second.data.bottomDamage;
        cp.element1 = first.data.element;
        cp.element2 = second.data.element;
        cp.topIMG = first.data.topSprite;
        cp.bottomIMG = second.data.bottomSprite;

        return cp;
    }
}
