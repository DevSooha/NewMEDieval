using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SeasonVisualController : MonoBehaviour
{
    public static SeasonVisualController Instance;

    [System.Serializable]
    public class SeasonVisualSet
    {
        public SeasonType season = SeasonType.Unknown;
        public Sprite backgroundSprite;
        public Sprite leftFrameSprite;
        public Sprite rightFrameSprite;
        public Color backgroundColor = Color.white;
        public Color frameColor = Color.white;
    }

    [Header("Transition")]
    [SerializeField] private float defaultTransitionDuration = 0.45f;

    [Header("Background")]
    [SerializeField] private SpriteRenderer backgroundPrimary;
    [SerializeField] private SpriteRenderer backgroundSecondary;

    [Header("Frame")]
    [SerializeField] private Image leftFrame;
    [SerializeField] private Image rightFrame;

    [Header("Season Visuals")]
    [SerializeField] private List<SeasonVisualSet> visualSets = new List<SeasonVisualSet>();

    private readonly Dictionary<SeasonType, SeasonVisualSet> visualLookup = new Dictionary<SeasonType, SeasonVisualSet>();

    private RawImage leftFrameBaseRaw;
    private RawImage rightFrameBaseRaw;
    private RawImage leftFrameOverlayRaw;
    private RawImage rightFrameOverlayRaw;
    [SerializeField] private float targetFrameTileHeight = 0f;
    private Coroutine transitionRoutine;
    private SeasonType currentSeason = SeasonType.Unknown;
    private bool isInitialized;
    private bool primaryBackgroundIsActive = true;

    private void Reset()
    {
        EnsureSeasonSlots();
        AutoAssignProjectDefaults();
        RebuildLookup();
    }

    private void LateUpdate()
    {
        SyncBackgroundToCamera();
        RefreshFrameLayouts();
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        RebuildLookup();
        EnsureBackgroundRenderers();
        ResolveFrameReferences();
        EnsureFrameLayers();
        ResetVisualState();
    }

    private void OnValidate()
    {
        EnsureSeasonSlots();
        RebuildLookup();
    }

    public void ApplySeason(RoomData roomData, bool immediate = false, float durationOverride = -1f)
    {
        SeasonType nextSeason = RoomSeasonResolver.Resolve(roomData);
        ApplySeason(nextSeason, immediate, durationOverride);
    }

    public void ApplySeason(SeasonType season, bool immediate = false, float durationOverride = -1f)
    {
        ResolveFrameReferences();
        EnsureFrameLayers();
        RefreshFrameLayouts();

        if (!TryGetVisualSet(season, out SeasonVisualSet targetVisual))
        {
            return;
        }

        float duration = durationOverride >= 0f ? durationOverride : defaultTransitionDuration;
        bool shouldApplyImmediately = immediate || !isInitialized;

        if (!shouldApplyImmediately && season == currentSeason)
        {
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (shouldApplyImmediately || duration <= 0f)
        {
            ApplyImmediate(targetVisual);
            currentSeason = season;
            isInitialized = true;
            return;
        }

        transitionRoutine = StartCoroutine(TransitionTo(targetVisual, season, duration));
    }

    private void RebuildLookup()
    {
        visualLookup.Clear();

        for (int i = 0; i < visualSets.Count; i++)
        {
            SeasonVisualSet set = visualSets[i];
            if (set == null)
            {
                continue;
            }

            visualLookup[set.season] = set;
        }
    }

    [ContextMenu("Auto Assign Project Defaults")]
    private void AutoAssignProjectDefaults()
    {
#if UNITY_EDITOR
        EnsureSeasonSlots();

        AssignDefaults(
            SeasonType.Spring,
            "Assets/봄/spr_하늘.배경.png",
            "Assets/Sprites/UI/와이어프레임/spring_2.jpg",
            "Assets/Sprites/UI/와이어프레임/spring_2.jpg");

        AssignDefaults(
            SeasonType.Summer,
            "Assets/Sprites/Map/Skybox/sky_summer.jpg",
            "Assets/Sprites/UI/와이어프레임/summer_2.jpg",
            "Assets/Sprites/UI/와이어프레임/summer_2.jpg");

        AssignDefaults(
            SeasonType.Autumn,
            "Assets/Sprites/Map/Skybox/sky_autumn.png",
            "Assets/Sprites/UI/와이어프레임/autumn_1.jpg",
            "Assets/Sprites/UI/와이어프레임/autumn_1.jpg");

        AssignDefaults(
            SeasonType.Winter,
            "Assets/Sprites/Map/Skybox/sky_winter.jpg",
            "Assets/Sprites/UI/와이어프레임/winter_1.jpg",
            "Assets/Sprites/UI/와이어프레임/winter_1.jpg");

        EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Clear Season Slots")]
    private void ClearSeasonSlots()
    {
        visualSets.Clear();
        EnsureSeasonSlots();
        RebuildLookup();
    }

    private bool TryGetVisualSet(SeasonType season, out SeasonVisualSet visualSet)
    {
        if (visualLookup.TryGetValue(season, out visualSet))
        {
            return true;
        }

        return visualLookup.TryGetValue(SeasonType.Unknown, out visualSet);
    }

    private void EnsureSeasonSlots()
    {
        EnsureSeasonSlot(SeasonType.Spring);
        EnsureSeasonSlot(SeasonType.Summer);
        EnsureSeasonSlot(SeasonType.Autumn);
        EnsureSeasonSlot(SeasonType.Winter);
        EnsureSeasonSlot(SeasonType.Unknown);
    }

    private void EnsureSeasonSlot(SeasonType season)
    {
        for (int i = 0; i < visualSets.Count; i++)
        {
            if (visualSets[i] != null && visualSets[i].season == season)
            {
                return;
            }
        }

        visualSets.Add(new SeasonVisualSet { season = season });
    }

#if UNITY_EDITOR
    private void AssignDefaults(SeasonType season, string backgroundPath, string leftFramePath, string rightFramePath)
    {
        SeasonVisualSet set = GetOrCreateVisualSet(season);
        if (set == null)
        {
            return;
        }

        if (set.backgroundSprite == null)
        {
            set.backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(backgroundPath);
        }

        if (set.leftFrameSprite == null)
        {
            set.leftFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(leftFramePath);
        }

        if (set.rightFrameSprite == null)
        {
            set.rightFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(rightFramePath);
        }
    }
#endif

    private SeasonVisualSet GetOrCreateVisualSet(SeasonType season)
    {
        for (int i = 0; i < visualSets.Count; i++)
        {
            SeasonVisualSet set = visualSets[i];
            if (set != null && set.season == season)
            {
                return set;
            }
        }

        SeasonVisualSet created = new SeasonVisualSet { season = season };
        visualSets.Add(created);
        return created;
    }

    private void ResolveFrameReferences()
    {
        if (leftFrame == null)
        {
            leftFrame = FindImageByName("LeftFrame");
        }

        if (rightFrame == null)
        {
            rightFrame = FindImageByName("RightFrame");
        }
    }

    private Image FindImageByName(string targetName)
    {
        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            return null;
        }

        Transform root = uiManager.transform;
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child.name != targetName)
            {
                continue;
            }

            return child.GetComponent<Image>();
        }

        return null;
    }

    private void EnsureBackgroundRenderers()
    {
        if (backgroundPrimary == null)
        {
            backgroundPrimary = CreateBackgroundRenderer("SeasonBackgroundPrimary", 0);
        }

        if (backgroundSecondary == null)
        {
            backgroundSecondary = CreateBackgroundRenderer("SeasonBackgroundSecondary", -1);
        }
    }

    private void SyncBackgroundToCamera()
    {
        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        SyncSingleBackground(backgroundPrimary, targetCamera);
        SyncSingleBackground(backgroundSecondary, targetCamera);
    }

    private void SyncSingleBackground(SpriteRenderer spriteRenderer, Camera targetCamera)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Transform t = spriteRenderer.transform;
        t.position = new Vector3(targetCamera.transform.position.x, targetCamera.transform.position.y, 10f);
    }

    private SpriteRenderer CreateBackgroundRenderer(string objectName, int sortingOrder)
    {
        Camera targetCamera = Camera.main;
        Transform parent = targetCamera != null ? targetCamera.transform : transform;

        Transform existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out SpriteRenderer existingRenderer))
        {
            return existingRenderer;
        }

        GameObject backgroundObject = new GameObject(objectName, typeof(SpriteRenderer));
        backgroundObject.transform.SetParent(parent, false);
        backgroundObject.transform.localPosition = new Vector3(0f, 0f, 20f);

        SpriteRenderer spriteRenderer = backgroundObject.GetComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerID = 0;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        UpdateBackgroundLayout(spriteRenderer);
        return spriteRenderer;
    }

    private void EnsureFrameLayers()
    {
        if (leftFrame != null && leftFrameBaseRaw == null)
        {
            leftFrameBaseRaw = CreateRawFrame(leftFrame, "LeftFrameRaw", false);
        }

        if (leftFrame != null && leftFrameOverlayRaw == null)
        {
            leftFrameOverlayRaw = CreateRawFrame(leftFrame, "LeftFrameOverlayRaw", true);
        }

        if (rightFrame != null && rightFrameBaseRaw == null)
        {
            rightFrameBaseRaw = CreateRawFrame(rightFrame, "RightFrameRaw", false);
        }

        if (rightFrame != null && rightFrameOverlayRaw == null)
        {
            rightFrameOverlayRaw = CreateRawFrame(rightFrame, "RightFrameOverlayRaw", true);
        }

        if (leftFrame != null) leftFrame.enabled = false;
        if (rightFrame != null) rightFrame.enabled = false;
    }

    private RawImage CreateRawFrame(Image source, string objectName, bool placeAbove)
    {
        Transform existing = source.transform.parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out RawImage existingRaw))
        {
            return existingRaw;
        }

        GameObject rawObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
        rawObject.transform.SetParent(source.transform.parent, false);
        rawObject.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + (placeAbove ? 1 : 0));

        RectTransform sourceRect = source.rectTransform;
        RectTransform rawRect = rawObject.GetComponent<RectTransform>();
        rawRect.anchorMin = sourceRect.anchorMin;
        rawRect.anchorMax = sourceRect.anchorMax;
        rawRect.anchoredPosition = sourceRect.anchoredPosition;
        rawRect.sizeDelta = sourceRect.sizeDelta;
        rawRect.pivot = sourceRect.pivot;
        rawRect.localScale = sourceRect.localScale;
        rawRect.localRotation = sourceRect.localRotation;

        LayoutElement sourceLayout = source.GetComponent<LayoutElement>();
        LayoutElement rawLayout = rawObject.GetComponent<LayoutElement>();
        if (sourceLayout != null)
        {
            rawLayout.ignoreLayout = sourceLayout.ignoreLayout;
            rawLayout.minWidth = sourceLayout.minWidth;
            rawLayout.minHeight = sourceLayout.minHeight;
            rawLayout.preferredWidth = sourceLayout.preferredWidth;
            rawLayout.preferredHeight = sourceLayout.preferredHeight;
            rawLayout.flexibleWidth = sourceLayout.flexibleWidth;
            rawLayout.flexibleHeight = sourceLayout.flexibleHeight;
        }
        else
        {
            rawLayout.ignoreLayout = true;
        }

        RawImage rawImage = rawObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        rawImage.enabled = false;
        rawImage.color = new Color(1f, 1f, 1f, 0f);
        return rawImage;
    }

    private void ResetVisualState()
    {
        if (backgroundPrimary != null)
        {
            SetSpriteRendererAlpha(backgroundPrimary, 1f);
            backgroundPrimary.enabled = backgroundPrimary.sprite != null;
        }

        if (backgroundSecondary != null)
        {
            SetSpriteRendererAlpha(backgroundSecondary, 0f);
            backgroundSecondary.enabled = false;
        }

        DisableOverlay(leftFrameOverlayRaw);
        DisableOverlay(rightFrameOverlayRaw);
    }

    private void ApplyImmediate(SeasonVisualSet targetVisual)
    {
        ApplyBackgroundImmediate(targetVisual);
        ApplyFrameImmediate(leftFrameBaseRaw, leftFrameOverlayRaw, targetVisual.leftFrameSprite, targetVisual.frameColor, leftFrame);
        ApplyFrameImmediate(rightFrameBaseRaw, rightFrameOverlayRaw, targetVisual.rightFrameSprite, targetVisual.frameColor, rightFrame);
    }

    private void ApplyBackgroundImmediate(SeasonVisualSet targetVisual)
    {
        SpriteRenderer active = GetActiveBackground();
        SpriteRenderer inactive = GetInactiveBackground();

        if (active != null)
        {
            active.sprite = targetVisual.backgroundSprite;
            active.color = targetVisual.backgroundColor;
            UpdateBackgroundLayout(active);
            SetSpriteRendererAlpha(active, 1f);
            active.enabled = targetVisual.backgroundSprite != null;
        }

        if (inactive != null)
        {
            inactive.sprite = null;
            inactive.enabled = false;
            SetSpriteRendererAlpha(inactive, 0f);
        }
    }

    private void ApplyFrameImmediate(RawImage baseImage, RawImage overlayImage, Sprite sprite, Color color, Image sourceImage)
    {
        if (baseImage != null)
        {
            ApplyRawFrame(baseImage, sprite, color, 1f, sourceImage);
            baseImage.enabled = sprite != null;
        }

        DisableOverlay(overlayImage);
    }

    private IEnumerator TransitionTo(SeasonVisualSet targetVisual, SeasonType nextSeason, float duration)
    {
        SpriteRenderer fromBackground = GetActiveBackground();
        SpriteRenderer toBackground = GetInactiveBackground();

        if (toBackground != null)
        {
            toBackground.sprite = targetVisual.backgroundSprite;
            toBackground.color = WithAlpha(targetVisual.backgroundColor, 0f);
            UpdateBackgroundLayout(toBackground);
            toBackground.enabled = targetVisual.backgroundSprite != null;
        }

        PrepareFrameOverlay(leftFrameOverlayRaw, targetVisual.leftFrameSprite, targetVisual.frameColor, leftFrame);
        PrepareFrameOverlay(rightFrameOverlayRaw, targetVisual.rightFrameSprite, targetVisual.frameColor, rightFrame);

        Color leftBaseColor = leftFrameBaseRaw != null ? leftFrameBaseRaw.color : Color.white;
        Color rightBaseColor = rightFrameBaseRaw != null ? rightFrameBaseRaw.color : Color.white;
        Color fromBackgroundColor = fromBackground != null ? fromBackground.color : Color.white;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            if (fromBackground != null)
            {
                fromBackground.color = WithAlpha(fromBackgroundColor, 1f - t);
            }

            if (toBackground != null && toBackground.enabled)
            {
                toBackground.color = WithAlpha(targetVisual.backgroundColor, t);
            }

            FadeFramePair(leftFrameBaseRaw, leftFrameOverlayRaw, leftBaseColor, targetVisual.frameColor, t);
            FadeFramePair(rightFrameBaseRaw, rightFrameOverlayRaw, rightBaseColor, targetVisual.frameColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        primaryBackgroundIsActive = !primaryBackgroundIsActive;
        ApplyImmediate(targetVisual);
        currentSeason = nextSeason;
        isInitialized = true;
        transitionRoutine = null;
    }

    private void PrepareFrameOverlay(RawImage overlayImage, Sprite sprite, Color color, Image sourceImage)
    {
        if (overlayImage == null)
        {
            return;
        }

        ApplyRawFrame(overlayImage, sprite, color, 0f, sourceImage);
        overlayImage.enabled = sprite != null;
    }

    private void FadeFramePair(RawImage baseImage, RawImage overlayImage, Color baseColor, Color targetColor, float t)
    {
        if (baseImage != null)
        {
            baseImage.color = WithAlpha(baseColor, 1f - t);
        }

        if (overlayImage != null && overlayImage.enabled)
        {
            overlayImage.color = WithAlpha(targetColor, t);
        }
    }

    private void ApplyRawFrame(RawImage rawImage, Sprite sprite, Color color, float alpha, Image sourceImage)
    {
        if (rawImage == null)
        {
            return;
        }

        Texture texture = sprite != null ? sprite.texture : null;
        rawImage.texture = texture;
        if (texture != null)
        {
            texture.wrapMode = TextureWrapMode.Repeat;
        }

        rawImage.uvRect = CalculateFrameUvRect(sourceImage, sprite);
        rawImage.color = WithAlpha(color, alpha);
        RefreshRawFrameLayout(rawImage, sourceImage);
    }

    private Rect CalculateFrameUvRect(Image sourceImage, Sprite sprite)
    {
        if (sourceImage == null || sprite == null || sprite.rect.height <= 0.01f)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        Canvas.ForceUpdateCanvases();

        RectTransform rect = sourceImage.rectTransform;
        rect.ForceUpdateRectTransforms();

        Canvas canvas = sourceImage.canvas;
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        float pixelHeight = Mathf.Max(1f, rect.rect.height * scaleFactor);

        float tileHeight = targetFrameTileHeight > 0f ? targetFrameTileHeight : sprite.rect.height;
        float repeatY = pixelHeight / tileHeight;
        return new Rect(0f, 0f, 1f, repeatY);
    }

    private void RefreshRawFrameLayout(RawImage rawImage, Image sourceImage)
    {
        if (rawImage == null || sourceImage == null)
        {
            return;
        }

        RectTransform sourceRect = sourceImage.rectTransform;
        RectTransform rawRect = rawImage.rectTransform;
        rawRect.anchorMin = sourceRect.anchorMin;
        rawRect.anchorMax = sourceRect.anchorMax;
        rawRect.anchoredPosition = sourceRect.anchoredPosition;
        rawRect.sizeDelta = sourceRect.sizeDelta;
        rawRect.pivot = sourceRect.pivot;
        rawRect.localScale = sourceRect.localScale;
        rawRect.localRotation = sourceRect.localRotation;
    }

    private void RefreshFrameLayouts()
    {
        RefreshRawFrameLayout(leftFrameBaseRaw, leftFrame);
        RefreshRawFrameLayout(leftFrameOverlayRaw, leftFrame);
        RefreshRawFrameLayout(rightFrameBaseRaw, rightFrame);
        RefreshRawFrameLayout(rightFrameOverlayRaw, rightFrame);

        if (leftFrameBaseRaw != null && leftFrameBaseRaw.enabled)
        {
            leftFrameBaseRaw.uvRect = CalculateFrameUvRect(leftFrame, GetFrameSprite(leftFrameBaseRaw.texture, true));
        }

        if (leftFrameOverlayRaw != null && leftFrameOverlayRaw.enabled)
        {
            leftFrameOverlayRaw.uvRect = CalculateFrameUvRect(leftFrame, GetFrameSprite(leftFrameOverlayRaw.texture, true));
        }

        if (rightFrameBaseRaw != null && rightFrameBaseRaw.enabled)
        {
            rightFrameBaseRaw.uvRect = CalculateFrameUvRect(rightFrame, GetFrameSprite(rightFrameBaseRaw.texture, false));
        }

        if (rightFrameOverlayRaw != null && rightFrameOverlayRaw.enabled)
        {
            rightFrameOverlayRaw.uvRect = CalculateFrameUvRect(rightFrame, GetFrameSprite(rightFrameOverlayRaw.texture, false));
        }
    }

    private Sprite GetFrameSprite(Texture texture, bool left)
    {
        if (texture == null)
        {
            return null;
        }

        for (int i = 0; i < visualSets.Count; i++)
        {
            SeasonVisualSet set = visualSets[i];
            if (set == null)
            {
                continue;
            }

            Sprite sprite = left ? set.leftFrameSprite : set.rightFrameSprite;
            if (sprite != null && sprite.texture == texture)
            {
                return sprite;
            }
        }

        return null;
    }

    private void DisableOverlay(RawImage overlayImage)
    {
        if (overlayImage == null)
        {
            return;
        }

        overlayImage.enabled = false;
        SetGraphicAlpha(overlayImage, 0f);
    }

    private SpriteRenderer GetActiveBackground()
    {
        return primaryBackgroundIsActive ? backgroundPrimary : backgroundSecondary;
    }

    private SpriteRenderer GetInactiveBackground()
    {
        return primaryBackgroundIsActive ? backgroundSecondary : backgroundPrimary;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void SetSpriteRendererAlpha(SpriteRenderer spriteRenderer, float alpha)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
        {
            return;
        }

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private void UpdateBackgroundLayout(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.transform.localScale = Vector3.one;
            return;
        }

        Bounds bounds = spriteRenderer.sprite.bounds;
        float spriteWidth = Mathf.Max(0.0001f, bounds.size.x);
        float spriteHeight = Mathf.Max(0.0001f, bounds.size.y);
        spriteRenderer.transform.localScale = new Vector3(width / spriteWidth, height / spriteHeight, 1f);
    }
}
