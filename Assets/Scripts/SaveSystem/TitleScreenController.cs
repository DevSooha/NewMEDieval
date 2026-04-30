using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreenController : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject settingsNotReadyMessage;

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
            continueButton.interactable = SaveManager.SaveFileExists();
        }

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExit);

        if (settingsNotReadyMessage != null)
            settingsNotReadyMessage.SetActive(false);
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

    private void OnSettings()
    {
        if (settingsNotReadyMessage != null)
            settingsNotReadyMessage.SetActive(true);
    }

    private void OnExit()
    {
        Application.Quit();
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
