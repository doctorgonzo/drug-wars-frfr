using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DebtManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text debtText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text interestWarningText;
    [SerializeField] private Button payDebtButton;
    [SerializeField] private TMP_InputField payAmountInput;

    [Header("Game Over")]
    [SerializeField] private string gameOverSceneName = "GameOver";
    [SerializeField] private string winSceneName = "YouWin";

    private GameTime gameTime;

    private void Start()
    {
        gameTime = FindObjectOfType<GameTime>();

        if (gameTime != null)
            gameTime.DayChanged += OnDayChanged;

        if (payDebtButton != null && payAmountInput != null)
        {
            payDebtButton.onClick.AddListener(OnPayDebtClicked);
        }

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnDebtChanged += _ => RefreshUI();

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (gameTime != null)
            gameTime.DayChanged -= OnDayChanged;

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnDebtChanged -= _ => RefreshUI();
    }

    private void OnDayChanged(GameTime.GameDateTime dt)
    {
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        // Apply daily interest
        if (!ps.IsDebtPaidOff)
        {
            int debtBefore = ps.Debt;
            ps.ApplyDailyInterest();
            int interest = ps.Debt - debtBefore;

            if (interestWarningText != null)
            {
                interestWarningText.text = $"Interest: +${interest:N0} added to your debt!";
                interestWarningText.gameObject.SetActive(true);
                CancelInvoke(nameof(HideInterestWarning));
                Invoke(nameof(HideInterestWarning), 4f);
            }
        }

        RefreshUI();
        CheckEndConditions(dt);
    }

    private void HideInterestWarning()
    {
        if (interestWarningText != null)
            interestWarningText.gameObject.SetActive(false);
    }

    private void CheckEndConditions(GameTime.GameDateTime dt)
    {
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        // Win: debt paid off
        if (ps.IsDebtPaidOff)
        {
            if (!string.IsNullOrEmpty(winSceneName))
                SceneManager.LoadScene(winSceneName);
            return;
        }

        // Lose: day limit exceeded
        if (dt.day > ps.DayLimit)
        {
            if (!string.IsNullOrEmpty(gameOverSceneName))
                SceneManager.LoadScene(gameOverSceneName);
        }
    }

    private void OnPayDebtClicked()
    {
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        int amount = 0;
        if (payAmountInput != null && int.TryParse(payAmountInput.text, out var parsed))
            amount = parsed;
        else
            amount = ps.PlayerWallet; // pay all if no input

        if (amount <= 0) return;

        ps.PayDebt(amount);
        RefreshUI();

        // Check immediate win
        if (ps.IsDebtPaidOff)
        {
            if (!string.IsNullOrEmpty(winSceneName))
                SceneManager.LoadScene(winSceneName);
        }
    }

    public void RefreshUI()
    {
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        if (debtText != null)
        {
            if (ps.IsDebtPaidOff)
                debtText.text = "Debt: PAID OFF";
            else
                debtText.text = $"Debt: ${ps.Debt:N0}";
        }

        if (dayText != null && gameTime != null)
        {
            int daysLeft = Mathf.Max(0, ps.DayLimit - gameTime.Day);
            dayText.text = $"Day {gameTime.Day} / {ps.DayLimit}  ({daysLeft} left)";
        }

        if (payDebtButton != null)
            payDebtButton.interactable = !ps.IsDebtPaidOff && ps.PlayerWallet > 0;
    }
}
