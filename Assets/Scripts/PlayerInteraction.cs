using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 16f;

    // NpcLayer는 Trigger 방식에서는 안 쓸 수도 있지만, OverlapCircle 쓸 거면 필요함
    // 여기서는 기존 Trigger 방식을 유지하므로 그냥 둡니다.
    // [SerializeField] private LayerMask npcLayer; 

    private CircleCollider2D interactionCollider;
    private Player playerMovement; // 부모에 있는 스크립트
    private NPC currentNPC;
    private bool canInteract = false;

    void Start()
    {
        interactionCollider = GetComponent<CircleCollider2D>();
        interactionCollider.radius = interactionRadius;
        interactionCollider.isTrigger = true;

        // ★ [수정됨] 이 스크립트는 이제 자식 오브젝트에 있으므로, 
        // 부모 오브젝트에서 Player 컴포넌트를 찾아와야 합니다.
        playerMovement = GetComponentInParent<Player>();
    }

    void Update()
    {
        // Z키 입력 감지
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (canInteract && currentNPC != null)
            {
                if (!DialogueManager.Instance.IsDialogueActive())
                {
                    InteractWithNPC();
                }
                else
                {
                    DialogueManager.Instance.AdvanceDialogue();
                }
            }
        }

        // (선택 사항) 센서 위치를 항상 부모(플레이어) 중심에 고정
        // 자식 오브젝트라서 보통 자동으로 따라다니지만, 확실하게 하기 위해 transform.localPosition을 0으로 둬도 됩니다.
        transform.localPosition = Vector3.zero;
    }

    void InteractWithNPC()
    {
        if (currentNPC != null)
        {
            // 방향 계산: 내 위치(센서)나 부모 위치나 거의 같음
            Vector2 direction = (transform.position - currentNPC.transform.position).normalized;
            currentNPC.FaceDirection(direction);

            DialogueManager.Instance.StartDialogue(currentNPC.dialogueData);
        }
    }

    // ★ Trigger 함수는 이제 '자식 오브젝트'의 콜라이더에 닿았을 때 실행됩니다.
    // 자식의 태그는 Untagged이므로 총알은 이 함수와 상관없이 그냥 통과합니다(총알 로직에서 Player 태그만 죽이니까).
    void OnTriggerEnter2D(Collider2D other)
    {
        NPC npc = other.GetComponent<NPC>();
        if (npc != null)
        {
            currentNPC = npc;
            canInteract = true;
            // 디버그용: 범위 들어왔는지 확인
            // Debug.Log("NPC 감지됨: " + npc.name);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        NPC npc = other.GetComponent<NPC>();
        if (npc != null && npc == currentNPC)
        {
            currentNPC = null;
            canInteract = false;
        }
    }
}