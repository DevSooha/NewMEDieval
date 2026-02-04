using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite fullHeartSprite;   // 기본 채워진 하트
    public Sprite bonusHeartSprite;  // 추가 체력용 하트 (기존 empty 자리에 있던 것)

    [Header("UI References")]
    public Image[] heartIcons;

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += UpdateHealthBar;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= UpdateHealthBar;
    }

    private void UpdateHealthBar(int hp, int maxHp)
    {
        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (i < hp)
            {
                // 현재 체력 범위 안일 때
                // 1~3번째 하트는 기본 스프라이트, 4~5번째는 보너스 스프라이트 적용
                heartIcons[i].sprite = (i < 3) ? fullHeartSprite : bonusHeartSprite;
                heartIcons[i].gameObject.SetActive(true);
            }
            else
            {
                heartIcons[i].gameObject.SetActive(false);
            }
        }
    }
}