using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeController : MonoBehaviour
{
    public static FadeController Instance { get; private set; }

    [SerializeField] private Image fadeImage;       // assign: FadePanel (Image)
    [SerializeField] private CanvasGroup canvasGroup; // assign: FadePanel (CanvasGroup)
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!fadeImage) fadeImage = GetComponent<Image>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();

        // Ensure clean start
        SetAlpha(0f);
        SetBlocking(false);
    }

    public void FadeOut(float duration = 1f)  // to black
    {
        StartFade(0f, 1f, duration);
    }

    public void FadeIn(float duration = 1f)   // from black
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
        // Make sure we block clicks when we’re going visible (toward black)
        SetBlocking(true);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp01(t / duration));
            SetAlpha(a);
            yield return null;
        }
        SetAlpha(endA);

        // Only unblock when fully transparent
        if (endA <= 0.001f) SetBlocking(false);

        fadeRoutine = null;
    }

    private void SetAlpha(float a)
    {
        // Drive both the Image color and CanvasGroup for robustness
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
            canvasGroup.interactable = false; // never interactable
        }
        if (fadeImage)
        {
            // Extra belt-and-braces: let the graphic eat rays when blocking
            fadeImage.raycastTarget = block;
        }
    }
}
