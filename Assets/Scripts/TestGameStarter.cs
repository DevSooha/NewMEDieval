using UnityEngine;
using System.Collections; // �ڷ�ƾ ����� ���� �߰�

public class TestGameStarter : MonoBehaviour
{
    [Header("���� ����")]
    public RoomData startingRoom;
    public Transform player;
    public Transform playerSpawnPointOverride;
    public string playerSpawnPointName = "PlayerSpawnPoint";

    private bool spawnPointRetryQueued = false;

    private IEnumerator Start()
    {
        if (startingRoom == null)
        {
            Debug.LogError("GameStarter: ������ �� �����Ͱ� ����ֽ��ϴ�!");
            yield break;
        }

        // 1. RoomManager�� ���� �ʱ�ȭ�ǰ� ���� ������ �ð��� �ֱ� ���� 1������ ���
        // (RoomManager.Start()�� ����Ǿ� �÷��̾� ��ġ�� 0,0,0���� �����ϴ� ���� ����)
        yield return null;

        // RoomManager�� ���� ���� �������� �ʾҴٸ� ���� ��û
        if (RoomManager.Instance.currentRoomData == null)
        {
            RoomManager.Instance.InitializeFirstRoom(startingRoom, Vector3.zero);
        }

        // 2. �� ���� ���� ������Ʈ���� Ȱ��ȭ�ǰ� �˻� ������ ���°� �ǵ��� �����ϰ� �� �� �� ���
        yield return null;

        if (player != null)
        {
            MovePlayerToSpawnPoint();
        }
    }

    private void MovePlayerToSpawnPoint()
    {
        // �⺻ ��ġ ����
        Vector3 spawnPos = new Vector3(0, -2, 0);
        Transform spawnPoint = playerSpawnPointOverride;

        // 0. ���� �� ���ο��� ĳ�õ� ���� ����Ʈ ��ȸ (���� ����)
        if (spawnPoint == null && RoomManager.Instance != null)
        {
            spawnPoint = RoomManager.Instance.GetSpawnPointForCurrentRoom(playerSpawnPointName);
        }

        // 1. Ȱ��ȭ�� ������Ʈ �߿��� �켱 �˻� (����)
        if (spawnPoint == null)
        {
            GameObject spawnPointObj = GameObject.Find(playerSpawnPointName);
            if (spawnPointObj != null) spawnPoint = spawnPointObj.transform;
        }

        if (spawnPoint != null)
        {
            spawnPos = spawnPoint.position;
            Debug.Log($"[TestGameStarter] ���� ����Ʈ �߰�: {spawnPos}");
        }
        else
        {
            if (!spawnPointRetryQueued)
            {
                spawnPointRetryQueued = true;
                StartCoroutine(RetrySpawnPointNextFrame());
                return;
            }

            Debug.LogWarning($"[TestGameStarter] '{playerSpawnPointName}'�� ã�� �� ���� �⺻ ��ġ(0, -2, 0)�� �̵��մϴ�.");
        }

        player.position = spawnPos;

        // �÷��̾� �̵� �� ī�޶� ����ȭ (RoomManager�� �ʱ�ȭ�� �� ī�޶� ������ ���� ���� �� ����)
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.SyncCameraToPlayer();
        }
    }

    private IEnumerator RetrySpawnPointNextFrame()
    {
        yield return null;
        MovePlayerToSpawnPoint();
    }
}