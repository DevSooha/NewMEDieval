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
    }

    public bool TryInteract()
    {
        // 1. 상호작용 불가능하면 실패 반환
        if (!canInteract) return false;

        // 2. 아이템 줍기
        if (currentItem != null)
        {
            PickUpItem();
            return true;
        }

        // 3. NPC 대화
        if (currentNPC != null)
        {
            if (UIManager.Instance == null) return false;

            if (!UIManager.Instance.IsDialogueActive())
                StartDialogue();
            else
                UIManager.Instance.AdvanceDialogue();

            return true;
        }

        // 4. 캠프파이어
        if (isCampfire)
        {
            return true;
        }

        return false;
    }

    void PickUpItem()
    {
        if (currentItem != null)
        {
            currentItem.Pickup();
            currentItem = null;
            canInteract = false;

            if (Player.Instance != null) Player.Instance.OnInteractionFinished();
        }
    }

    void StartDialogue()
    {
        Vector2 dir = (transform.position - currentNPC.transform.position).normalized;
        currentNPC.FaceDirection(dir);

        UIManager.Instance.StartDialogue(currentNPC.dialogueData, () =>
        {
            if (Player.Instance != null)
            {
                Player.Instance.OnInteractionFinished(); // 상태 복귀 함수
            }
        });
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        NPC npc = other.GetComponent<NPC>();
        if (npc != null)
        {
            currentNPC = npc;
            canInteract = true;
            return;
        }

        WorldItem item = other.GetComponent<WorldItem>();
        if (item != null)
        {
            currentItem = item;
            canInteract = true;
            return;
        }

        if (other.CompareTag("Campfire"))
        {
            isCampfire = true;
            canInteract = true;
            UIManager.Instance.ShowSelectPanel(
            "Campfire?",
            "Yes",
            () => { UIManager.Instance.GoCrafting(); },
            "No",
            () => { }
            );
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