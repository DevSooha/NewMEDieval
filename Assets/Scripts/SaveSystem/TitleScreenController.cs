using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreenController : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;

    private void Start()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(OnNewGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinue);

            bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSaveFile();
            continueButton.interactable = hasSave;
        }
    }

    private void OnNewGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.DeleteSave();
        }

        if (BossDefeatTracker.Instance != null)
        {
            BossDefeatTracker.Instance.ClearAll();
        }

        SceneManager.LoadScene("Field");
    }

    private void OnContinue()
    {
        if (SaveManager.Instance == null) return;

        SaveData data = SaveManager.Instance.Load();
        if (data == null)
        {
            Debug.LogWarning("[TitleScreen] No save data found.");
            return;
        }

        SaveManager.Instance.ApplyLoadedData(data);
        SceneManager.LoadScene("Field");
    }
}
