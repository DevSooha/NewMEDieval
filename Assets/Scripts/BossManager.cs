using UnityEngine;

public class BossManager : MonoBehaviour
{
    public static BossManager Instance;
    public BossAI bossAI;


    public bool IsBossActive { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartBossBattle()
    {
        if (bossAI != null)
        {
            IsBossActive = true; // "전투 중" 깃발 올림
            bossAI.gameObject.SetActive(true);
            bossAI.StartBattle();
        }
    }

    public void EndBossBattle()
    {
        IsBossActive = false; // "전투 끝" 깃발 내림

        Debug.Log("보스 처치! 이제 이동 가능합니다.");
    }
}