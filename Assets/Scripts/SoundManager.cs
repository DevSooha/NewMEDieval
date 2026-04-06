using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    AudioSource audioSource;
    public static SoundManager instance;
    public RoomData roomData;
    private string currentRoomID;

    [Header("BGM's")]
    public AudioClip springBGM;
    public AudioClip summerBGM;
    public AudioClip fallBGM;


    [Header("Boss Music")]
    public AudioClip witchesBGM;
    public AudioClip rolietBGM;
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

   

    public void PlayBGMForScene(string roomID)
    {
        AudioClip clip = null;
        currentRoomID = roomID;

        switch (currentRoomID)
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
    
        PlayNewClip(clip);
        Debug.Log("Playing music");
        
    }
    public void PlayBGMForBoss()
    {
        
        if (currentRoomID == null) return;

        AudioClip clip = null;

        switch (currentRoomID)
        {
            case "spr_4":
                clip = witchesBGM;
                break;
            case "sum_3":
                clip = rolietBGM;
                break;
        }
        
        PlayNewClip(clip);
    }
    public void PlayNewClip(AudioClip clip)
    {
        if (clip == null) return;
        
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

}

