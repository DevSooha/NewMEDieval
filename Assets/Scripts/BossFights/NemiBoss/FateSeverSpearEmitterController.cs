using UnityEngine;

public class FateSeverSpearEmitterController : MonoBehaviour
{
    [SerializeField] private Transform[] fixedYEmitters;
    [SerializeField] private Transform[] fixedXEmitters;

    public Transform GetClosestYEmitter(float playerX)
    {
        Transform closest = fixedYEmitters[0];//0번 emitter을 사용한다.
        float bestDist = Mathf.Abs(playerX - closest.position.x);

        foreach (var e in fixedYEmitters)
        {
            float dist = Mathf.Abs(playerX - e.position.x);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = e;
            }
        }

        return closest;
    }

    public Transform GetClosestXEmitter(float playerY)
    {
        Transform closest = fixedXEmitters[0];
        float bestDist = Mathf.Abs(playerY - closest.position.y);

        foreach (var e in fixedXEmitters)
        {
            float dist = Mathf.Abs(playerY - e.position.y);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = e;
            }
        }

        return closest;
    }

    private void OnDrawGizmos()
    {
        // Y 에미터 (세로 발사 기준점) — 초록
        if (fixedYEmitters != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
            foreach (var e in fixedYEmitters)
            {
                if (e == null) continue;
                Gizmos.DrawSphere(e.position, 0.25f);
                Gizmos.DrawLine(e.position, e.position + Vector3.down * 1.5f);
            }
        }

        // X 에미터 (가로 발사 기준점) — 파랑
        if (fixedXEmitters != null)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.6f);
            foreach (var e in fixedXEmitters)
            {
                if (e == null) continue;
                Gizmos.DrawSphere(e.position, 0.25f);
                Gizmos.DrawLine(e.position, e.position + Vector3.left * 1.5f);
            }
        }
    }
}