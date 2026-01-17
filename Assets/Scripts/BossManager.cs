using UnityEngine;

public class BossManager : MonoBehaviour
{
    public static BossManager Instance;
    public ThreeWitchCombat threeWitchCombat;

    public bool IsBossActive { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartBossBattle()
    {
        if (threeWitchCombat != null)
        {
            IsBossActive = true; // "전투 중" 깃발 올림
            threeWitchCombat.gameObject.SetActive(true);
            threeWitchCombat.StartBattle();
        }
    }

    public void EndBossBattle()
    {
        IsBossActive = false; // "전투 끝" 깃발 내림

        Debug.Log("보스 처치! 이제 이동 가능합니다.");
    }
}