using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    public static bool DialogueActive => dialogueActive;
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Image npcIllustrationImage;

    [Header("Typing Settings")]
    [SerializeField] private float typingSpeed = 0.03f; // 30ms per character
    [SerializeField] private float sentenceDelay = 0.1f; // 100ms after sentence

    private DialogueData currentDialogue;
    private int currentLineIndex = 0;
    private static bool dialogueActive = false;
    private bool isTyping = false;
    private Coroutine typingCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

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
}
