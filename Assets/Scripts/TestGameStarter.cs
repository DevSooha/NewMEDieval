using UnityEngine;

public class TestGameStarter : MonoBehaviour
{
    [Header("시작 설정")]
    public RoomData startingRoom;
    public Transform player;

    private void Start()
    {
        if (startingRoom == null)
        {
            Debug.LogError("GameStarter: 시작할 방 데이터가 비어있습니다!");
            return;
        }

        RoomManager.Instance.InitializeFirstRoom(startingRoom, Vector3.zero);

        if (player != null)
        {
            player.position = new Vector3(0, -2, 0);
        }
    }
}