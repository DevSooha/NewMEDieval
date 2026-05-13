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
            continueButton.interactable = SaveManager.Instance != null && SaveManager.Instance.HasSaveFile();
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

        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSaveFile();

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
        SaveManager.Instance?.DeleteSave();

        BossDefeatTracker.Instance?.ClearAll();

        SceneManager.LoadScene("FIeld");
    }

    private void OnContinue()
    {
        if (SaveManager.Instance == null)
        {
            SceneManager.LoadScene("FIeld");
            return;
        }

        SaveData data = SaveManager.Instance.Load();
        if (data != null)
        {
            SaveManager.Instance.ApplyLoadedData(data);
        }

        SceneManager.LoadScene("FIeld");
    }
}
