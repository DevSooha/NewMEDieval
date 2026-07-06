using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _applicationIsQuitting = false;

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting) return null;

            if (_instance == null)
            {
                // 죽어서 비활성화된 싱글톤(예: 사망 후 SetActive(false)된 Player)도
                // 찾도록 비활성 포함 검색 — 비활성이라고 새 인스턴스를 만들면 안 된다.
                _instance = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

                if (_instance == null)
                {
                    // 플레이 중이 아닐 때(에디터 정리/씬 닫기 중 OnDestroy 등)는 lazy 생성 금지.
                    // 여기서 생성된 "(Singleton)" 오브젝트가 씬 클린업 경고
                    // "Some objects were not cleaned up when closing the scene"의 원인이었다.
                    if (!Application.isPlaying) return null;

                    GameObject obj = new GameObject(typeof(T).Name + " (Singleton)");
                    _instance = obj.AddComponent<T>();
                    DontDestroyOnLoad(obj);
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);

            // 오브젝트가 비활성 상태면 OnApplicationQuit을 받지 못하므로
            // 정적 이벤트로도 종료 플래그를 세운다 (사망한 Player 케이스).
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private static void MarkQuitting()
    {
        _applicationIsQuitting = true;
    }

    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }
}