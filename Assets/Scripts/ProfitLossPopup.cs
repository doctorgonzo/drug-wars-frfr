using TMPro;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class ProfitLossPopup : MonoBehaviour
{
    public static ProfitLossPopup Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TMP_Text popupText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupRect;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float holdDuration = 0.7f;
    [SerializeField] private float fadeOutDuration = 0.35f;

    [Header("Motion")]
    [SerializeField] private float floatUpDistance = 50f;
    [SerializeField] private float scaleOvershoot = 1.2f;
    [SerializeField] private float aboveMouseOffset = 30f;

    [Header("Colors")]
    [SerializeField] private Color profitColor = new Color(0.15f, 1f, 0.45f);
    [SerializeField] private Color lossColor = new Color(1f, 0.25f, 0.25f);
    [SerializeField] private Color evenColor = new Color(1f, 0.92f, 0.23f);

    private Canvas parentCanvas;
    private Coroutine activePopup;

    private void Awake()
    {
        Instance = this;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (popupRect == null) popupRect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private Vector2 GetMouseCanvasPosition()
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            Input.mousePosition,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out localPoint);
        return localPoint;
    }

    public void Show(int totalProfit)
    {
        if (activePopup != null) StopCoroutine(activePopup);
        activePopup = StartCoroutine(PopupRoutine(totalProfit));
    }

    private IEnumerator PopupRoutine(int totalProfit)
    {
        // --- Setup text & color ---
        if (totalProfit > 0)
        {
            popupText.text = $"+${totalProfit:N0} PROFIT";
            popupText.color = profitColor;
        }
        else if (totalProfit < 0)
        {
            popupText.text = $"-${Mathf.Abs(totalProfit):N0} LOSS";
            popupText.color = lossColor;
        }
        else
        {
            popupText.text = "$0 BREAK EVEN";
            popupText.color = evenColor;
        }

        // --- Position at mouse cursor ---
        Vector2 spawnPos = GetMouseCanvasPosition() + Vector2.up * aboveMouseOffset;
        popupRect.anchoredPosition = spawnPos;
        popupRect.localScale = Vector3.one * 0.4f;
        canvasGroup.alpha = 0f;

        // === PHASE 1: Snap in — fast fade + scale overshoot ===
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            canvasGroup.alpha = p;
            float scale = Mathf.LerpUnclamped(0.4f, scaleOvershoot, EaseOutBack(p));
            popupRect.localScale = Vector3.one * scale;
            popupRect.anchoredPosition = spawnPos + Vector2.up * (floatUpDistance * 0.08f * p);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // === PHASE 2: Settle scale back to 1.0 ===
        float settleDuration = 0.08f;
        t = 0f;
        float settleStart = popupRect.localScale.x;
        while (t < settleDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / settleDuration);
            float scale = Mathf.Lerp(settleStart, 1f, p * p);
            popupRect.localScale = Vector3.one * scale;
            yield return null;
        }
        popupRect.localScale = Vector3.one;

        // === PHASE 3: Hold ===
        yield return new WaitForSeconds(holdDuration);

        // === PHASE 4: Float up + fade out ===
        Vector2 holdPos = popupRect.anchoredPosition;
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            float easedP = EaseInQuad(p);
            canvasGroup.alpha = 1f - easedP;
            popupRect.anchoredPosition = holdPos + Vector2.up * (floatUpDistance * easedP);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        activePopup = null;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static float EaseInQuad(float t)
    {
        return t * t;
    }
}
