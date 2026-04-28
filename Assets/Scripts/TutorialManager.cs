using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public string title;
        [TextArea(2, 6)] public string body;
    }

    [Header("Steps")]
    [SerializeField] private TutorialStep[] steps = new TutorialStep[]
    {
        new TutorialStep
        {
            title = "WELCOME TO THE GAME",
            body = "You owe Big Tony $50,000. You have 30 days to pay it back before he sends someone to collect — permanently.\n\nHere's how to stay alive."
        },
        new TutorialStep
        {
            title = "THE DEALERS",
            body = "Those portraits on the map are dealers. Click one to open their inventory.\n\nBuy drugs cheap here, sell them to another dealer for profit."
        },
        new TutorialStep
        {
            title = "HEAT",
            body = "Every buy and sell adds Heat to your meter. Hit 100% and the cops show up.\n\nWait between deals to let it decay — or keep pushing your luck."
        },
        new TutorialStep
        {
            title = "TRAVEL",
            body = "Prices vary wildly between cities. Travel to find better spreads.\n\nEach trip costs $500 and 6 hours. Time is debt."
        },
        new TutorialStep
        {
            title = "THE DEBT",
            body = "Open the Debt tab in your info panel to make payments.\n\nInterest compounds daily. Pay Big Tony off before Day 30 — or it's over.\n\nGood luck."
        },
    };

    [Header("UI References")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private Button skipButton;
    [SerializeField] private TMP_Text stepCounterText;

    [Header("Settings")]
    [SerializeField] private float fadeInDuration = 0.25f;

    private const string SeenKey = "TutorialSeen_v1";
    private int currentStep = 0;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        if (PlayerPrefs.GetInt(SeenKey, 0) == 1)
        {
            gameObject.SetActive(false);
            return;
        }

        canvasGroup = tutorialPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = tutorialPanel.AddComponent<CanvasGroup>();

        nextButton.onClick.AddListener(OnNext);
        if (skipButton != null) skipButton.onClick.AddListener(Dismiss);

        tutorialPanel.SetActive(true);
        canvasGroup.alpha = 0f;
        ShowStep(0);
        StartCoroutine(FadeIn());
    }

    private void ShowStep(int index)
    {
        var step = steps[index];
        titleText.text = step.title;
        bodyText.text = step.body;

        bool isLast = index == steps.Length - 1;
        if (nextButtonLabel != null)
            nextButtonLabel.text = isLast ? "GOT IT" : "NEXT";

        if (stepCounterText != null)
            stepCounterText.text = $"{index + 1} / {steps.Length}";
    }

    private void OnNext()
    {
        currentStep++;
        if (currentStep >= steps.Length)
            Dismiss();
        else
            ShowStep(currentStep);
    }

    private void Dismiss()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        StartCoroutine(FadeOutAndHide());
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOutAndHide()
    {
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        tutorialPanel.SetActive(false);
    }
}
