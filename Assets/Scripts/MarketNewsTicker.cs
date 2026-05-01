using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarketNewsTicker : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text newsText;
    [SerializeField] private GameObject newsPanel;
    [SerializeField] private RectTransform tickerContainer;

    [Header("Scroll Settings")]
    [Tooltip("Pixels per second the text scrolls.")]
    [SerializeField] private float scrollSpeed = 150f;
    [Tooltip("Gap (in pixels) between consecutive messages.")]
    [SerializeField] private float messageGap = 80f;
    [Tooltip("If true, the ticker loops forever. If false, it scrolls each message once and stops.")]
    [SerializeField] private bool loopForever = true;

    [Header("Text Auto-Sizing")]
    [SerializeField] private float minFontSize = 12f;
    [SerializeField] private float maxFontSize = 64f;

    [Header("Edge Fade")]
    [Tooltip("Width in pixels of the soft fade at each horizontal edge.")]
    [SerializeField] private int edgeFadeWidth = 40;

    [Header("Flavor Text")]
    [SerializeField] private string[] boomPrefixes = new string[]
    {
        "SHORTAGE ALERT",
        "DEMAND SURGE",
        "PANIC BUYING",
        "SUPPLY CHAIN CRISIS"
    };
    [SerializeField] private string[] bustPrefixes = new string[]
    {
        "MARKET CRASH",
        "POLICE RAID",
        "SURPLUS FLOOD",
        "SUPPLY GLUT"
    };

    private RectTransform textRT;

    private void Start()
    {
        if (newsPanel != null)
            newsPanel.SetActive(false);

        if (newsText != null)
        {
            textRT = newsText.rectTransform;
            ConfigureTextForScrolling();
        }

        // Fall back to newsPanel if tickerContainer not assigned
        if (tickerContainer == null && newsPanel != null)
            tickerContainer = newsPanel.GetComponent<RectTransform>();

        // RectMask2D must be on the PARENT of the text to clip it at the edges
        var maskTarget = newsText != null ? newsText.transform.parent : null;
        if (maskTarget != null)
        {
            var mask = maskTarget.GetComponent<RectMask2D>();
            if (mask == null)
                mask = maskTarget.gameObject.AddComponent<RectMask2D>();
            // Soft fade at horizontal edges
            mask.softness = new Vector2Int(edgeFadeWidth, 0);
        }

        StartCoroutine(CheckAndShowEvents());
    }

    private void ConfigureTextForScrolling()
    {
        // Single-line, no wrapping
        newsText.enableWordWrapping = false;
        newsText.overflowMode = TextOverflowModes.Overflow;

        // Auto-size font to fit the container's height
        newsText.enableAutoSizing = true;
        newsText.fontSizeMin = minFontSize;
        newsText.fontSizeMax = maxFontSize;

        // Anchor / pivot so anchoredPosition.x is the LEFT edge of the text
        textRT.anchorMin = new Vector2(0f, 0.5f);
        textRT.anchorMax = new Vector2(0f, 0.5f);
        textRT.pivot = new Vector2(0f, 0.5f);
    }

    private IEnumerator CheckAndShowEvents()
    {
        // Small delay so the scene finishes loading
        yield return new WaitForSeconds(0.5f);

        var city = PlayerStats.Instance?.CurrentCity;
        if (city == null) yield break;

        // Rebuild the message list each full loop iteration so dynamic events (market
        // saturation that grows as the player sells) appear within ~1 cycle of happening.
        do
        {
            var messages = BuildMessages(city);
            if (messages.Count == 0)
            {
                // Nothing to say right now — hide the panel and re-check shortly.
                if (newsPanel != null && newsPanel.activeSelf) newsPanel.SetActive(false);
                yield return new WaitForSeconds(2f);
                continue;
            }

            if (newsPanel != null && !newsPanel.activeSelf) newsPanel.SetActive(true);

            foreach (var msg in messages)
                yield return StartCoroutine(ScrollMessage(msg));
        } while (loopForever);

        if (newsPanel != null) newsPanel.SetActive(false);
    }

    private List<string> BuildMessages(City city)
    {
        var messages = new List<string>();

        // City event — leads the ticker
        var cityEvt = CityEventManager.GetEventForCity(city.Name);
        if (cityEvt == CityEventManager.CityEvent.Lockdown)
        {
            messages.Add("<color=#FF4444>!! CITY LOCKDOWN !!</color> — Police presence doubled. Heat penalty active. Prices depressed.");
        }
        else if (cityEvt == CityEventManager.CityEvent.Festival)
        {
            string favName = city.FavoriteDrug != null ? city.FavoriteDrug.Name : "product";
            messages.Add($"<color=#FFD700>CITY FESTIVAL</color> — {favName} demand through the roof! Sell prices DOUBLED today.");
        }
        else if (cityEvt == CityEventManager.CityEvent.Shortage)
        {
            messages.Add("<color=#FF8800>SUPPLY SHORTAGE</color> — Distribution lines cut. All drug prices surging 80%. Heat risk elevated.");
        }

        // Boom/bust per item type
        foreach (var mod in city.priceModifiers)
        {
            var evt = PriceService.DailyEvent(
                city.Name,
                mod.itemType,
                mod.boomChance,
                mod.bustChance
            );

            if (evt == PriceService.MarketEvent.Boom)
            {
                string prefix = boomPrefixes[Random.Range(0, boomPrefixes.Length)];
                messages.Add($"<color=#FFD700>{prefix}</color> — {mod.itemType} prices are <b>skyrocketing</b> in {city.Name}!");
            }
            else if (evt == PriceService.MarketEvent.Bust)
            {
                string prefix = bustPrefixes[Random.Range(0, bustPrefixes.Length)];
                messages.Add($"<color=#FF4444>{prefix}</color> — {mod.itemType} prices have <b>crashed</b> in {city.Name}!");
            }
        }

        // Favorite drug demand label
        if (city.FavoriteDrug != null && city.favoriteDrugDemandMultiplier > 1.1f)
        {
            messages.Add($"<color=#44FF44>HOT MARKET</color> — {city.FavoriteDrug.Name} is in high demand here!");
        }

        // Today's one-shot tip (might point at a different city — that's the player's decision to chase or not).
        var tip = DailyTipService.GetTodaysTip();
        if (tip.Type != DailyTipType.None)
            messages.Add(tip.ToHeadline());

        // Market saturation — surfaces when the player has flooded a drug's market in this city.
        AppendSaturationMessages(messages, city);

        return messages;
    }

    // Appends one message per (current city, drug) where saturation has crossed a tier
    // threshold. Three tiers — slowing (0.4), saturated (0.7), flooded (1.0) — with
    // distinct copy + colors so the player can read severity at a glance.
    private void AppendSaturationMessages(List<string> messages, City city)
    {
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        foreach (var kv in ps.AllMarketSaturation())
        {
            if (!PlayerStats.TryParseMarketKey(kv.Key, out string keyCity, out string drug)) continue;
            if (keyCity != city.Name) continue;

            float sat = kv.Value;
            if (sat >= 1.0f)
                messages.Add($"<color=#FF4444>FLOODED</color> — {city.Name} can barely move {drug} today. Buyers paying scraps.");
            else if (sat >= 0.7f)
                messages.Add($"<color=#FF8800>SATURATED</color> — {city.Name}'s {drug} market is saturated — prices tumbling.");
            else if (sat >= 0.4f)
                messages.Add($"<color=#FFD700>SLOWING</color> — {city.Name} buyers getting picky about {drug} today.");
        }
    }

    private IEnumerator ScrollMessage(string message)
    {
        newsText.text = message;

        // Force layout/text update so preferredWidth is valid
        newsText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        float containerWidth = tickerContainer != null
            ? tickerContainer.rect.width
            : ((RectTransform)newsPanel.transform).rect.width;

        float textWidth = newsText.preferredWidth;

        // Resize the text rect to its preferred width so layout doesn't clip it
        textRT.sizeDelta = new Vector2(textWidth, textRT.sizeDelta.y);

        // Right-to-left scroll: left edge of text starts at the container's right edge
        float startX = containerWidth;
        float endX = -textWidth;
        float distance = Mathf.Abs(endX - startX);
        float duration = distance / Mathf.Max(1f, scrollSpeed);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float x = Mathf.Lerp(startX, endX, t);
            textRT.anchoredPosition = new Vector2(x, textRT.anchoredPosition.y);
            yield return null;
        }
    }
}
