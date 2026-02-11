using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CraftUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum PotionTemp { Failure, LowTemp, MidTemp, HighTemp }

    [SerializeField] private Image potSlot1Image;
    [SerializeField] private Image potSlot2Image;
    [SerializeField] private ItemData itemData;
    [SerializeField] private Inventory inventory;
    //[SerializeField] private GameObject itemUI;

    [SerializeField] private Image gaugeBar;          
    [SerializeField] private Image needleMarker;      
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private GameObject oxygenGauge;
    //[SerializeField] private Transform container;
    

    private Item slot1Item;
    private Item slot2Item;
    //private InventorySlot[] craftSlots;

    private float gaugeValue = 0f;      
    private float gameTimer = 6f;       
    private bool isGameActive = false;
    private bool isDragging = false;
    private bool gameStarted = false;

    private const float GAUGE_HEIGHT = 128f;
    private const float GAUGE_UP_SPEED = 28f;   
    private const float GAUGE_DOWN_SPEED = 10f;

    private int nextReplaceIndex = 0;

    private void Start()
    {        
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
        float barHeight = (gaugeValue / 100f) * GAUGE_HEIGHT;
        gaugeBar.rectTransform.sizeDelta = new Vector2(gaugeBar.rectTransform.sizeDelta.x, barHeight);

        float needleY = (gaugeValue / 100f) * GAUGE_HEIGHT - GAUGE_HEIGHT / 2f;
        needleMarker.rectTransform.localPosition = new Vector3(0, needleY, 0);
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
        PotionData cp = new()
        {
            potionName = first.data.topName + second.data.bottomName,

            damage1 = first.data.topDamage,
            damage2 = second.data.bottomDamage,
            
            element1 = first.data.element,
            element2 = second.data.element,

        };
        return cp;
    }
}