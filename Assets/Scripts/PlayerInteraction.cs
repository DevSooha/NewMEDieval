using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius;

    [Header("UI Reference")]
    private CircleCollider2D interactionCollider;
    private NPC currentNPC;
    private WorldItem currentItem;

    private bool canInteract = false;
    private bool isCampfire = false;

    public bool IsInteractable => canInteract;

    void Start()
    {
        interactionCollider = GetComponent<CircleCollider2D>();
        interactionCollider.radius = interactionRadius;
        interactionCollider.isTrigger = true;

        isCampfire = false;

        currentNPC = null;

        canInteract = false;
        currentItem = null;
    }

    // Update Method for Interaction
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.F))
        {
            if (!canInteract) return;

            if (currentItem != null)
            {
                PickUpItem();
                return;
            }

            if (currentNPC != null)
            {
                // Dialogue logic...
                if (UIManager.Instance == null) return;
                if (!UIManager.Instance.IsDialogueActive())
                    StartDialogue();
                else
                    UIManager.Instance.AdvanceDialogue();
            }
        }
    }

    void PickUpItem()
    {
        //if (currentItem != null && Inventory.Instance != null)
        //{
        //    if (Inventory.Instance.AddItem(currentItem.itemData, currentItem.quantity))
        //    {
        //        Destroy(currentItem.gameObject);
        //        currentItem = null;
        //        canInteract = false;
        //    }
        //}

        if (currentItem != null)
        {
            Destroy(currentItem.gameObject);
            currentItem = null;
            canInteract = false;
        }
    }

    void StartDialogue()
    {
        Vector2 dir = (transform.position - currentNPC.transform.position).normalized;
        currentNPC.FaceDirection(dir);
        UIManager.Instance.StartDialogue(currentNPC.dialogueData);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<NPC>()) 
        { 
            currentNPC = other.GetComponent<NPC>(); 
            canInteract = true; 
        }

        else if (other.CompareTag("Campfire")) {
            isCampfire = true; 
            canInteract = true; 
            UIManager.Instance.ShowSelectPanel(
                "", 
                "Crafting", UIManager.Instance.GoCrafting, 
                "Potion", UIManager.Instance.GoPotion
            );
        }

        else if (other.GetComponent<WorldItem>()) 
        { 
            currentItem = other.GetComponent<WorldItem>(); 
            canInteract = true; 
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // 1. NPC인지 확인 (변수에 담아서 null이 아닌지 먼저 체크)
        NPC exitedNPC = other.GetComponent<NPC>();
        if (exitedNPC != null && exitedNPC == currentNPC)
        {
            currentNPC = null;
        }

        // 2. [수정] else if를 지우고 독립적인 if문으로 변경
        // 이렇게 해야 위에서 무슨 일이 있어도 캠프파이어 태그를 반드시 확인합니다.
        if (other.CompareTag("Campfire"))
        {
            Debug.Log("캠프파이어 나감"); // 디버깅용 로그

            isCampfire = false;

            // 안전하게 UI 닫기
            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideSelectPanel();
            }
        }

        // 3. 아이템인지 확인 (역시 독립적인 if문 사용)
        WorldItem exitedItem = other.GetComponent<WorldItem>();
        if (exitedItem != null && exitedItem == currentItem)
        {
            currentItem = null;
        }

        // 상호작용 가능 상태 끄기
        if (currentNPC == null && !isCampfire && currentItem == null)
        {
            canInteract = false;
        }
    }
}