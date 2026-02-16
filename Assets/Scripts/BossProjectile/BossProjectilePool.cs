using System.Collections.Generic;
using UnityEngine;

public class BossProjectilePool
{
    private readonly Queue<BossProjectile> pool = new Queue<BossProjectile>();
    private readonly GameObject prefab;
    private readonly Transform root;

    public BossProjectilePool(GameObject prefab, int initialSize, Transform root)
    {
        this.prefab = prefab;
        this.root = root;

        if (this.prefab == null || this.root == null) return;

        for (int i = 0; i < initialSize; i++)
        {
            BossProjectile projectile = CreateNew();
            if (projectile != null)
            {
                pool.Enqueue(projectile);
            }
        }
    }

    public BossProjectile Rent()
    {
        while (pool.Count > 0)
        {
            BossProjectile projectile = pool.Dequeue();
            if (projectile == null) continue;

            projectile.gameObject.SetActive(true);
            return projectile;
        }

        BossProjectile created = CreateNew();
        if (created != null)
        {
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

        projectile.transform.SetParent(root);
        projectile.gameObject.SetActive(false);
        pool.Enqueue(projectile);
    }
}
