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
    [SerializeField] private GameObject inGameMenu;
    [SerializeField] private InventoryUI inventoryUI;

    private bool canInteract;
    private bool isCampfire;
    private Coroutine craftingOpenTransitionRoutine;
    private Coroutine controlRecoveryRoutine;
    private const float CraftingOpenTransitionSeconds = 0.5f;
    private const int ControlRecoveryFrames = 3;
    private PlayerAttackSystem playerAttackSystem;
    private PlayerStatusController playerStatusController;

    public bool IsInteractable => canInteract;
    public bool HasImmediateInteractionTarget => currentItem != null || currentNPC != null || isCampfire;
    public bool IsCraftingUiOpen => IsCraftingUiVisible();

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
        playerAttackSystem = GetComponentInParent<PlayerAttackSystem>();
        playerStatusController = GetComponentInParent<PlayerStatusController>();

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

        if (inventoryUI != null)
        {
            inventoryUI.gameObject.SetActive(false);
        }

        if (inGameMenu != null)
        {
            inGameMenu.SetActive(false);
        }
    }

    private void Update()
    {
        bool closePressed = playerStatusController != null
            ? playerStatusController.ProcessActionButtonDown("close_ui", Input.GetKeyDown(KeyCode.X))
            : Input.GetKeyDown(KeyCode.X);

        if (IsCraftingUiVisible() && closePressed)
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

        if (UIManager.Instance != null && UIManager.Instance.IsSelectPanelActive() && closePressed)
        {
            UIManager.Instance.HideSelectPanel();
        }
    }

    public bool TryInteract()
    {
        if (IsCraftingUiVisible())
        {
            return false;
        }

        if (currentItem != null)
        {
            bool picked = PickUpItem();
            if (picked)
            {
                CombatInputHelper.ConsumeAttackInputThisFrame();
                return true;
            }

            return false;
        }

        if (!canInteract) return false;

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
            ShowCampfireSelectPanelIfNeeded();
            CombatInputHelper.ConsumeAttackInputThisFrame();
            return true;
        }

        return false;
    }

    private bool IsCraftingUiVisible()
    {
        EnsureUiReferences();

        return (craftingMenu != null && craftingMenu.activeInHierarchy)
            || (inGameMenu != null && inGameMenu.activeInHierarchy)
            || (craftUI != null && craftUI.gameObject.activeInHierarchy);
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

        if (inGameMenu == null || inGameMenu.scene != activeScene)
        {
            inGameMenu = null;

            if (inventoryUI != null)
            {
                inGameMenu = FindAncestorByName(inventoryUI.transform, "InGameMenu");
            }

            if (inGameMenu == null)
            {
                inGameMenu = FindGameObjectInActiveScene("InGameMenu");
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

    private bool PickUpItem()
    {
        if (currentItem == null) return false;

        bool picked = currentItem.Pickup();
        if (!picked)
        {
            return false;
        }

        currentItem = null;
        canInteract = currentNPC != null || isCampfire;

        if (Player.Instance != null)
        {
            Player.Instance.OnInteractionFinished();
        }

        return true;
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
                ShowCampfireSelectPanelIfNeeded();
            }
        }
    }

    public void EnterCrafting()
    {
        EnsureUiReferences();
        EnsureCombatStateReset();
        CombatInputHelper.ConsumeAttackInputThisFrame();

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
        EnsureCombatStateReset();
        BeginControlRecovery();

        if (craftUI != null)
        {
            craftUI.ForceCloseImmediate();
        }
        else if (craftingMenu != null)
        {
            craftingMenu.SetActive(false);
        }

        if (inventoryUI != null)
        {
            inventoryUI.gameObject.SetActive(false);
        }

        if (inGameMenu != null)
        {
            inGameMenu.SetActive(false);
        }

        if (Player.Instance != null)
        {
            Player.Instance.OnInteractionFinished();
        }
    }

    private void ShowCampfireSelectPanelIfNeeded()
    {
        if (UIManager.Instance == null)
        {
            return;
        }

        if (UIManager.Instance.IsDialogueActive() || UIManager.Instance.IsSelectPanelActive())
        {
            return;
        }

        UIManager.Instance.ShowSelectPanel(
            "Campfire?",
            "Yes",
            EnterCrafting,
            "No",
            () => { }
        );
    }

    public void BeginControlRecoveryAfterCraftingClose()
    {
        BeginControlRecovery();
    }

    private void BeginControlRecovery()
    {
        if (controlRecoveryRoutine != null)
        {
            StopCoroutine(controlRecoveryRoutine);
        }

        controlRecoveryRoutine = StartCoroutine(ControlRecoveryRoutine());
    }

    private IEnumerator ControlRecoveryRoutine()
    {
        for (int i = 0; i < ControlRecoveryFrames; i++)
        {
            EnsureUiReferences();
            EnsureCombatStateReset();

            if (craftingMenu != null)
            {
                craftingMenu.SetActive(false);
            }

            if (craftUI != null)
            {
                craftUI.gameObject.SetActive(false);
            }

            if (inventoryUI != null)
            {
                inventoryUI.gameObject.SetActive(false);
            }

            if (inGameMenu != null)
            {
                inGameMenu.SetActive(false);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideSelectPanel();
            }

            Time.timeScale = 1f;

            if (Player.Instance != null)
            {
                Player.Instance.OnInteractionFinished();
            }

            yield return null;
        }

        controlRecoveryRoutine = null;
    }

    private void EnsureCombatStateReset()
    {
        if (playerAttackSystem == null)
        {
            playerAttackSystem = GetComponentInParent<PlayerAttackSystem>();
        }

        if (playerAttackSystem != null)
        {
            playerAttackSystem.CancelTransientInputState();
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
        else if (craftUI == null)
        {
            Debug.LogError("[PlayerInteraction] EnterCrafting failed: CraftingMenu reference is missing.");
        }

        if (craftUI != null)
        {
            craftUI.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("[PlayerInteraction] EnterCrafting failed: CraftUI reference is missing.");
        }

        if (inGameMenu != null)
        {
            inGameMenu.SetActive(true);
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
