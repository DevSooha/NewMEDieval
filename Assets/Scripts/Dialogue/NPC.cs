using UnityEngine;

public class NPC : MonoBehaviour
{
    [Header("NPC Settings")]
    public DialogueData dialogueData;
    
    [Header("Sprite Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite frontSprite;
    [SerializeField] private Sprite backSprite;
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite rightSprite;

    private Vector2 currentFacingDirection = Vector2.down;

    void Start()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        FaceDirection(Vector2.down);
    }

    public void FaceDirection(Vector2 direction)
    {
        currentFacingDirection = direction;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            if (direction.x > 0 && rightSprite != null)
            {
                spriteRenderer.sprite = rightSprite;
            }
            else if (direction.x < 0 && leftSprite != null)
            {
                spriteRenderer.sprite = leftSprite;
            }
        }
        else
        {
            if (direction.y > 0 && backSprite != null)
            {
                spriteRenderer.sprite = backSprite;
            }
            else if (direction.y < 0 && frontSprite != null)
            {
                spriteRenderer.sprite = frontSprite;
            }
        }
    }

    public Vector2 GetFacingDirection()
    {
        return currentFacingDirection;
    }
}