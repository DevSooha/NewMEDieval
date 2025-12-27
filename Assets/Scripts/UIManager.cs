using UnityEngine;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("연결할 UI")]
    public GameObject messagePanel;
    public TextMeshProUGUI messageText;

    private Coroutine currentRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // [기존] 2초 뒤에 꺼지는 경고창
    public void ShowWarning(string message)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        messagePanel.SetActive(true);
        messageText.text = message;

        currentRoutine = StartCoroutine(HideRoutine());
    }

    // ★ [추가] 엔딩용: 절대 자동으로 안 꺼짐!
    public void ShowEnding(string message)
    {
        // 혹시 켜져있던 끄기 타이머가 있다면 취소
        if (currentRoutine != null) StopCoroutine(currentRoutine);

        messagePanel.SetActive(true);
        messageText.text = message;

        // HideRoutine()을 시작하지 않음 -> 계속 떠 있음
    }

    IEnumerator HideRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        messagePanel.SetActive(false);
    }
}