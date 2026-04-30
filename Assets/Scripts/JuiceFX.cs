using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "Juice" effects manager — animations, particles, screen flash, number tweens.
//
// Auto-spawned at game start via [RuntimeInitializeOnLoadMethod] — zero Editor wiring.
// All assets are generated procedurally (a circle texture for coin particles).
//
// Public API:
//   JuiceFX.Instance.CoinBurstAtScreen(Vector2 screenPos, int count, Color tint)
//   JuiceFX.Instance.CoinBurstAtUI(RectTransform anchor, int count, Color tint)
//   JuiceFX.Instance.FlashScreen(Color color, float duration)
//   JuiceFX.Instance.NumberPunch(TMP_Text label, float amount = 1.25f)
//   JuiceFX.Instance.TweenIntegerText(TMP_Text label, int from, int to, string prefix, string suffix, float duration)
public class JuiceFX : MonoBehaviour
{
    public static JuiceFX Instance { get; private set; }

    // Two-canvas setup so we can render under (flash, particles) AND over (popups, text overlays)
    // depending on the effect — for now everything sits on a single high-priority canvas.
    private Canvas _canvas;
    private RectTransform _root;
    private Image _flashImage;

    // Procedural sprites
    private Sprite _coinSprite;

    // Pool for coin particles
    private readonly Stack<RectTransform> _coinPool = new Stack<RectTransform>();
    private const int CoinPoolMax = 256;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("JuiceFX");
        go.AddComponent<JuiceFX>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
        _coinSprite = MakeCircleSprite(64, Color.white);
    }

    // ---- Setup ----
    private void BuildCanvas()
    {
        var canvasGo = new GameObject("JuiceCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000; // above gameplay UI but below cheat menu (32000)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>().enabled = false; // never intercepts clicks

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Root transform for all juice elements (so we can shake later if we want)
        _root = MakeRect(canvasGo.transform, "Root");
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;

        // Full-screen flash image (transparent by default)
        var flashGo = new GameObject("Flash", typeof(RectTransform));
        flashGo.transform.SetParent(_root, false);
        _flashImage = flashGo.AddComponent<Image>();
        _flashImage.color = new Color(1, 0, 0, 0);
        _flashImage.raycastTarget = false;
        var fRT = _flashImage.rectTransform;
        fRT.anchorMin = Vector2.zero;
        fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero;
        fRT.offsetMax = Vector2.zero;
    }

    // ---- Public effects ----

    public void FlashScreen(Color color, float duration = 0.35f, float peakAlpha = 0.45f)
    {
        if (_flashImage == null) return;
        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine(color, duration, peakAlpha));
    }

    private IEnumerator FlashRoutine(Color color, float duration, float peakAlpha)
    {
        if (_flashImage == null) yield break;
        float half = Mathf.Max(0.01f, duration * 0.5f);
        // Ramp up
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(0f, peakAlpha, t / half);
            _flashImage.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        // Ramp down
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(peakAlpha, 0f, t / half);
            _flashImage.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        _flashImage.color = new Color(color.r, color.g, color.b, 0f);
    }

    public void CoinBurstAtScreen(Vector2 screenPos, int count = 12, Color? tint = null)
    {
        Color c = tint ?? new Color(1f, 0.85f, 0.2f);
        Vector2 anchored = ScreenToCanvasAnchored(screenPos);
        for (int i = 0; i < count; i++)
            StartCoroutine(CoinRoutine(anchored, c));
    }

    public void CoinBurstAtUI(RectTransform anchor, int count = 12, Color? tint = null)
    {
        if (anchor == null) return;
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, worldCenter);
        CoinBurstAtScreen(screenPos, count, tint);
    }

    public void NumberPunch(TMP_Text label, float scaleAmount = 1.30f, float duration = 0.25f)
    {
        if (label == null) return;
        StartCoroutine(NumberPunchRoutine(label.rectTransform, scaleAmount, duration));
    }

    private IEnumerator NumberPunchRoutine(RectTransform rt, float scaleAmount, float duration)
    {
        if (rt == null) yield break;
        Vector3 baseScale = rt.localScale;
        Vector3 peak = baseScale * scaleAmount;
        float half = Mathf.Max(0.01f, duration * 0.4f);
        // Snap up
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(baseScale, peak, t / half);
            yield return null;
        }
        // Settle back (slightly longer for elastic feel)
        float settle = duration - half;
        t = 0f;
        while (t < settle)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(peak, baseScale, t / settle);
            yield return null;
        }
        rt.localScale = baseScale;
    }

    // Animates an integer counter from -> to over duration, formatting as "{prefix}{value:N0}{suffix}".
    public void TweenIntegerText(TMP_Text label, int from, int to, string prefix, string suffix, float duration = 0.45f)
    {
        if (label == null) return;
        StartCoroutine(TweenIntRoutine(label, from, to, prefix, suffix, duration));
    }

    private IEnumerator TweenIntRoutine(TMP_Text label, int from, int to, string prefix, string suffix, float duration)
    {
        if (label == null) yield break;
        if (duration <= 0f || from == to)
        {
            label.text = $"{prefix}{to:N0}{suffix}";
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            // Ease-out cubic
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            label.text = $"{prefix}{v:N0}{suffix}";
            yield return null;
        }
        label.text = $"{prefix}{to:N0}{suffix}";
    }

    // ---- Coin particle ----
    private IEnumerator CoinRoutine(Vector2 startAnchored, Color tint)
    {
        var rt = GetCoin();
        var img = rt.GetComponent<Image>();
        img.color = tint;

        rt.anchoredPosition = startAnchored;
        rt.localScale = Vector3.one;

        // Random initial velocity (anchored-units / sec) + a touch of spread
        Vector2 dir = new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.3f)).normalized;
        float speed = Random.Range(550f, 950f);
        Vector2 vel = dir * speed;
        float gravity = 1500f;
        float life = Random.Range(0.7f, 1.05f);
        float t = 0f;
        float startScale = Random.Range(0.7f, 1.1f);

        while (t < life)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            vel.y -= gravity * dt;
            rt.anchoredPosition += vel * dt;

            float u = t / life;
            float scale = startScale * (1f - 0.4f * u);
            rt.localScale = new Vector3(scale, scale, 1f);
            float a = Mathf.Lerp(1f, 0f, u);
            img.color = new Color(tint.r, tint.g, tint.b, a);
            yield return null;
        }

        ReturnCoin(rt);
    }

    private RectTransform GetCoin()
    {
        if (_coinPool.Count > 0)
        {
            var rt = _coinPool.Pop();
            rt.gameObject.SetActive(true);
            return rt;
        }
        var go = new GameObject("Coin", typeof(RectTransform));
        go.transform.SetParent(_root, false);
        var rt2 = (RectTransform)go.transform;
        rt2.anchorMin = new Vector2(0.5f, 0.5f);
        rt2.anchorMax = new Vector2(0.5f, 0.5f);
        rt2.pivot = new Vector2(0.5f, 0.5f);
        rt2.sizeDelta = new Vector2(28f, 28f);
        var img = go.AddComponent<Image>();
        img.sprite = _coinSprite;
        img.raycastTarget = false;
        return rt2;
    }

    private void ReturnCoin(RectTransform rt)
    {
        if (rt == null) return;
        rt.gameObject.SetActive(false);
        if (_coinPool.Count < CoinPoolMax)
            _coinPool.Push(rt);
        else
            Destroy(rt.gameObject);
    }

    // ---- Helpers ----
    private static RectTransform MakeRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Convert screen-space pixel position to anchored-units relative to the JuiceCanvas
    // (centered pivot 0.5/0.5). For ScreenSpaceOverlay this is straightforward.
    private Vector2 ScreenToCanvasAnchored(Vector2 screenPos)
    {
        Vector2 size = _canvas.pixelRect.size;
        float sf = _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;
        return new Vector2(screenPos.x - size.x * 0.5f, screenPos.y - size.y * 0.5f) / sf;
    }

    // Procedurally generate a soft-edged circle sprite for coin particles.
    private static Sprite MakeCircleSprite(int diameter, Color color)
    {
        var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = diameter * 0.5f;
        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x - r + 0.5f;
                float dy = y - r + 0.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01((r - dist) / 2f); // 2px soft edge
                Color c = new Color(color.r, color.g, color.b, color.a * t);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 100f);
    }
}
