using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FinalBossSceneTransitionController : MonoBehaviour
{
    [SerializeField] private float defaultFadeOutDuration = 0.5f;

    public IEnumerator TransitionToHiddenScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("FinalBossSceneTransitionController: sceneName is empty.");
            yield break;
        }

        if (UIManager.Instance != null)
        {
            yield return UIManager.Instance.FadeOut(defaultFadeOutDuration);
        }

        SceneManager.LoadScene(sceneName);
    }
}
