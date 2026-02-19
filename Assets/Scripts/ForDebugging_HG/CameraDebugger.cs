using UnityEngine;

public class CameraDebugger : MonoBehaviour
{
    void Start()
    {
        DebugActiveCameras();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
            DebugActiveCameras();
    }

    void DebugActiveCameras()
    {
        Debug.Log($"[CamDbg] allCamerasCount={Camera.allCamerasCount}, main={(Camera.main ? Camera.main.name : "NULL")}");

        foreach (var cam in Camera.allCameras)
        {
            Debug.Log(
                $"[CamDbg] name={cam.name}, enabled={cam.enabled}, activeInHierarchy={cam.gameObject.activeInHierarchy}, " +
                $"depth={cam.depth}, pos={cam.transform.position}, ortho={cam.orthographic}, orthoSize={cam.orthographicSize}, rect={cam.rect}"
            );
        }
    }
}