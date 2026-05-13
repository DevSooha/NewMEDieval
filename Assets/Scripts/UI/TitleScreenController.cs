using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreenController : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;

    private bool isLoading = false;

    private void Start()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(OnNewGame);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinue);

            SaveManager saveManager = FindFirstObjectByType<SaveManager>();
            bool hasSave = saveManager != null && saveManager.HasSaveFile();
            continueButton.interactable = hasSave;
        }
    }

    private void Update()
    {
        if (isLoading) return;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        isLoading = true;

        SaveManager saveManager = FindFirstObjectByType<SaveManager>();
        bool hasSave = saveManager != null && saveManager.HasSaveFile();

        if (hasSave)
        {
            OnContinue();
        }
        else
        {
            OnNewGame();
        }
    }

    private void OnNewGame()
    {
        SaveManager saveManager = FindFirstObjectByType<SaveManager>();
        if (saveManager != null)
        {
            saveManager.DeleteSave();
        }

        BossDefeatTracker tracker = FindFirstObjectByType<BossDefeatTracker>();
        if (tracker != null)
        {
            tracker.ClearAll();
        }

        SceneManager.LoadScene("Field");
    }

    private void OnContinue()
    {
        SaveManager saveManager = FindFirstObjectByType<SaveManager>();
        if (saveManager == null)
        {
            SceneManager.LoadScene("Field");
            return;
        }

        SaveData data = saveManager.Load();
        if (data != null)
        {
            saveManager.ApplyLoadedData(data);
        }

        SceneManager.LoadScene("Field");
    }
}
