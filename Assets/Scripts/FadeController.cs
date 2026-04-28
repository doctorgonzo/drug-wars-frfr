using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeController : MonoBehaviour
{
    public static FadeController Instance { get; private set; }

    [SerializeField] private Image fadeImage;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration = 1f;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!fadeImage) fadeImage = GetComponentInChildren<Image>();
        if (!canvasGroup) canvasGroup = GetComponentInChildren<CanvasGroup>();

        // Start fully black so the fade-in plays on scene load
        SetAlpha(1f);
        SetBlocking(true);
    }

    private void Start()
    {
        FadeIn(fadeInDuration);
    }

    public void FadeOut(float duration = 1f)
    {
        StartFade(0f, 1f, duration);
    }

    public void FadeIn(float duration = 1f)
    {
        StartFade(1f, 0f, duration);
    }

    private void StartFade(float from, float to, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(from, to, duration));
    }

    private IEnumerator FadeRoutine(float startA, float endA, float duration)
    {
        SetBlocking(true);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(startA, endA, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetAlpha(endA);

        if (endA <= 0.001f) SetBlocking(false);
        fadeRoutine = null;
    }

    private void SetAlpha(float a)
    {
        if (fadeImage)
        {
            var c = fadeImage.color;
            c.a = a;
            fadeImage.color = c;
        }
        if (canvasGroup) canvasGroup.alpha = a;
    }

    private void SetBlocking(bool block)
    {
        if (canvasGroup)
        {
            canvasGroup.blocksRaycasts = block;
            canvasGroup.interactable = false;
        }
        if (fadeImage) fadeImage.raycastTarget = block;
    }
}
