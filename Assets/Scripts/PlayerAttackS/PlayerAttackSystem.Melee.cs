using System.Collections;
using UnityEngine;

public partial class PlayerAttackSystem
{
    void HandleMeleeInput()
    {
        if (IsAttackPressed())
        {
            StartCoroutine(MeleeAttackRoutine());
        }
    }

    IEnumerator MeleeAttackRoutine()
    {
        isAttack = true;

        Vector2 forward = aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.down;
        Vector2 attackPos = (Vector2)transform.position + (forward * (tileSize + meleeForwardOffset));
        Vector2 attackBoxSize = new Vector2(tileSize * 2f, tileSize * 2f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(attackPos, attackBoxSize, 0f, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            BossHealth boss = hit.GetComponent<BossHealth>();
            if (boss != null)
            {
                boss.TakeDamage(50, ElementType.None);
                continue;
            }

            if (!CombatTargetHitbox.TryGetEnemyCombat(hit, out EnemyCombat enemy))
            {
                continue;
            }

            enemy.EnemyTakeDamage(50);
        }

        yield return new WaitForSeconds(0.4f);
        isAttack = false;
    }

    Vector2 GetAimDirection()
    {
        return aimDirection.sqrMagnitude > 0.001f ? aimDirection.normalized : Vector2.down;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawMeleeGizmo) return;
        if (tileSize <= 0f) return;

        Vector2 dir = aimDirection;
        if (dir == Vector2.zero)
        {
            dir = Vector2.down;
        }

        Vector2 forward = dir.normalized;
        Vector3 attackPos = transform.position + (Vector3)(forward * (tileSize + meleeForwardOffset));
        Vector3 attackBoxSize = new Vector3(tileSize * 2f, tileSize * 2f, 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(attackPos, attackBoxSize);
    }
}
