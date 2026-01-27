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
            "캠프파이어를 사용하시겠습니까?",
            "제작하기",
            () => { UIManager.Instance.GoCrafting(); },
            "취소",
            () => {}
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
        
        NPC exitedNPC = other.GetComponent<NPC>();
        if (exitedNPC != null && exitedNPC == currentNPC)
        {
            currentNPC = null;
        }

       
        if (other.CompareTag("Campfire"))
        {
            isCampfire = false;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideSelectPanel();
            }
        }

        WorldItem exitedItem = other.GetComponent<WorldItem>();
        if (exitedItem != null && exitedItem == currentItem)
        {
            currentItem = null;
        }

        if (currentNPC == null && !isCampfire && currentItem == null)
        {
            canInteract = false;
        }
    }
}