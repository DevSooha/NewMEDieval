using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FinalBossDiamondFlickerOverlay : MonoBehaviour
{
    [SerializeField] private float darkenDuration = 0.8f;
    [SerializeField] private float lightenDuration = 0.8f;
    [SerializeField] private float minAlpha = 0.05f;
    [SerializeField] private float maxAlpha = 0.3f;

    private Image overlayImage;
    private Coroutine loopRoutine;

    private void Awake()
    {
        EnsureOverlayImage();
        SetAlpha(0f);
    }

    public void BeginLoop()
    {
        if (!this) return;

        EnsureOverlayImage();
        if (loopRoutine != null) return;
        loopRoutine = StartCoroutine(FlickerRoutine());
    }

    public void StopLoop()
    {
        if (!this) return;

        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        SetAlpha(0f);
    }

    private void OnDisable()
    {
        if (loopRoutine != null)
        {
            StopCoroutine(loopRoutine);
            loopRoutine = null;
        }

        SetAlpha(0f);
    }

    private IEnumerator FlickerRoutine()
    {
        while (true)
        {
            yield return FadeTo(maxAlpha, Mathf.Max(0.01f, darkenDuration));
            yield return FadeTo(minAlpha, Mathf.Max(0.01f, lightenDuration));
        }
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float elapsed = 0f;
        float startAlpha = overlayImage != null ? overlayImage.color.a : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void EnsureOverlayImage()
    {
        if (overlayImage != null) return;

        Canvas existingCanvas = FindFirstObjectByType<Canvas>();
        Canvas targetCanvas = existingCanvas;
        if (targetCanvas == null)
        {
            GameObject canvasGO = new GameObject("DiamondFlickerCanvas");
            targetCanvas = canvasGO.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject overlayGO = new GameObject("FinalBossDiamondFlickerOverlay");
        overlayGO.transform.SetParent(targetCanvas.transform, false);
        RectTransform rect = overlayGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayImage = overlayGO.AddComponent<Image>();
        overlayImage.color = Color.black;
        overlayImage.raycastTarget = false;
    }

    private void SetAlpha(float alpha)
    {
        if (overlayImage == null) return;
        Color color = overlayImage.color;
        color.a = Mathf.Clamp01(alpha);
        overlayImage.color = color;
    }
}
