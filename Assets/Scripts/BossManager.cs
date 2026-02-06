using UnityEngine;

public class BossManager : MonoBehaviour
{
    public static BossManager Instance;
    public ThreeWitchCombat threeWitchCombat;
    public RolietCombat rolietCombat;

    [Header("Bosses by Room Name")]
    public BossData[] roomBosses;  

    [System.Serializable]
    public class BossData {
        public string roomID; 
        public BossType type;
        public MonoBehaviour bossCombat;
    }

    public enum BossType { ThreeWitch, RJ } 

    public bool IsBossActive { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartBossBattle(RoomData currentRoom)
{
    Debug.Log($"[BossManager] RoomID: {currentRoom.roomID}");
    
    IsBossActive = true;

    foreach (BossData boss in roomBosses)
    {
        Debug.Log($"[BossManager] Checking roomID: {boss.roomID}");  
        if (boss.roomID == currentRoom.roomID)
        {

            Debug.Log($"[BossManager] MATCH! Type: {boss.type}");  
            if (boss.bossCombat != null && boss.bossCombat.gameObject != null) 
            {
                boss.bossCombat.gameObject.SetActive(true);
                Debug.Log($"[BossManager] Activated: {boss.bossCombat.name}");
                if (boss.bossCombat is ThreeWitchCombat witch) {
                    witch.StartBattle();
                } 
                else if (boss.bossCombat is RolietCombat rolietCombat) {
                    rolietCombat.StartBattle();
                }
            }
            return;
        }
    }
    Debug.LogWarning($"[BossManager] No boss found for {currentRoom.roomID}");
}


    public void EndBossBattle()
    {
        IsBossActive = false; // "���� ��" ��� ����

        Debug.Log("���� óġ! ���� �̵� �����մϴ�.");
    }
}