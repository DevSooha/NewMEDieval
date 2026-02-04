using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Spawner : MonoBehaviour
{
    public TextAsset csvFile;
    public Tilemap floorTilemap;

    [Header("공용 아이템 프리팹")]
    public GameObject worldItemPrefab;

    [System.Serializable]
    public struct SpawnMapping
    {
        public string itemID;
        public string spritePath;
        public ItemData itemData;
    }

    public List<SpawnMapping> spawnList;

    void Start()
    {
        SpawnFromCSV();
    }

    public void SpawnFromCSV()
    {
        if (csvFile == null) return;

        ClearPreviousSpawns();

        string[] lines = csvFile.text.Split(
            new[] { '\n', '\r' },
            System.StringSplitOptions.RemoveEmptyEntries
        );

        for (int i = 1; i < lines.Length; i++)
        {
            string[] data = lines[i].Split(',');
            if (data.Length < 6) continue;

            string itemID = data[0].Trim();
            string mapID = data[4].Trim();
            string rateStr = data[5].Trim().Replace("%", "");

            if (mapID != "spr_3") continue;

            float spawnRate = float.Parse(rateStr) / 100f;

            // 리스트에서 ID에 맞는 맵핑 데이터 찾기
            SpawnMapping mapping = spawnList.Find(x => x.itemID == itemID);

            if (!string.IsNullOrEmpty(mapping.itemID))
            {
                TrySpawn(mapping, spawnRate, itemID);
            }
        }
    }

    void TrySpawn(SpawnMapping mapping, float rate, string id)
    {
        if (Random.value > rate) return;

        BoundsInt bounds = floorTilemap.cellBounds;

        for (int attempts = 0; attempts < 100; attempts++)
        {
            Vector3Int randomCell = new Vector3Int(
                Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax),
                0
            );

            if (!floorTilemap.HasTile(randomCell)) continue;

            Vector3 spawnPos = floorTilemap.GetCellCenterWorld(randomCell);
            spawnPos.z = 0;

            Collider2D hit = Physics2D.OverlapCircle(spawnPos, 0.3f);
            if (hit != null && hit.CompareTag("Obstacle")) continue;

            // 1. 아이템 생성
            GameObject item = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity, transform);

            WorldItem worldItemScript = item.GetComponent<WorldItem>();
            if (worldItemScript != null)
            {
                if (mapping.itemData != null)
                {
                    worldItemScript.Init(mapping.itemData, 1);
                }
            }

            Sprite sprite = Resources.Load<Sprite>(mapping.spritePath);
            if (sprite != null)
            {
                item.GetComponent<SpriteRenderer>().sprite = sprite;
            }

            // 4. 태그 및 레이어 설정 (PlayerInteraction이 찾을 수 있게)
            item.tag = "Respawn";
            item.layer = 0;

            item.SetActive(true);
            return;
        }
    }

    void ClearPreviousSpawns()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            // 태그가 Item이든 Respawn이든 Spawner 자식이면 다 지움
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}