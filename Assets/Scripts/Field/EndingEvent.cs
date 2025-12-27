using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class EndingEvent : MonoBehaviour
{
    [Header("설정")]
    public string titleSceneName = "TitleScene";

    [TextArea] // 인스펙터에서 줄바꿈 가능하게
    public string endingMessage = "THE END\n플레이 해주셔서 감사합니다.";

    // ★ 이제 캔버스나 텍스트 오브젝트 연결할 필요 없음 (UIManager가 다 함)

    // 안전장치
    public bool isAuthorized = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && isAuthorized)
        {
            PlayEnding();
        }
    }

    public void PlayEnding()
    {
        StartCoroutine(EndingRoutine());
    }

    IEnumerator EndingRoutine()
    {
        // 1. UIManager를 통해 엔딩 텍스트 출력 (안 사라짐)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowEnding(endingMessage);
        }

        // 2. 플레이어 숨기기
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            SpriteRenderer sr = player.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.enabled = false;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.simulated = false;
                rb.linearVelocity = Vector2.zero;
            }
        }

        Debug.Log("엔딩 연출 시작...");

        // 3. 5초 동안 텍스트 감상
        yield return new WaitForSeconds(5.0f);

        // 4. 타이틀로 이동
        if (!string.IsNullOrEmpty(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
        else
            Application.Quit();
    }
}