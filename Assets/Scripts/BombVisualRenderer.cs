using UnityEngine;

[DisallowMultipleComponent]
public class BombVisualRenderer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer baseRenderer;
    [SerializeField] private SpriteRenderer bottomRenderer;
    [SerializeField] private SpriteRenderer topRenderer;
    [SerializeField] private SpriteRenderer frameRenderer;
    [Header("Sorting")]
    [SerializeField] private bool forceSorting = true;
    [SerializeField] private string sortingLayerName = "EnemyBullet";
    [SerializeField] private int sortingOrder = 40;

    private void Awake()
    {
        EnsureLayers();
    }

    public void Apply(PotionData potionData)
    {
        Apply(PotionVisualResolver.Resolve(potionData));
    }

    public void Apply(PotionVisualParts parts)
    {
        EnsureLayers();

        if (!parts.HasAny)
        {
            SetLayerEnabled(false);
            if (baseRenderer != null)
            {
                baseRenderer.enabled = true;
            }
            return;
        }

        if (baseRenderer != null)
        {
            baseRenderer.enabled = false;
        }

        SetRenderer(bottomRenderer, parts.Bottom);
        SetRenderer(topRenderer, parts.Top);
        SetRenderer(frameRenderer, parts.Frame);
    }

    private void EnsureLayers()
    {
        if (baseRenderer == null)
        {
            baseRenderer = GetComponent<SpriteRenderer>();
        }

        int sortingLayerId = SortingLayer.NameToID(sortingLayerName);
        bool hasValidSortingLayer = sortingLayerId != 0 || sortingLayerName == "Default";

        string resolvedSortingLayer = baseRenderer != null ? baseRenderer.sortingLayerName : "Default";
        int resolvedBaseOrder = baseRenderer != null ? baseRenderer.sortingOrder : 0;

        if (forceSorting)
        {
            if (hasValidSortingLayer)
            {
                resolvedSortingLayer = sortingLayerName;
            }

            resolvedBaseOrder = sortingOrder;
            ApplySorting(baseRenderer, hasValidSortingLayer, sortingLayerId, resolvedSortingLayer, resolvedBaseOrder);
        }

        bottomRenderer = EnsureLayerRenderer(bottomRenderer, "BottomLayer", resolvedBaseOrder, resolvedSortingLayer);
        topRenderer = EnsureLayerRenderer(topRenderer, "TopLayer", resolvedBaseOrder + 1, resolvedSortingLayer);
        frameRenderer = EnsureLayerRenderer(frameRenderer, "FrameLayer", resolvedBaseOrder + 2, resolvedSortingLayer);
    }

    private SpriteRenderer EnsureLayerRenderer(SpriteRenderer renderer, string childName, int sortingOrder, string sortingLayer)
    {
        if (renderer == null)
        {
            Transform existing = transform.Find(childName);

            if (existing == null)
            {
                GameObject layerObject = new GameObject(childName);
                layerObject.transform.SetParent(transform, false);
                existing = layerObject.transform;
            }

            renderer = existing.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = existing.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = false;
        return renderer;
    }

    private void SetLayerEnabled(bool enabled)
    {
        if (bottomRenderer != null) bottomRenderer.enabled = enabled;
        if (topRenderer != null) topRenderer.enabled = enabled;
        if (frameRenderer != null) frameRenderer.enabled = enabled;
    }

    private static void SetRenderer(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.enabled = sprite != null;
    }

    private static void ApplySorting(
        SpriteRenderer renderer,
        bool hasValidSortingLayer,
        int sortingLayerId,
        string sortingLayerName,
        int sortingOrder)
    {
        if (renderer == null)
        {
            return;
        }

        if (hasValidSortingLayer)
        {
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingLayerName = sortingLayerName;
        }

        renderer.sortingOrder = sortingOrder;
    }
}
