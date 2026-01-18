using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

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
        //public ItemData itemData;
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

            // ★ [수정됨] spritePath 체크 대신 itemID가 맞는게 있는지 체크
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

            // ★★★ [핵심 수정] WorldItem 컴포넌트 초기화 (Init 호출) ★★★
            WorldItem worldItemScript = item.GetComponent<WorldItem>();

            //if (mapping.itemData != null)
            //{
            //    // 여기서 Init을 해줘야 WorldItem의 initialized가 true가 되어 주워집니다.
            //    worldItemScript.Init(mapping.itemData, 1);
            //}
            //else
            //{
            //    Debug.LogError($"Spawner: {id}에 해당하는 ItemData가 Inspector에 연결되지 않았습니다!");
            //}

            // 2. 스프라이트 설정 (기존 로직 유지)
            Sprite sprite = Resources.Load<Sprite>(mapping.spritePath);
            if (sprite != null)
            {
                item.GetComponent<SpriteRenderer>().sprite = sprite;
            }

            // 3. 태그 설정 (Item 태그여야 PlayerInteraction이 인식함)
            // PlayerInteraction에서 "Item" 태그를 줍게 되어 있다면 "Item"으로, 
            // "Respawn"으로직을 따로 짰다면 "Respawn" 유지.
            // 보통 줍기 로직은 "Item" 태그를 씁니다. 확인 필요!
            item.tag = "Item";

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