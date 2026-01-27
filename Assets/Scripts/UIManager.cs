using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public static bool DialogueActive => dialogueActive;

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

    [Header("Dialogue Typing Settings")]
    [SerializeField] private float typingSpeed = 0.03f; // 30ms per character
    [SerializeField] private float sentenceDelay = 0.1f; // 100ms after sentence

    private DialogueData currentDialogue;
    private int currentLineIndex = 0;
    private static bool dialogueActive = false;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    [Header("Select UI References")]
    public GameObject SelectPanel;
    public TextMeshProUGUI selectText;
    public Button btn1;
    public Button btn2;


    void Awake()
    {
        Instance = this;
        // �׻� �����ִ°� �ƴ� UI���� �ʱ�ȭ �� ��Ȱ��ȭ
        if (messagePanel != null) messagePanel.SetActive(false);
        if (fadeImage != null) fadeImage.color = new Color(0, 0, 0, 0);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (SelectPanel != null) SelectPanel.SetActive(false);
    }

    #region FadeImage ���� - FadeIn, FadeOut, LoadSceneWithFade
    public IEnumerator FadeIn(float duration)
    {
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

    #region MessagePanel ���� - ShowWarning, ShowEnding, HideMessage

    // [Warning] 2�� �ڿ� �ڵ����� �����
    public void ShowWarning(string message)
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);

        messagePanel.SetActive(true);
        messageText.text = message;

        messageRoutine = StartCoroutine(HideMessageRoutine());
    }

    // [Ending] �ڵ����� ������� ���� (EndingEvent���� ���)
    public void ShowEnding(string message)
    {
        if (messageRoutine != null) StopCoroutine(messageRoutine);

        messagePanel.SetActive(true);
        messageText.text = message;
    }

    // UI�� �������� �ݰ� ���� �� ȣ��
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

    #region DialoguePanel ���� - StartDialogue, DisplayLine, AdvanceDialogue, EndDialogue, IsDialogueActive

    public void StartDialogue(DialogueData dialogue)
    {
        if (dialogue == null || dialogue.dialogueLines.Count == 0)
        {
            Debug.LogWarning("Dialogue data is empty or null!");
            return;
        }

        currentDialogue = dialogue;
        currentLineIndex = 0;
        dialogueActive = true;

        dialoguePanel.SetActive(true);

        if (nameText != null)
        {
            nameText.text = dialogue.npcName;
        }

        if (npcIllustrationImage != null && dialogue.npcIllustration != null)
        {
            npcIllustrationImage.sprite = dialogue.npcIllustration;
            npcIllustrationImage.enabled = true;
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
            yield return new WaitForSeconds(typingSpeed);

            if (letter == '.' || letter == '!' || letter == '?')
            {
                yield return new WaitForSeconds(sentenceDelay);
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
        dialoguePanel.SetActive(false);
        currentDialogue = null;
        currentLineIndex = 0;

        if (npcIllustrationImage != null)
        {
            npcIllustrationImage.enabled = false;
        }
    }

    public bool IsDialogueActive()
    {
        return dialogueActive;
    }


    #endregion

    #region SelectPanel ����- ShowSelectPanel, HideSelectPanel

    public void ShowSelectPanel(string selectLabel, string btn1Text, UnityAction action1, string btn2Text, UnityAction action2)
    {

        if (SelectPanel == null) return;
        SelectPanel.SetActive(true);

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
    }


    public void HideSelectPanel()
    {
        if (SelectPanel == null) return;
        SelectPanel.SetActive(false);
    }

    #endregion


    public void GoCrafting() { StartCoroutine(LoadSceneWithFade("Crafting")); }

    public void ExitCrafting()
{
    StartCoroutine(LoadSceneWithFade("Field"));
}


    

    

}