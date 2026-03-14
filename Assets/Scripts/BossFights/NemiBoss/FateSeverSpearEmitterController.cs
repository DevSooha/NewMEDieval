using UnityEngine;

public class FateSeverSpearEmitterController : MonoBehaviour
{
    [SerializeField] private Transform[] fixedYEmitters;
    [SerializeField] private Transform[] fixedXEmitters;

    // Y emitter index 계산 (플레이어 X 기준)
    public int GetFixedYIndexFromPlayerCellX(int playerX)
    {
        int closest = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < fixedYEmitters.Length; i++)
        {
            float d = Mathf.Abs(playerX - Mathf.RoundToInt(fixedYEmitters[i].position.x));
            if (d < bestDist)
            {
                bestDist = d;
                closest = i;
            }
        }

        return closest;
    }

    public int GetFixedXIndexFromPlayerCellY(int playerY)
    {
        int closest = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < fixedXEmitters.Length; i++)
        {
            float d = Mathf.Abs(playerY - Mathf.RoundToInt(fixedXEmitters[i].position.y));
            if (d < bestDist)
            {
                bestDist = d;
                closest = i;
            }
        }

        return closest;
    }

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
}