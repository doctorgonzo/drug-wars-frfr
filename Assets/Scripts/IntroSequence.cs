using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroSequence : MonoBehaviour
{
    [System.Serializable]
    public class IntroPanel
    {
        [TextArea(2, 5)] public string headline;
        [TextArea(3, 8)] public string body;
    }

    [Header("Panels")]
    [SerializeField] private IntroPanel[] panels = new IntroPanel[]
    {
        new IntroPanel { headline = "THREE WEEKS AGO", body = "You borrowed $50,000 from a loan shark named Big Tony.\n\nSeemed like a good idea at the time." },
        new IntroPanel { headline = "THE DEAL", body = "30 days to pay it back.\n\nWith interest." },
        new IntroPanel { headline = "YOUR OPTIONS", body = "You know a few dealers across the city.\n\nBuy low. Sell high. Don't get caught." },
        new IntroPanel { headline = "GET TO WORK.", body = "" },
    };

    [Header("UI References")]
    [SerializeField] private TMP_Text headlineText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Settings")]
    [SerializeField] private string nextSceneName = "CharCreation";
    [SerializeField] private float panelFadeDuration = 0.4f;

    private int currentPanel = 0;
    private bool transitioning = false;

    private void Start()
    {
        nextButton.onClick.AddListener(OnNextClicked);
        ShowPanel(0, instant: true);
    }

    private void OnNextClicked()
    {
        if (transitioning) return;

        currentPanel++;
        if (currentPanel >= panels.Length)
            StartCoroutine(LoadNextScene());
        else
            StartCoroutine(CrossfadeToPanel(currentPanel));
    }

    private void ShowPanel(int index, bool instant = false)
    {
        var p = panels[index];
        headlineText.text = p.headline;
        bodyText.text = p.body;
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(p.body));

        bool isLast = index == panels.Length - 1;
        if (nextButtonLabel != null)
            nextButtonLabel.text = isLast ? "LET'S GO" : "NEXT";

        if (instant && panelCanvasGroup != null)
            panelCanvasGroup.alpha = 1f;
    }

    private IEnumerator CrossfadeToPanel(int index)
    {
        transitioning = true;

        // Fade out
        yield return StartCoroutine(FadeCanvasGroup(panelCanvasGroup, 1f, 0f, panelFadeDuration));

        ShowPanel(index);

        // Fade in
        yield return StartCoroutine(FadeCanvasGroup(panelCanvasGroup, 0f, 1f, panelFadeDuration));

        transitioning = false;
    }

    private IEnumerator LoadNextScene()
    {
        transitioning = true;
        if (FadeController.Instance != null)
            FadeController.Instance.FadeOut(panelFadeDuration);
        yield return new WaitForSeconds(panelFadeDuration + 0.1f);
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        cg.alpha = to;
    }
}
