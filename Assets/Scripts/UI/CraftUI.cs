using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;
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
    [SerializeField] private Image bellowsImage;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Sprite exitButtonSprite;
    [SerializeField] private Sprite resetButtonSprite;
    [SerializeField] private float bellowsFrameInterval = 0.08f;
    //[SerializeField] private Transform container;


    private Item slot1Item;
    private Item slot2Item;
    //private InventorySlot[] craftSlots;

    private float gaugeValue = 0f;
    private float gameTimer = 6f;
    private bool isGameActive = false;
    private bool isDragging = false;
    private bool isPointerPressed = false;
    private bool gameStarted = false;
    private Vector2 lastPointerPosition;
    private Vector2 pointerDownPosition;
    private bool wasDraggingDuringGame = false;

    private const float GAUGE_HEIGHT = 224f;
    private const float GAUGE_UP_SPEED = 28f;
    private const float GAUGE_DOWN_SPEED = 10f;
    private const float DRAG_THRESHOLD_PIXELS = 2f;
    private const float GAME_START_DRAG_THRESHOLD_PIXELS = 80f;
    private const float BELLOWS_RELEASE_ANIMATION_SECONDS = 0.25f;

    private int nextReplaceIndex = 0;
    private const float CORNER_BUTTON_SIZE = 32f;
    private const float CORNER_BUTTON_MARGIN = 16f;
    private readonly List<Sprite> bellowsFrames = new();
    private int bellowsFrameIndex = 0;
    private float bellowsFrameTimer = 0f;
    private Coroutine bellowsReleaseRoutine;

    private void OnEnable()
    {
        InitializeUiRefs();
        SetInventoryVisible(true);
        SetCornerButtonsVisible(true);
    }

    private void OnDisable()
    {
        SetCornerButtonsVisible(false);
        SetInventoryVisible(false);
        ResetCraftingState();
    }

    private void Start()
    {
        InitializeUiRefs();
        SetCornerButtonsVisible(true);
        oxygenGauge.gameObject.SetActive(false);
        ResetBellowsAnimation();
        if (resultText != null)
            resultText.gameObject.SetActive(false);
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
        Debug.Log("[CraftUI] Material selected for crafting.");
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerPressed = true;
        isDragging = false;
        lastPointerPosition = eventData.position;
        pointerDownPosition = eventData.position;

        if (!gameStarted && (slot1Item == null || slot2Item == null))
        {
            Debug.Log("[CraftUI] Not enough materials to start crafting.");
            isPointerPressed = false;
            return;
        }
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerPressed = false;
        isDragging = false;
    }

    private void Update()
    {
        RefreshDragState();
        TryStartMiniGameByDrag();
        UpdateBellowsAnimation();
        if (!isGameActive) return;

        float dt = Time.unscaledDeltaTime;

        if (isDragging)
        {
            gaugeValue += GAUGE_UP_SPEED * dt;
        }
        else
        {
            gaugeValue -= GAUGE_DOWN_SPEED * dt;
        }

        gameTimer -= dt;
        timerText.text = Mathf.Max(0, gameTimer).ToString("F1");

        gaugeValue = Mathf.Clamp(gaugeValue, 0f, 100f);

        UpdateGaugeUI();

        if (gameTimer <= 0)
        {
            EndGame();
        }
    }

    private void RefreshDragState()
    {
        if (!isPointerPressed || !Input.GetMouseButton(0))
        {
            isPointerPressed = false;
            isDragging = false;
            return;
        }

        Vector2 currentPointerPosition = Input.mousePosition;
        float distanceSqr = (currentPointerPosition - lastPointerPosition).sqrMagnitude;
        isDragging = distanceSqr >= (DRAG_THRESHOLD_PIXELS * DRAG_THRESHOLD_PIXELS);
        lastPointerPosition = currentPointerPosition;
    }

    private void TryStartMiniGameByDrag()
    {
        if (gameStarted || !isPointerPressed || !Input.GetMouseButton(0))
        {
            return;
        }

        if (slot1Item == null || slot2Item == null)
        {
            return;
        }

        float verticalDrag = Mathf.Abs(Input.mousePosition.y - pointerDownPosition.y);
        if (verticalDrag < GAME_START_DRAG_THRESHOLD_PIXELS)
        {
            return;
        }

        StartMiniGame();
    }

    private void StartMiniGame()
    {
        oxygenGauge.SetActive(true);
        isGameActive = true;
        gameStarted = true;
        gaugeValue = 0f;
        gameTimer = 6f;
        UpdateGaugeUI();
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
        isPointerPressed = false;
        wasDraggingDuringGame = false;
        EnsureInventory();

        PotionTemp potionTemp = DeterminePotionTemp(gaugeValue);
        string resultName = GetPotionName(potionTemp);

        if (resultText != null)
        {
            resultText.text = resultName;
            resultText.gameObject.SetActive(true);
        }

        if (potionTemp != PotionTemp.Failure)
        {
            PotionData craftedPotion = CraftPotion(slot1Item, slot2Item, potionTemp);
            if (craftedPotion != null)
            {
                if (inventory != null)
                {
                    inventory.AddPotion(craftedPotion, 1);
                    if (inventoryUI != null)
                    {
                        inventoryUI.RefreshUI();
                    }
                }
                else
                {
                    Debug.LogWarning("[CraftUI] Inventory reference is missing. Crafted potion was not added.");
                }
            }
        }

        Debug.Log($"[CraftUI] Crafting finished. Gauge: {gaugeValue:F1}");

        RemoveUsedMaterials();

        Invoke(nameof(CloseGame), 1.5f);
    }
    private void RemoveUsedMaterials()
    {
        EnsureInventory();
        if (inventory == null)
        {
            return;
        }

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
        StopBellowsReleaseAnimation();
        ResetBellowsAnimation();
        if (resultText != null)
            resultText.gameObject.SetActive(false);
        gameStarted = false;
        ClearSlots();
    }

    public void ForceCloseImmediate(bool clearSlots = false)
    {
        CancelInvoke(nameof(CloseGame));
        StopBellowsReleaseAnimation();

        isGameActive = false;
        isDragging = false;
        isPointerPressed = false;
        gameStarted = false;
        wasDraggingDuringGame = false;

        if (oxygenGauge != null)
            oxygenGauge.SetActive(false);
        ResetBellowsAnimation();

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
        if (closeButton == null || IsSelectPanelButtonName(closeButton.gameObject.name))
        {
            closeButton = FindButtonByNames("ExitButton", "CloseButton", "Exit");
        }

        closeButton = EnsureCornerButton(
            closeButton,
            "ExitButton",
            "X",
            exitButtonSprite,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-CORNER_BUTTON_MARGIN, -CORNER_BUTTON_MARGIN),
            keepExistingLayout: false
        );

        closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        closeButton.onClick.AddListener(OnCloseButtonClicked);
    }

    private void EnsureResetButton()
    {
        if (resetButton == null || IsSelectPanelButtonName(resetButton.gameObject.name))
        {
            resetButton = FindButtonByNames("ResetButton");
        }

        resetButton = EnsureCornerButton(
            resetButton,
            "ResetButton",
            "R",
            resetButtonSprite,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(CORNER_BUTTON_MARGIN, -CORNER_BUTTON_MARGIN),
            keepExistingLayout: false
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

    private void InitializeUiRefs()
    {
        EnsureInventory();
        EnsureInventoryUI();
        EnsureCloseButton();
        EnsureResetButton();
        EnsureBellowsRefs();
    }

    private void SetCornerButtonsVisible(bool visible)
    {
        SetCloseButtonVisible(visible);
        SetResetButtonVisible(visible);
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
        StopBellowsReleaseAnimation();
        isGameActive = false;
        isDragging = false;
        isPointerPressed = false;
        gameStarted = false;
        wasDraggingDuringGame = false;
        gaugeValue = 0f;
        gameTimer = 6f;

        if (oxygenGauge != null)
            oxygenGauge.SetActive(false);
        ResetBellowsAnimation();

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

    private Button EnsureCornerButton(
        Button button,
        string name,
        string labelText,
        Sprite iconSprite,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        bool keepExistingLayout = false)
    {
        bool createdNew = false;

        if (button == null)
        {
            button = FindButtonByNames(name);
        }

        if (button == null)
        {
            createdNew = true;

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

        button.gameObject.SetActive(true);
        button.interactable = true;
        if (button.targetGraphic != null)
        {
            button.targetGraphic.raycastTarget = true;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }

        ApplyButtonVisual(button, labelText, iconSprite);

        if (!keepExistingLayout || createdNew)
        {
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
        }

        button.transform.SetAsLastSibling();

        return button;
    }

    private static void ApplyButtonVisual(Button button, string labelText, Sprite iconSprite)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (iconSprite != null)
        {
            if (image != null)
            {
                image.sprite = iconSprite;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
            }

            if (label != null)
            {
                label.gameObject.SetActive(false);
            }

            return;
        }

        if (image != null && image.sprite == null)
        {
            image.color = new Color(0f, 0f, 0f, 0.65f);
        }

        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = labelText;
        }
    }

    private Button FindButtonByNames(params string[] names)
    {
        if (names == null || names.Length == 0)
        {
            return null;
        }

        Canvas canvas = GetComponentInParent<Canvas>(true);
        Transform root = canvas != null ? canvas.transform : transform.root;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < names.Length; i++)
        {
            string candidate = names[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            for (int j = 0; j < buttons.Length; j++)
            {
                Button b = buttons[j];
                if (b == null) continue;

                if (string.Equals(b.gameObject.name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return b;
                }
            }
        }

        return null;
    }

    private static bool IsSelectPanelButtonName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return string.Equals(name, "Btn_1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Btn_2", StringComparison.OrdinalIgnoreCase);
    }
    private void EnsureInventoryUI()
    {
        if (inventoryUI != null) return;

        inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
    }

    private void EnsureInventory()
    {
        if (inventory != null) return;

        inventory = Inventory.Instance;
        if (inventory == null)
        {
            inventory = FindFirstObjectByType<Inventory>(FindObjectsInactive.Include);
        }
    }

    private void EnsureBellowsRefs()
    {
        if (bellowsImage == null)
        {
            Transform bellowsTransform = FindDescendantByName(transform, "Bellows");
            if (bellowsTransform != null)
            {
                bellowsImage = bellowsTransform.GetComponent<Image>();
            }
        }

        if (bellowsFrames.Count == 0)
        {
            string[] frameNames = { "bellow1", "bellow2", "bellow3", "bellow4" };
            for (int i = 0; i < frameNames.Length; i++)
            {
                Sprite frame = Resources.Load<Sprite>(frameNames[i]);
                if (frame != null)
                {
                    bellowsFrames.Add(frame);
                }
            }

            if (bellowsFrames.Count == 0 && bellowsImage != null && bellowsImage.sprite != null)
            {
                bellowsFrames.Add(bellowsImage.sprite);
            }
        }
    }

    private void UpdateBellowsAnimation()
    {
        if (bellowsImage == null || bellowsFrames.Count == 0) return;

        bool draggingNow = isGameActive && isDragging;

        if (draggingNow)
        {
            StopBellowsReleaseAnimation();
            wasDraggingDuringGame = true;

            bellowsFrameTimer += Time.unscaledDeltaTime;
            if (bellowsFrameTimer < bellowsFrameInterval) return;

            bellowsFrameTimer = 0f;
            bellowsFrameIndex = (bellowsFrameIndex + 1) % bellowsFrames.Count;
            bellowsImage.sprite = bellowsFrames[bellowsFrameIndex];
            return;
        }

        if (!isGameActive)
        {
            StopBellowsReleaseAnimation();
            wasDraggingDuringGame = false;
            ResetBellowsAnimation();
            return;
        }

        if (wasDraggingDuringGame)
        {
            StartBellowsReleaseAnimation();
            wasDraggingDuringGame = false;
        }
    }

    private void ResetBellowsAnimation()
    {
        if (bellowsImage == null || bellowsFrames.Count == 0) return;

        bellowsFrameTimer = 0f;
        bellowsFrameIndex = 0;
        bellowsImage.sprite = bellowsFrames[0];
    }

    private void StartBellowsReleaseAnimation()
    {
        StopBellowsReleaseAnimation();
        bellowsReleaseRoutine = StartCoroutine(PlayBellowsReleaseAnimation());
    }

    private IEnumerator PlayBellowsReleaseAnimation()
    {
        if (bellowsImage == null || bellowsFrames.Count == 0)
        {
            bellowsReleaseRoutine = null;
            yield break;
        }

        if (bellowsFrames.Count < 4)
        {
            ResetBellowsAnimation();
            bellowsReleaseRoutine = null;
            yield break;
        }

        bellowsFrameTimer = 0f;
        int[] releaseOrder = { 3, 2, 1, 0 };
        float stepDuration = BELLOWS_RELEASE_ANIMATION_SECONDS / 3f;

        for (int i = 0; i < releaseOrder.Length; i++)
        {
            int frame = Mathf.Clamp(releaseOrder[i], 0, bellowsFrames.Count - 1);
            bellowsFrameIndex = frame;
            bellowsImage.sprite = bellowsFrames[frame];

            if (i < releaseOrder.Length - 1)
            {
                yield return new WaitForSecondsRealtime(stepDuration);
            }
        }

        bellowsReleaseRoutine = null;
    }

    private void StopBellowsReleaseAnimation()
    {
        if (bellowsReleaseRoutine != null)
        {
            StopCoroutine(bellowsReleaseRoutine);
            bellowsReleaseRoutine = null;
        }
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName)) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null) continue;

            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindDescendantByName(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void SetInventoryVisible(bool visible)
    {
        if (inventoryUI == null) return;
        inventoryUI.gameObject.SetActive(visible);
    }
    public static PotionTemp DeterminePotionTemp(float gaugeValue)
    {
        CraftTemperatureBand band = PotionCraftRules.DetermineBand(gaugeValue);
        return band switch
        {
            CraftTemperatureBand.Low => PotionTemp.LowTemp,
            CraftTemperatureBand.Mid => PotionTemp.MidTemp,
            CraftTemperatureBand.High => PotionTemp.HighTemp,
            _ => PotionTemp.Failure
        };
    }
    public static string GetPotionName(PotionTemp type)
    {
        CraftTemperatureBand band = type switch
        {
            PotionTemp.LowTemp => CraftTemperatureBand.Low,
            PotionTemp.MidTemp => CraftTemperatureBand.Mid,
            PotionTemp.HighTemp => CraftTemperatureBand.High,
            _ => CraftTemperatureBand.Failure
        };
        return PotionCraftRules.GetPotionName(band);
    }

    private static PotionTemperature ToPotionTemperature(PotionTemp tempType)
    {
        CraftTemperatureBand band = tempType switch
        {
            PotionTemp.LowTemp => CraftTemperatureBand.Low,
            PotionTemp.MidTemp => CraftTemperatureBand.Mid,
            PotionTemp.HighTemp => CraftTemperatureBand.High,
            _ => CraftTemperatureBand.Failure
        };
        return PotionCraftRules.ToPotionTemperature(band);
    }

    public PotionData CraftPotion(Item first, Item second, PotionTemp tempType)
    {
        if (first == null || second == null || first.data == null || second.data == null)
        {
            return null;
        }

        PotionTemperature temperature = ToPotionTemperature(tempType);
        if (temperature == PotionTemperature.Failure)
        {
            return null;
        }

        return PotionDesignCatalog.CraftPotion(first.data, second.data, temperature);
    }
}
