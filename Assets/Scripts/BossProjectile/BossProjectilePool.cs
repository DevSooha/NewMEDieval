using System.Collections.Generic;
using UnityEngine;

public class BossProjectilePool
{
    private readonly Queue<BossProjectile> pool = new Queue<BossProjectile>();
    private readonly GameObject prefab;
    private readonly Transform root;
    private bool prefabLacksProjectile;

    public BossProjectilePool(GameObject prefab, int initialSize, Transform root)
    {
        this.prefab = prefab;
        this.root = root;

        if (this.prefab == null || this.root == null) return;

        for (int i = 0; i < initialSize; i++)
        {
            BossProjectile projectile = CreateNew();
            if (projectile == null)
            {
                // 프리팹 구성 결함(BossProjectile 미부착)은 나머지 시도도 전부 동일하게
                // 실패하므로 1회 경고 후 중단 — 프리필 크기만큼 경고가 반복되는 스팸 방지 (QS-81 재발 대비).
                Debug.LogError($"[BossProjectilePool] '{this.prefab.name}' 프리팹에 BossProjectile이 없어 풀을 구성할 수 없습니다.");
                prefabLacksProjectile = true;
                return;
            }

            pool.Enqueue(projectile);
        }
    }

    public BossProjectile Rent()
    {
        // 프리팹 결함이 확인된 풀은 매 발사마다 Instantiate/Destroy를 반복하지 않는다.
        if (prefabLacksProjectile) return null;

        while (pool.Count > 0)
        {
            BossProjectile projectile = pool.Dequeue();
            if (projectile == null) continue;

            if (root != null && projectile.transform.parent != root)
            {
                projectile.transform.SetParent(root, false);
            }

            projectile.gameObject.SetActive(true);
            return projectile;
        }

        BossProjectile created = CreateNew();
        if (created != null)
        {
            if (root != null && created.transform.parent != root)
            {
                created.transform.SetParent(root, false);
            }

            created.gameObject.SetActive(true);
        }

        return created;
    }

    private BossProjectile CreateNew()
    {
        GameObject obj = Object.Instantiate(prefab, root);
        BossProjectile projectile = obj.GetComponent<BossProjectile>();
        if (projectile == null)
        {
            Object.Destroy(obj);
            return null;
        }

        projectile.SetPoolCallback(ReturnToPool);
        obj.SetActive(false);
        return projectile;
    }

    private void ReturnToPool(BossProjectile projectile)
    {
        if (projectile == null) return;

        projectile.gameObject.SetActive(false);
        pool.Enqueue(projectile);
    }
}
