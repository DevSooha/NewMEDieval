using System.Collections;
using System;
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
    [SerializeField] private GameObject dialogueDimmer;


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

        onDialogueEndedCallback = onEnd;
        dialogueDimmer.SetActive(true);
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
        dialogueDimmer.SetActive(false);
        currentDialogue = null;
        currentLineIndex = 0;

        // 저장해둔 행동(상태 복귀) 실행
        if (onDialogueEndedCallback != null)
        {
            onDialogueEndedCallback?.Invoke();
            onDialogueEndedCallback = null; // 초기화
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

    // 1. 객체가 활성화될 때 이벤트 연결 (구독)
    void OnEnable()
    {
        // PlayerHealth의 OnPlayerDeath 이벤트가 발생하면 HandleGameOver를 실행하라고 '등록'
        PlayerHealth.OnPlayerDeath += HandleGameOver;
    }

    // 2. 객체가 비활성화되거나 삭제될 때 이벤트 끊기 (구독 해제)
    void OnDisable()
    {
        PlayerHealth.OnPlayerDeath -= HandleGameOver;
    }

    void HandleGameOver()
    {
        ShowSelectPanel(
            "YOU DIED ...", // 메세지
            "Restart",             // 버튼 1 텍스트
            RestartGame,           // 버튼 1 기능 (씬 재시작)
            "Quit",             // 버튼 2 텍스트
            QuitGame               // 버튼 2 기능 (게임 끄기)
        );
    }

    // [수정] 기능 1: 게임 재시작 (Field 씬 새로고침)
    void RestartGame()
    {
        // 1. RoomManager에게 재시작 위치 저장 요청
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.SetRestartPositionToCurrentDoor();
        }

        // 2. ★ [핵심] 플레이어 강제 부활 및 활성화
        if (Player.Instance != null)
        {
            // 죽으면서 비활성화(SetActive(false))되었다면 다시 켜야 함
            Player.Instance.gameObject.SetActive(true);

            // (선택) 체력 초기화 로직이 있다면 호출
            var health = Player.Instance.GetComponent<PlayerHealth>();
            if (health != null) health.Resurrect();
        }

        // 3. 씬 다시 로드
        StartCoroutine(LoadSceneWithFade("Field"));
    }

    // [수정] 기능 2: 게임 종료
    void QuitGame()
    {
        // 에디터에서는 플레이 모드를 끄고, 빌드된 게임에서는 창을 닫습니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

        #endregion  

    

}