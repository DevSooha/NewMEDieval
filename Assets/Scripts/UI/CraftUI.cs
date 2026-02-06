using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CraftUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image potSlot1Image;
    [SerializeField] private Image potSlot2Image;
    [SerializeField] private ItemData itemData;
    [SerializeField] private Inventory inventory;

    [SerializeField] private Image gaugeBar;          
    [SerializeField] private Image needleMarker;      
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private GameObject oxygenGauge;

    private Item slot1Item;
    private Item slot2Item;

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

            oxygenGauge.gameObject.SetActive(true);
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

        PotionCraft.PotionType potionType = PotionCraft.DeterminePotionType(gaugeValue);
        string resultName = PotionCraft.GetPotionName(potionType);

        if (resultText != null)
        {
            resultText.text = resultName;
            resultText.gameObject.SetActive(true);
        }

        // PotionCraft에 포션 생성 위임
        PotionCraft.CreatePotion(potionType);

        Debug.Log($"게임 종료! 게이지: {gaugeValue:F1}");

        Invoke(nameof(CloseGame), 1.5f);
    }

    private void CloseGame()
    {
        oxygenGauge.gameObject.SetActive(false);
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

    public int GetMaterialCount()
    {
        int count = 0;
        if (slot1Item != null) count++;
        if (slot2Item != null) count++;
        return count;
    }
}