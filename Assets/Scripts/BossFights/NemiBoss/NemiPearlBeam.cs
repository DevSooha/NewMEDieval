using System.Collections;
using UnityEngine;

public class NemiPearlBeam : MonoBehaviour
{
    private Coroutine patternRoutine;
    private bool hasExecuted = false;

    // Phase2 진입 시 "딱 1번만" 호출
    public void StartPatternOnce()
    {
        if (hasExecuted) return;

        hasExecuted = true;
        patternRoutine = StartCoroutine(PatternRoutine());
    }

    public void StopPattern()
    {
        if (patternRoutine != null)
            StopCoroutine(patternRoutine);

        patternRoutine = null;
    }

    // 🔹 Phase2 필살기 자리
    private IEnumerator PatternRoutine()
    {
        yield return StartCoroutine(TeleportToOrigin());
        yield return StartCoroutine(ExecutePearlBeam());

        // 끝 (1회용)
    }

    // 🔹 원점 텔포 자리
    private IEnumerator TeleportToOrigin()
    {
        // TODO: groundTilemap cell (0,0)으로 이동
        yield return null;
    }

    // 🔹 PearlBeam 실행 자리
    private IEnumerator ExecutePearlBeam()
    {
        // TODO: PearlBeamController.PlayOnce(playerTF) 호출
        yield return null;
    }
}