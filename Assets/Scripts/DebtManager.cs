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
    [SerializeField] private Button borrowButton;
    [SerializeField] private TMP_InputField borrowAmountInput;
    [SerializeField] private TMP_Text borrowInfoText;

    [Header("Game Over")]
    [SerializeField] private string gameOverSceneName = "GameOver";
    [SerializeField] private string winSceneName = "YouWin";

    private GameTime gameTime;

    private void Start()
    {
        gameTime = GameTime.Instance ?? FindObjectOfType<GameTime>();

        if (gameTime != null)
            gameTime.DayChanged += OnDayChanged;

        if (payDebtButton != null)
            payDebtButton.onClick.AddListener(OnPayDebtClicked);

        if (borrowButton != null)
            borrowButton.onClick.AddListener(OnBorrowClicked);

        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnDebtChanged += _ => RefreshUI();

        ConfigureTextAutoSize();
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

            if (ToastUI.Instance != null)
                ToastUI.Instance.Show($"+${interest:N0} INTEREST\nDebt: ${ps.Debt:N0}", new Color(1f, 0.35f, 0.2f));
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

    private void ConfigureTextAutoSize()
    {
        SetAutoSize(debtText, 10f, 28f);
        SetAutoSize(dayText, 8f, 20f);
        SetAutoSize(interestWarningText, 8f, 16f);
        SetAutoSize(borrowInfoText, 7f, 14f);
    }

    private static void SetAutoSize(TMP_Text label, float min, float max)
    {
        if (label == null) return;
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void OnBorrowClicked()
    {
        var ps = PlayerStats.Instance;
        if (ps == null) return;

        int requested = ps.MaxSingleBorrow;
        if (borrowAmountInput != null && int.TryParse(borrowAmountInput.text, out var parsed))
            requested = parsed;

        int actual = ps.BorrowFromShark(requested);
        if (actual <= 0) return;

        int debtAdded = Mathf.RoundToInt(actual * (1f + ps.BorrowPremiumRate));
        if (interestWarningText != null)
        {
            interestWarningText.text = $"Borrowed ${actual:N0} — debt increased by ${debtAdded:N0}!";
            interestWarningText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideInterestWarning));
            Invoke(nameof(HideInterestWarning), 4f);
        }

        RefreshUI();
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
            dayText.text = $"Day {gameTime.Day} / {ps.DayLimit}  ({daysLeft} left)\n{gameTime.Hour:D2}:{gameTime.Minute:D2}";
        }

        if (payDebtButton != null)
            payDebtButton.interactable = !ps.IsDebtPaidOff && ps.PlayerWallet > 0;

        if (borrowButton != null)
            borrowButton.interactable = !ps.IsDebtPaidOff;

        if (borrowInfoText != null)
        {
            int premium = Mathf.RoundToInt(ps.MaxSingleBorrow * ps.BorrowPremiumRate);
            borrowInfoText.text = $"Max ${ps.MaxSingleBorrow:N0} — shark takes {ps.BorrowPremiumRate * 100:F0}% cut (costs ${ps.MaxSingleBorrow + premium:N0} in debt)";
        }
    }
}
