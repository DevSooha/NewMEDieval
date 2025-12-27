using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class Spawner : MonoBehaviour
{
    public TextAsset csvFile;
    public Tilemap floorTilemap;

    [System.Serializable]
    public struct SpawnMapping
    {
        public string itemID;
        public GameObject originalTemplate;
    }

    public List<SpawnMapping> spawnList;

    void Awake()
    {
        // 1. 원본들은 숨깁니다.
        foreach (var mapping in spawnList)
        {
            if (mapping.originalTemplate != null)
                mapping.originalTemplate.SetActive(false);
        }
    }

    void Start()
    {
        SpawnFromCSV();
    }

    public void SpawnFromCSV()
    {
        if (csvFile == null) { Debug.LogError("CSV 파일이 연결되지 않았습니다!"); return; }

        ClearPreviousSpawns();

        // 유니티 텍스트 에셋 읽기 시 줄바꿈 처리
        string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        Debug.Log("CSV 줄 수: " + lines.Length);

        for (int i = 1; i < lines.Length; i++)
        {
            string[] data = lines[i].Split(',');
            if (data.Length < 6) continue;

            string itemID = data[0].Trim();
            string mapID = data[4].Trim();
            string rateStr = data[5].Trim().Replace("%", "");

            if (mapID == "spr_3")
            {
                float spawnRate = float.Parse(rateStr) / 100f;
                GameObject template = spawnList.Find(x => x.itemID == itemID).originalTemplate;

                if (template != null)
                {
                    TrySpawnClone(template, spawnRate, itemID);
                }
            }
        }
    }

    void TrySpawnClone(GameObject original, float rate, string id)
    {
        float dice = Random.value;
        if (dice > rate) return;

        BoundsInt bounds = floorTilemap.cellBounds;
        for (int attempts = 0; attempts < 100; attempts++)
        {
            Vector3Int randomCell = new Vector3Int(
                Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax),
                0
            );

            if (floorTilemap.HasTile(randomCell))
            {
                Vector3 spawnPos = floorTilemap.GetCellCenterWorld(randomCell);
                spawnPos.z = 0;

                Collider2D hit = Physics2D.OverlapCircle(spawnPos, 0.3f);

                if (hit == null || !hit.CompareTag("Obstacle"))
                {
                    GameObject clone = Instantiate(original, spawnPos, Quaternion.identity, transform);
                    
                    clone.SetActive(true);
                    clone.tag = "Respawn";
                    return;
                }
            }
        }
        Debug.LogWarning($"{id} 스폰 실패: 위치 못 찾음");
    }

    void ClearPreviousSpawns()
    {
        // 내 하위(자식) 오브젝트들 중에서만 찾아서 파괴
        // 리스트를 역순으로 도는 게 삭제할 때 안전함
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag("Respawn"))
            {
                Destroy(child.gameObject);
            }
        }
    }
}