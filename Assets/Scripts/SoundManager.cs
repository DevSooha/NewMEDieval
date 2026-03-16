using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    [Header("BGM's")]
    public AudioClip springBGM;
    public AudioClip summerBGM;
    public AudioClip fallBGM;

    AudioSource audioSource;
    public static SoundManager instance;

    void Awake()
    {
        // 중복 방지 + 씬 전환 시 파괴 안됨
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
    }

    public void OnSceneLoaded(RoomData roomData)
    {        
        if (roomData == null) return;
        PlayBGMForScene(roomData.roomID);
    }

    void PlayBGMForScene(string roomID)
    {
        AudioClip clip = null;

        switch (roomID)
        {
            case "spr_1" or "spr_2" or "spr_3" or "spr_4" or "spr_5" or "spr_6" or "spr_7":
                clip = springBGM;
                break;
            case "sum_1" or "sum_2" or "sum_3":
                clip = summerBGM;
                break;                
            case "aut_1" or "aut_2" or "aut_3": 
                clip = fallBGM;
                break;
        }
        if (clip == null) return;
        if (audioSource.clip == clip && audioSource.isPlaying)
            return;

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();  // AudioSource.Play로 재생[web:10]

        Debug.Log("Playing music");
        
    }
}

