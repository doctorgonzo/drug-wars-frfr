using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ToastUI : MonoBehaviour
{
    public static ToastUI Instance { get; private set; }

    [SerializeField] private TMP_Text toastText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform toastRect;

    [SerializeField] private float fadeInDuration = 0.15f;
    [SerializeField] private float holdDuration = 2.5f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float floatUpDistance = 40f;

    private Coroutine activeToast;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (toastRect == null) toastRect = GetComponent<RectTransform>();

        // Add dark background panel to toastRect if not already present
        var bg = toastRect.GetComponent<Image>();
        if (bg == null) bg = toastRect.gameObject.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (toastText != null)
        {
            toastText.enableAutoSizing = true;
            toastText.fontSizeMin = 18f;
            toastText.fontSizeMax = 56f;
            toastText.fontStyle = TMPro.FontStyles.Bold;
        }
    }

    public void Show(string message, Color color)
    {
        if (activeToast != null) StopCoroutine(activeToast);
        activeToast = StartCoroutine(ToastRoutine(message, color));
    }

    private IEnumerator ToastRoutine(string message, Color color)
    {
        toastText.text = message;
        toastText.color = color;

        Vector2 startPos = toastRect.anchoredPosition;
        toastRect.localScale = Vector3.one * 0.8f;
        canvasGroup.alpha = 0f;

        // Fade in + scale up
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeInDuration);
            canvasGroup.alpha = p;
            toastRect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, p * p);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        toastRect.localScale = Vector3.one;

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // Float up + fade out
        Vector2 holdPos = toastRect.anchoredPosition;
        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeOutDuration);
            float eased = p * p;
            canvasGroup.alpha = 1f - eased;
            toastRect.anchoredPosition = holdPos + Vector2.up * (floatUpDistance * eased);
            yield return null;
        }

        toastRect.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;
        activeToast = null;
    }
}
