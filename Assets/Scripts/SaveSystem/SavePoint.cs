using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어가 진입하면 저장 UI를 표시하는 세이브 포인트.
/// Bonfire는 이 클래스를 상속한다.
/// savePointId는 에디터 OnValidate에서 GUID가 자동 부여된다.
/// </summary>
public class SavePoint : MonoBehaviour
{
    [SerializeField] protected string savePointId;

    public string SavePointId => savePointId;

    // 씬 로드 직후 플레이어가 콜라이더 안에 있을 때 UI가 즉시 뜨는 것을 방지
    private bool interactionReady;

    protected virtual void Start()
    {
        StartCoroutine(EnableAfterDelay());
    }

    private IEnumerator EnableAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1f);
        interactionReady = true;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!interactionReady) return;
        ShowSaveMenu();
    }

    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        UIManager.Instance?.HideSelectPanel();
    }

    protected virtual string SaveMenuTitle => "Bonfire";

    private void ShowSaveMenu()
    {
        if (UIManager.Instance == null) return;
        if (UIManager.Instance.IsDialogueActive() || UIManager.Instance.IsSelectPanelActive()) return;

        UIManager.Instance.ShowSelectPanel(
            SaveMenuTitle,
            "SAVE", OnSaveSelected,
            "POTION", OpenCrafting
        );
    }

    private void OnSaveSelected()
    {
        if (UIManager.Instance == null) return;

        UIManager.Instance.ReplaceSelectPanelContent(
            "Save progress?",
            "YES", OnConfirmSave,
            "NO",  () => UIManager.Instance.HideSelectPanel()
        );
    }

    private void OnConfirmSave()
    {
        SaveManager.Instance?.SaveGame(savePointId, transform.position);

        UIManager.Instance?.ReplaceSelectPanelContent(
            "SAVED!", null, null,
            "OK", () => UIManager.Instance.HideSelectPanel()
        );
    }

    private void OpenCrafting()
    {
        UIManager.Instance?.HideSelectPanel();
        Player.Instance?.GetComponent<PlayerInteraction>()?.EnterCrafting();
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(savePointId))
        {
            savePointId = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
