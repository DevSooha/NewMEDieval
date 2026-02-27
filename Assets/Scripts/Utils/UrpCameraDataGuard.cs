using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UrpCameraDataGuard
{
    private const string UrpCameraDataTypeName =
        "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureCameraDataForAllCameras();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureCameraDataForAllCameras();
    }

    private static void EnsureCameraDataForAllCameras()
    {
        Type cameraDataType = Type.GetType(UrpCameraDataTypeName);
        if (cameraDataType == null)
        {
            return;
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Camera cam in cameras)
        {
            if (cam == null)
            {
                continue;
            }

            if (cam.gameObject.GetComponent(cameraDataType) == null)
            {
                cam.gameObject.AddComponent(cameraDataType);
            }
        }
    }
}
