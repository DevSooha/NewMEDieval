using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHP = 5;
    public int HP;

    [Header("Heart UI")]
    public Sprite fullHeartSprite;
    public Sprite emptyHeartSprite;
    public Image[] heartIcons;   

    [SerializeField] private GameObject player;

    void Start()
    {
        HP = maxHP;
        UpdateHealthBar();
         if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.X)) TakeDamage(1);
    }

    public void TakeDamage(int amount)
    {
        if (player == null) return;

        HP -= amount;
        if (HP > maxHP) HP = maxHP;
        if (HP < 0) HP = 0;

        UpdateHealthBar();

        if (HP == 0)
        {
            Destroy(player);
            Destroy(gameObject);
        }
    }

    public void Heal(int amount)
    {
        if (player == null) return;

        HP += amount;
        if (HP > maxHP) HP = maxHP;

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (heartIcons == null || heartIcons.Length == 0) return;

        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (heartIcons[i] == null) continue;

            if (i < HP)
            {
                heartIcons[i].sprite = fullHeartSprite;
                heartIcons[i].gameObject.SetActive(true);
            }
            else
            {
                heartIcons[i].sprite = emptyHeartSprite;
                heartIcons[i].gameObject.SetActive(true);
            }
        }
    }
}
