using System.Collections;
using UnityEngine;

public class BossAI : MonoBehaviour
{
    [Header("Bullet Prefabs (속성별 총알)")]
    public GameObject redBulletPrefab;   // 불 속성 총알 (화염벽 이펙트)
    public GameObject greenBulletPrefab; // 풀 속성 총알
    public GameObject blueBulletPrefab;  // 물 속성 총알

    [Header("Pattern Settings")]
    public float patternInterval = 4.0f; // 패턴 사이 휴식 시간

    private bool isBattleStarted = false; // 전투 시작 여부 체크

    public void StartBattle()
    {
        // ★ 이미 시작했으면 또 하지 마! (중복 방지)
        if (isBattleStarted) return;

        isBattleStarted = true; // 이제 시작했다!
        StartCoroutine(PatternRoutine());
    }

    IEnumerator PatternRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(2.5f);

            Debug.Log("[BOSS] 화염벽(FireWall) 시전!");
            yield return StartCoroutine(FireWallPattern(ElementType.Fire));

            Debug.Log("[BOSS] 지침... 휴식");
            yield return new WaitForSeconds(patternInterval);
        }
    }

    // 속성을 인자로 받아서 그에 맞는 색깔 총알을 쏨
    IEnumerator FireWallPattern(ElementType element)
    {
        for (int i = 0; i < 2; i++)
        {
            SpawnFireWall(element);
            yield return new WaitForSeconds(0.7f);
        }
    }

    void SpawnFireWall(ElementType element)
    {
        // 1. 속성에 맞는 프리팹 고르기
        GameObject prefabToUse = null;

        switch (element)
        {
            case ElementType.Fire:
                prefabToUse = redBulletPrefab;
                break;
            case ElementType.Water:
                prefabToUse = blueBulletPrefab;
                break;
            case ElementType.Electric: // 혹은 Nature
                prefabToUse = greenBulletPrefab; // 기획에 따라 변경
                break;
        }

        if (prefabToUse == null) return;

        if (prefabToUse == null) return;

        // 2. 발사 로직
        Vector2 baseDir = Vector2.down;
        float[] angles = { -90, -45, 0, 45, 90 };

        foreach (float angle in angles)
        {
            // 각도 계산
            Vector2 dir = Quaternion.Euler(0, 0, angle) * baseDir;

            // ★ [핵심 수정] 생성 위치를 보스 몸에서 좀 떨어뜨리기! (offset)
            // 보스 위치 + (방향 * 2.0f) -> 보스보다 2미터 앞에서 생성됨
            Vector3 spawnPos = transform.position + (Vector3)(dir * 2.0f);

            // 생성 (spawnPos 사용)
            GameObject ball = Instantiate(prefabToUse, spawnPos, Quaternion.identity);

            // 크기 설정
            ball.transform.localScale = transform.localScale *2.0f;

            // 방향 설정 (총알 스크립트에 Setup이 있다면)
            ball.GetComponent<FireBall>().Setup(dir, element);
        }
    }
}