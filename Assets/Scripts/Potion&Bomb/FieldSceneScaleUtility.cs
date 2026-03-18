using System;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class FieldSceneScaleUtility
{
    internal static void ApplyIfNeeded(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (!IsFieldSceneContext(target))
        {
            return;
        }

        target.transform.localScale *= 0.25f;
    }

    private static bool IsFieldSceneContext(GameObject target)
    {
        if (target != null && IsFieldSceneName(target.scene.name))
        {
            return true;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (IsFieldSceneName(activeScene.name))
        {
            return true;
        }

        int loadedSceneCount = SceneManager.sceneCount;
        for (int i = 0; i < loadedSceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (IsFieldSceneName(loadedScene.name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFieldSceneName(string sceneName)
    {
        return string.Equals(sceneName, "FIeld", StringComparison.OrdinalIgnoreCase)
               || string.Equals(sceneName, "Field", StringComparison.OrdinalIgnoreCase);
    }
}
