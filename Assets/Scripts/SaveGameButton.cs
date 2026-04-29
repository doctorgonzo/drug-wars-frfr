using System.Collections;
using TMPro;
using UnityEngine;

// Attach to any Button. Wire the Button's onClick to SaveGame().
// Optionally wire feedbackText to show a brief "SAVED!" confirmation.
public class SaveGameButton : MonoBehaviour
{
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackDuration = 1.5f;

    private Coroutine _feedbackRoutine;

    public void SaveGame()
    {
        if (GameSessionManager.Instance == null)
        {
            Debug.LogWarning("[SaveGameButton] GameSessionManager not found.");
            return;
        }

        GameSessionManager.Instance.SaveGame();

        if (feedbackText != null)
        {
            if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(ShowFeedback());
        }
    }

    private IEnumerator ShowFeedback()
    {
        feedbackText.text = "SAVED!";
        feedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(feedbackDuration);
        feedbackText.gameObject.SetActive(false);
        _feedbackRoutine = null;
    }
}
