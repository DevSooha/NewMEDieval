using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class EndingEvent : MonoBehaviour
{
    [Header("����")]
    public string titleSceneName = "TitleScene";

    [TextArea] // �ν����Ϳ��� �ٹٲ� �����ϰ�
    public string endingMessage = "THE END\n�÷��� ���ּż� �����մϴ�.";

    // �� ���� ĵ������ �ؽ�Ʈ ������Ʈ ������ �ʿ� ���� (UIManager�� �� ��)
    public bool isAuthorized = false;
    private bool isPlaying = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && isAuthorized)
        {
            PlayEnding();
        }
    }

    public void PlayEnding()
    {
        if (isPlaying) return;
        isPlaying = true;
        StartCoroutine(EndingRoutine());
    }

    IEnumerator EndingRoutine()
    {
        // 1. UIManager�� ���� ���� �ؽ�Ʈ ��� (�� �����)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowEnding(endingMessage);
        }

        // 2. �÷��̾� �����
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

        Debug.Log("���� ���� ����...");

        // 3. 5�� ���� �ؽ�Ʈ ����
        yield return new WaitForSeconds(5.0f);

        // 4. Ÿ��Ʋ�� �̵�
        if (!string.IsNullOrEmpty(titleSceneName))
            SceneManager.LoadScene(titleSceneName);
        else
            Application.Quit();
    }
}