using System.Collections;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public static bool DialogueActive => dialogueActive;
    public static bool SelectionActive => Instance != null && Instance.SelectPanel != null && Instance.SelectPanel.activeSelf;

    private static int pauseRequestCount = 0;

    public static void RequestPause()
    {
        pauseRequestCount++;
        Time.timeScale = 0f;
    }

    public static void ReleasePause()
    {
        pauseRequestCount = Mathf.Max(0, pauseRequestCount - 1);
        if (pauseRequestCount == 0)
        {
            Time.timeScale = 1f;
        }
    }

    public static void ForceResetPause()
    {
        pauseRequestCount = 0;
        Time.timeScale = 1f;
    }

    [Header("Fade Settings")]
    public Image fadeImage;

    [Header("Message UI Settings")]
    public GameObject messagePanel;
    public TextMeshProUGUI messageText;
    private Coroutine messageRoutine;

    [Header("Dialogue UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Image npcIllustrationImage;
    [SerializeField] private GameObject dialogueDimmer;
    private Image dialogueDimmerImage;


    [Header("Dialogue Typing Settings")]
    [SerializeField] private float typingSpeed = 0.03f; // 30ms per character
    [SerializeField] private float sentenceDelay = 0.1f; // 100ms after sentence

    private DialogueData currentDialogue;
    private int currentLineIndex = 0;
    private static bool dialogueActive = false;
    private bool isTyping = false;
    private Coroutine typingCoroutine;
    private Action onDialogueEndedCallback;

    [Header("Select UI References")]
    public GameObject SelectPanel;
    public TextMeshProUGUI selectText;
    public Button btn1;
    public Button btn2;
    private int selectedChoiceIndex = 0;
    private GameObject selectBlocker;

    [Header("Select UI Colors")]
    [SerializeField] private Color selectNormalColor = Color.white;
    [SerializeField] private Color selectHoverColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color selectTextNormalColor = Color.black;
    [SerializeField] private Color selectTextHoverColor = Color.white;
    [SerializeField] private float selectScale = 1.08f;


    void Awake()
    {
        Instance = this;
        pauseRequestCount = 0;
        dialogueActive = false;
        Time.timeScale = 1f;
        if (messagePanel != null) messagePanel.SetActive(false);
        if (fadeImage != null)
        {
            StretchToParent(fadeImage.rectTransform);
            fadeImage.color = new Color(0, 0, 0, 1f);
            fadeImage.raycastTarget = false;
            fadeImage.gameObject.SetActive(true);
            fadeImage.transform.SetAsLastSibling();
        }
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (SelectPanel != null) SelectPanel.SetActive(false);
        if (dialogueDimmer != null)
        {
            RectTransform dimmerRect = dialogueDimmer.GetComponent<RectTransform>();
            StretchToParent(dimmerRect);
            dialogueDimmerImage = dialogueDimmer.GetComponent<Image>();
            if (dialogueDimmerImage != null) dialogueDimmerImage.raycastTarget = false;
            dialogueDimmer.SetActive(false);
            dialogueDimmer.transform.SetAsLastSibling();
        }
    }

    void Update()
    {
        HandleSelectPanelInput();
    }

    #region FadeImage - FadeIn, FadeOut, LoadSceneWithFade
    public IEnumerator FadeIn(float duration)
    {
        if (fadeImage == null) yield break;
        fadeImage.transform.SetAsLastSibling();
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, 1f - (t / duration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 0);
    }

    public IEnumerator FadeOut(float duration)
    {
        if (fadeImage == null) yield break;
        fadeImage.transform.SetAsLastSibling();
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, t / duration);
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, 1);
    }

    IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (Player.Instance != null) Player.Instance.SaveCurrentPosition();
        yield return StartCoroutine(FadeOut(0.5f));
        SceneManager.LoadScene(sceneName);
    }
    #endregion

    #region MessagePanel - ShowWarning, ShowEnding, HideMessage

    public void ShowWarning(string message)
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);

        messagePanel.SetActive(true);
        messageText.text = message;
        messagePanel.transform.SetAsLastSibling();

        messageRoutine = StartCoroutine(HideMessageRoutine());
    }

    public void ShowEnding(string message)
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);

        messagePanel.SetActive(true);
        messageText.text = message;
        messagePanel.transform.SetAsLastSibling();
    }

    public void HideMessage()
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messagePanel.SetActive(false);
    }

    IEnumerator HideMessageRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        messagePanel.SetActive(false);
    }
    #endregion

    #region DialoguePanel - StartDialogue, DisplayLine, AdvanceDialogue, EndDialogue, IsDialogueActive

    public void StartDialogue(DialogueData dialogue, Action onEnd = null)
    {
        if (dialogue == null || dialogue.dialogueLines.Count == 0)
        {
            Debug.LogWarning("Dialogue data is empty or null!");
            return;
        }

        currentDialogue = dialogue;
        currentLineIndex = 0;
        dialogueActive = true;
        RequestPause();

        onDialogueEndedCallback = onEnd;
        SetDimmerActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        if (nameText != null)
        {
            nameText.text = dialogue.npcName;
        }

        if (npcIllustrationImage != null && dialogue.npcIllustration != null)
        {
            npcIllustrationImage.sprite = dialogue.npcIllustration;
            npcIllustrationImage.enabled = true;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.transform.SetAsLastSibling();
        }
        if (dialogueDimmer != null)
        {
            dialogueDimmer.transform.SetAsLastSibling();
            dialoguePanel.transform.SetAsLastSibling();
        }
        DisplayLine();
    }

    void DisplayLine()
    {
        if (currentLineIndex < currentDialogue.dialogueLines.Count)
        {
            string line = currentDialogue.dialogueLines[currentLineIndex].text;

            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }

            typingCoroutine = StartCoroutine(TypeLine(line));
        }
        else
        {
            EndDialogue();
        }
    }

    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char letter in line.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSecondsRealtime(typingSpeed);

            if (letter == '.' || letter == '!' || letter == '?')
            {
                yield return new WaitForSecondsRealtime(sentenceDelay);
            }
        }

        isTyping = false;
    }

    public void AdvanceDialogue()
    {
        if (isTyping)
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }

            dialogueText.text = currentDialogue.dialogueLines[currentLineIndex].text;
            isTyping = false;
        }
        else
        {
            currentLineIndex++;
            DisplayLine();
        }
    }

    void EndDialogue()
    {
        dialogueActive = false;
        ReleasePause();
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        SetDimmerActive(false);
        currentDialogue = null;
        currentLineIndex = 0;

        if (onDialogueEndedCallback != null)
        {
            onDialogueEndedCallback?.Invoke();
            onDialogueEndedCallback = null;
        }

        if (npcIllustrationImage != null)
        {
            npcIllustrationImage.enabled = false;
        }
    }

    public bool IsDialogueActive()
    {
        return dialogueActive;
    }

    public bool IsSelectPanelActive()
    {
        return SelectPanel != null && SelectPanel.activeSelf;
    }


    #endregion

    #region SelectPanel - ShowSelectPanel, HideSelectPanel

    public void ShowSelectPanel(string selectLabel, string btn1Text, UnityAction action1, string btn2Text, UnityAction action2)
    {
        if (SelectPanel == null) return;
        RequestPause();
        EnsureSelectBlocker();
        SetSelectBlockerActive(true);
        SelectPanel.SetActive(true);
        SelectPanel.transform.SetAsLastSibling();

        if (selectText != null)
        {
            selectText.text = selectLabel;
        }

        if (btn1 != null)
        {
            TextMeshProUGUI btn1Label = btn1.GetComponentInChildren<TextMeshProUGUI>();
            if (btn1Label != null) btn1Label.text = btn1Text;
            btn1.onClick.RemoveAllListeners();
            btn1.onClick.AddListener(() =>
            {
                action1?.Invoke();
                HideSelectPanel();
            });
        }

        if (btn2 != null)
        {
            TextMeshProUGUI btn2Label = btn2.GetComponentInChildren<TextMeshProUGUI>();
            if (btn2Label != null) btn2Label.text = btn2Text;
            btn2.onClick.RemoveAllListeners();
            btn2.onClick.AddListener(() =>
            {
                action2?.Invoke();
                HideSelectPanel();
            });
        }

        selectedChoiceIndex = 0;
        FocusSelectedChoice();
    }


    public void HideSelectPanel()
    {
        if (SelectPanel == null) return;
        SelectPanel.SetActive(false);
        ReleasePause();
        SetSelectBlockerActive(false);
        UpdateSelectButtonVisuals();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void HandleSelectPanelInput()
    {
        if (SelectPanel == null || !SelectPanel.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            selectedChoiceIndex = 0;
            FocusSelectedChoice();
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            selectedChoiceIndex = 1;
            FocusSelectedChoice();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space))
        {
            if (selectedChoiceIndex == 0 && btn1 != null)
            {
                btn1.onClick.Invoke();
            }
            else if (selectedChoiceIndex == 1 && btn2 != null)
            {
                btn2.onClick.Invoke();
            }
        }
    }

    private void FocusSelectedChoice()
    {
        if (EventSystem.current == null) return;

        if (selectedChoiceIndex == 0 && btn1 != null)
        {
            EventSystem.current.SetSelectedGameObject(btn1.gameObject);
            btn1.Select();
        }
        else if (selectedChoiceIndex == 1 && btn2 != null)
        {
            EventSystem.current.SetSelectedGameObject(btn2.gameObject);
            btn2.Select();
        }

        UpdateSelectButtonVisuals();
    }

    private void UpdateSelectButtonVisuals()
    {
        ApplyButtonVisual(btn1, selectedChoiceIndex == 0);
        ApplyButtonVisual(btn2, selectedChoiceIndex == 1);
    }

    private void ApplyButtonVisual(Button button, bool selected)
    {
        if (button == null) return;
        Graphic graphic = button.targetGraphic;
        if (graphic != null)
        {
            graphic.color = selected ? selectHoverColor : selectNormalColor;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.color = selected ? selectTextHoverColor : selectTextNormalColor;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }

        Vector3 targetScale = selected ? Vector3.one * selectScale : Vector3.one;
        button.transform.localScale = targetScale;
    }

    private void EnsureSelectBlocker()
    {
        if (selectBlocker != null) return;

        Transform parent = SelectPanel != null ? SelectPanel.transform.parent : null;
        if (parent == null) parent = transform;

        selectBlocker = new GameObject("SelectBlocker", typeof(RectTransform), typeof(Image));
        selectBlocker.transform.SetParent(parent, false);

        Image blockerImage = selectBlocker.GetComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        blockerImage.raycastTarget = true;

        RectTransform rect = selectBlocker.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        selectBlocker.SetActive(false);
    }

    private void SetSelectBlockerActive(bool active)
    {
        if (selectBlocker == null) return;
        selectBlocker.SetActive(active);
        if (active)
        {
            selectBlocker.transform.SetAsLastSibling();
            if (SelectPanel != null)
            {
                SelectPanel.transform.SetAsLastSibling();
            }
        }
    }

    void OnEnable()
    {
        PlayerHealth.OnPlayerDeath += HandleGameOver;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerDeath -= HandleGameOver;
    }

    void HandleGameOver()
    {
        ShowSelectPanel(
            "YOU DIED ...",
            "Restart",
            RestartGame,
            "Quit",
            QuitGame
        );
    }

    void RestartGame()
    {
        HideSelectPanel();
        StartCoroutine(RestartGameRoutine());
    }

    IEnumerator RestartGameRoutine()
    {
        if (fadeImage != null)
        {
            yield return StartCoroutine(FadeOut(0.5f));
        }

        if (Player.Instance != null)
        {
            PlayerInteraction interaction = Player.Instance.GetComponentInChildren<PlayerInteraction>(true);
            if (interaction != null)
            {
                interaction.ForceCloseCraftingUI();
            }
        }

        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.SetRestartPositionToCurrentDoor();
        }

        if (Player.Instance != null)
        {
            Player.Instance.gameObject.SetActive(true);

            var health = Player.Instance.GetComponent<PlayerHealth>();
            if (health != null) health.Resurrect();
        }

        SceneManager.LoadScene("Field");
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetDimmerActive(bool active)
    {
        if (dialogueDimmer == null) return;
        dialogueDimmer.SetActive(active);
        if (dialogueDimmerImage != null)
        {
            dialogueDimmerImage.raycastTarget = active;
        }
        if (active)
        {
            dialogueDimmer.transform.SetAsLastSibling();
        }
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null) return;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    #endregion
}
