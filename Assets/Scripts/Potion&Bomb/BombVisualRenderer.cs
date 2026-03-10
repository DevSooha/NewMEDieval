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
    [Header("Fallback")]
    [SerializeField] private bool useBaseRendererAsFallback = true;
    [SerializeField] private bool logFallbackWarnings = true;
    [Header("Visual Scale")]
    [SerializeField] private bool applyVisualScale = true;
    [SerializeField] private float visualScaleMultiplier = 0.25f;

    private Vector3 initialLocalScale;
    private bool initialLocalScaleCaptured;

    private void Awake()
    {
        CaptureInitialScaleIfNeeded();
        EnsureLayers();
        ApplyVisualScaleIfNeeded();
    }

    public void Apply(PotionData potionData)
    {
        Apply(PotionVisualResolver.Resolve(potionData));
    }

    public void Apply(PotionVisualParts parts)
    {
        EnsureLayers();
        ApplyVisualScaleIfNeeded();
        DisableAllVisualLayers();

        if (!parts.HasAny)
        {
            ApplyFallbackVisual();
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

    private void CaptureInitialScaleIfNeeded()
    {
        if (initialLocalScaleCaptured)
        {
            return;
        }

        initialLocalScale = transform.localScale;
        initialLocalScaleCaptured = true;
    }

    private void ApplyVisualScaleIfNeeded()
    {
        if (!applyVisualScale)
        {
            return;
        }

        CaptureInitialScaleIfNeeded();
        float safeMultiplier = Mathf.Max(0.01f, visualScaleMultiplier);
        transform.localScale = initialLocalScale * safeMultiplier;
    }

    private void EnsureLayers()
    {
        if (baseRenderer == null)
        {
            baseRenderer = GetComponent<SpriteRenderer>();
        }

        string resolvedSortingLayer = ResolveSortingLayerName(sortingLayerName, "Default");
        int sortingLayerId = SortingLayer.NameToID(resolvedSortingLayer);
        bool hasValidSortingLayer = sortingLayerId != 0 || resolvedSortingLayer == "Default";

        string inheritedSortingLayer = baseRenderer != null ? baseRenderer.sortingLayerName : "Default";
        int resolvedBaseOrder = baseRenderer != null ? baseRenderer.sortingOrder : 0;

        if (forceSorting)
        {
            if (hasValidSortingLayer)
            {
                inheritedSortingLayer = resolvedSortingLayer;
            }

            resolvedBaseOrder = sortingOrder;
            ApplySorting(baseRenderer, hasValidSortingLayer, sortingLayerId, inheritedSortingLayer, resolvedBaseOrder);
        }

        if (baseRenderer != null)
        {
            baseRenderer.enabled = false;
        }

        bottomRenderer = EnsureLayerRenderer(bottomRenderer, "BottomImageRenderer", "BottomLayer", resolvedBaseOrder, inheritedSortingLayer);
        topRenderer = EnsureLayerRenderer(topRenderer, "TopImageRenderer", "TopLayer", resolvedBaseOrder + 1, inheritedSortingLayer);
        frameRenderer = EnsureLayerRenderer(frameRenderer, "FrameRenderer", "FrameLayer", resolvedBaseOrder + 2, inheritedSortingLayer);
    }

    private SpriteRenderer EnsureLayerRenderer(
        SpriteRenderer renderer,
        string preferredChildName,
        string legacyChildName,
        int sortingOrder,
        string sortingLayer)
    {
        if (renderer == null)
        {
            Transform existing = transform.Find(preferredChildName);
            if (existing == null && !string.IsNullOrEmpty(legacyChildName))
            {
                existing = transform.Find(legacyChildName);
            }

            if (existing == null)
            {
                GameObject layerObject = new GameObject(preferredChildName);
                layerObject.transform.SetParent(transform, false);
                existing = layerObject.transform;
            }
            else
            {
                existing.name = preferredChildName;
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

    private void DisableAllVisualLayers()
    {
        if (baseRenderer != null)
        {
            baseRenderer.enabled = false;
        }

        SetLayerEnabled(false);
    }

    private void ApplyFallbackVisual()
    {
        if (!useBaseRendererAsFallback)
        {
            if (logFallbackWarnings)
            {
                Debug.LogWarning("[BombVisual] No potion visual parts resolved. Bomb visual layers are hidden.", this);
            }

            return;
        }

        if (baseRenderer != null && baseRenderer.sprite != null)
        {
            baseRenderer.color = Color.white;
            baseRenderer.enabled = true;
            return;
        }

        if (logFallbackWarnings)
        {
            Debug.LogWarning("[BombVisual] Fallback requested but baseRenderer sprite is missing.", this);
        }
    }

    private string ResolveSortingLayerName(string requestedName, string fallbackName)
    {
        string candidate = string.IsNullOrWhiteSpace(requestedName) ? fallbackName : requestedName;
        int candidateId = SortingLayer.NameToID(candidate);
        if (candidateId != 0 || candidate == "Default")
        {
            return candidate;
        }

        int fallbackId = SortingLayer.NameToID(fallbackName);
        if (fallbackId != 0 || fallbackName == "Default")
        {
            if (logFallbackWarnings)
            {
                Debug.LogWarning($"[BombVisual] Sorting layer '{candidate}' not found. Falling back to '{fallbackName}'.", this);
            }

            return fallbackName;
        }

        if (logFallbackWarnings)
        {
            Debug.LogWarning($"[BombVisual] Sorting layer '{candidate}' not found. Falling back to 'Default'.", this);
        }

        return "Default";
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
