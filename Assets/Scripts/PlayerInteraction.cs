using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius;

    [Header("UI Reference")]
    private CircleCollider2D interactionCollider;
    private NPC currentNPC;
    private WorldItem currentItem;

    public CraftUI craftUI;
    [SerializeField] private GameObject craftingMenu;
    [SerializeField] private InventoryUI inventoryUI;

    private bool canInteract;
    private bool isCampfire;
    private Coroutine craftingOpenTransitionRoutine;
    private const float CraftingOpenTransitionSeconds = 0.5f;

    public bool IsInteractable => canInteract || IsCraftingActive();

    private void Start()
    {
        interactionCollider = GetComponent<CircleCollider2D>();
        if (interactionCollider == null)
        {
            Debug.LogError($"[PlayerInteraction] CircleCollider2D is missing on {gameObject.name}. Disabling PlayerInteraction.");
            enabled = false;
            return;
        }

        interactionCollider.radius = interactionRadius;
        interactionCollider.isTrigger = true;

        EnsureUiReferences();

        if (craftingMenu != null)
        {
            craftingMenu.SetActive(false);
        }
        else if (craftUI != null)
        {
            craftUI.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"[PlayerInteraction] CraftingMenu/CraftUI is not assigned on {gameObject.name}.");
        }
    }

    private void Update()
    {
        if (IsCraftingActive() && Input.GetKeyDown(KeyCode.X))
        {
            if (craftUI != null)
            {
                craftUI.RequestCloseConfirm();
            }
            else if (craftingMenu != null)
            {
                craftingMenu.SetActive(false);
            }
        }

        if (UIManager.Instance != null && UIManager.Instance.IsSelectPanelActive() && Input.GetKeyDown(KeyCode.X))
        {
            UIManager.Instance.HideSelectPanel();
        }
    }

    public bool TryInteract()
    {
        if (IsCraftingActive())
        {
            CombatInputHelper.ConsumeAttackInputThisFrame();
            return true;
        }

        if (!canInteract) return false;

        if (currentItem != null)
        {
            PickUpItem();
            CombatInputHelper.ConsumeAttackInputThisFrame();
            return true;
        }

        if (currentNPC != null)
        {
            if (UIManager.Instance == null) return false;

            if (!UIManager.Instance.IsDialogueActive())
            {
                StartDialogue();
            }
            else
            {
                UIManager.Instance.AdvanceDialogue();
            }

            CombatInputHelper.ConsumeAttackInputThisFrame();
            return true;
        }

        if (isCampfire)
        {
            CombatInputHelper.ConsumeAttackInputThisFrame();
            return true;
        }

        return false;
    }

    private bool IsCraftingActive()
    {
        EnsureUiReferences();

        if (craftingMenu != null)
        {
            return craftingMenu.activeInHierarchy;
        }

        return craftUI != null && craftUI.gameObject.activeInHierarchy;
    }

    private void EnsureUiReferences()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (craftUI == null || craftUI.gameObject.scene != activeScene)
        {
            craftUI = FindInActiveScene<CraftUI>();
            if (craftUI == null)
            {
                craftUI = FindFirstObjectByType<CraftUI>(FindObjectsInactive.Include);
            }
        }

        if (craftingMenu == null || craftingMenu.scene != activeScene)
        {
            craftingMenu = null;

            if (craftUI != null)
            {
                craftingMenu = FindAncestorByName(craftUI.transform, "CraftingMenu");
            }

            if (craftingMenu == null)
            {
                craftingMenu = FindGameObjectInActiveScene("CraftingMenu");
            }
        }

        if (craftUI == null && craftingMenu != null)
        {
            craftUI = craftingMenu.GetComponent<CraftUI>();
            if (craftUI == null)
            {
                craftUI = craftingMenu.GetComponentInChildren<CraftUI>(true);
            }
        }

        if (inventoryUI == null || inventoryUI.gameObject.scene != activeScene)
        {
            inventoryUI = FindInActiveScene<InventoryUI>();
            if (inventoryUI == null)
            {
                inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
            }
        }
    }

    private static T FindInActiveScene<T>() where T : Component
    {
        Scene activeScene = SceneManager.GetActiveScene();
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null) continue;

            GameObject go = candidate.gameObject;
            if (!go.scene.IsValid()) continue;
            if (go.scene != activeScene) continue;
            if (go.hideFlags != HideFlags.None) continue;

            return candidate;
        }

        return null;
    }

    private static GameObject FindAncestorByName(Transform start, string targetName)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.name == targetName)
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        return null;
    }

    private static GameObject FindGameObjectInActiveScene(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Transform[] candidates = Resources.FindObjectsOfTypeAll<Transform>();

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform t = candidates[i];
            if (t == null) continue;
            if (t.name != objectName) continue;

            GameObject go = t.gameObject;
            if (!go.scene.IsValid()) continue;
            if (go.scene != activeScene) continue;
            if (go.hideFlags != HideFlags.None) continue;

            return go;
        }

        return null;
    }

    private void PickUpItem()
    {
        if (currentItem == null) return;

        currentItem.Pickup();
        currentItem = null;
        canInteract = false;

        if (Player.Instance != null)
        {
            Player.Instance.OnInteractionFinished();
        }
    }

    private void StartDialogue()
    {
        Vector2 dir = (transform.position - currentNPC.transform.position).normalized;
        currentNPC.FaceDirection(dir);

        UIManager.Instance.StartDialogue(currentNPC.dialogueData, () =>
        {
            if (Player.Instance != null)
            {
                Player.Instance.OnInteractionFinished();
            }
        });
    }

    private void OnTriggerEnter2D(Collider2D other)
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

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowSelectPanel(
                    "Campfire?",
                    "Yes",
                    () => { EnterCrafting(); },
                    "No",
                    () => { }
                );
            }
        }
    }

    public void EnterCrafting()
    {
        EnsureUiReferences();

        if (craftingOpenTransitionRoutine != null)
        {
            StopCoroutine(craftingOpenTransitionRoutine);
        }

        craftingOpenTransitionRoutine = StartCoroutine(EnterCraftingWithTransition());
    }

    public void ForceCloseCraftingUI()
    {
        if (craftingOpenTransitionRoutine != null)
        {
            StopCoroutine(craftingOpenTransitionRoutine);
            craftingOpenTransitionRoutine = null;
        }

        isCampfire = false;

        if (craftUI != null)
        {
            craftUI.ForceCloseImmediate();
        }
        else if (craftingMenu != null)
        {
            craftingMenu.SetActive(false);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        NPC exitedNpc = other.GetComponent<NPC>();
        if (exitedNpc != null && exitedNpc == currentNPC)
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

    private IEnumerator EnterCraftingWithTransition()
    {
        float halfDuration = CraftingOpenTransitionSeconds * 0.5f;
        UIManager uiManager = UIManager.Instance;

        if (uiManager != null && uiManager.fadeImage != null)
        {
            yield return StartCoroutine(uiManager.FadeOut(halfDuration));
        }

        OpenCraftingUiImmediate();

        if (uiManager != null && uiManager.fadeImage != null)
        {
            yield return StartCoroutine(uiManager.FadeIn(halfDuration));
        }

        craftingOpenTransitionRoutine = null;
    }

    private void OpenCraftingUiImmediate()
    {
        if (craftingMenu != null)
        {
            craftingMenu.SetActive(true);
        }
        else if (craftUI != null)
        {
            craftUI.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("[PlayerInteraction] EnterCrafting failed: CraftingMenu reference is missing.");
        }

        if (inventoryUI != null)
        {
            inventoryUI.gameObject.SetActive(true);
            inventoryUI.RefreshUI();
        }
        else
        {
            Debug.LogError("[PlayerInteraction] EnterCrafting failed: InventoryUI reference is missing.");
        }
    }
}
