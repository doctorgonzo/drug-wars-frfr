using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CityUIHandler : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] TMP_Text playerName;
    [SerializeField] TMP_Text playerWallet;
    [SerializeField] Image playerImage;

    [Header("Equipment")]
    [SerializeField] TMP_Text trenchSlotsText;
    [SerializeField] TMP_Text trenchArmorText;
    [SerializeField] Image trenchImage;
    [SerializeField] TMP_Text weaponDamageText;
    [SerializeField] Image weaponImage;

    [Header("City")]
    [SerializeField] TMP_Text cityNameText;
    [SerializeField] TMP_Text cityPopulationText;
    [SerializeField] TMP_Text cityFavoriteDrugText;

    [Header("Debt & Time")]
    [SerializeField] TMP_Text debtText;
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text netWorthText;

    [Header("Systems")]
    [SerializeField] private TravelManager travelManager;

    private void Start()
    {
        var ps = PlayerStats.Instance;
        playerName.text = ps.PlayerName;
        playerWallet.text = $"Wallet: ${ps.PlayerWallet:N0}";
        UpdateTrenchSlotsDisplay();

        if (ps.CurrentTrench != null)
        {
            if (trenchSlotsText != null) trenchSlotsText.text = "Slots: " + ps.CurrentTrench.StorageSlots;
            if (trenchArmorText != null) trenchArmorText.text = "Armor: " + ps.CurrentTrench.ArmorValue;
            if (trenchImage != null) trenchImage.sprite = ps.CurrentTrench.Image;
        }

        if (ps.CurrentWeapon != null)
        {
            if (weaponDamageText != null) weaponDamageText.text = "Damage: " + ps.CurrentWeapon.Damage;
            if (weaponImage != null) weaponImage.sprite = ps.CurrentWeapon.Image;
        }

        playerImage.sprite = ps.PlayerSprite;

        if (ps.CurrentCity != null)
        {
            cityNameText.text = ps.CurrentCity.Name;
            cityPopulationText.text = ps.CurrentCity.Population.ToString();
            cityFavoriteDrugText.text = ps.CurrentCity.FavoriteDrug != null ? ps.CurrentCity.FavoriteDrug.Name : "None";
        }

        ps.OnInventoryChanged += UpdateTrenchSlotsDisplay;
        ps.OnInventoryChanged += UpdateNetWorthDisplay;
        ps.OnWalletChanged += OnWalletChangedHandler;
        ps.OnDebtChanged += _ => UpdateDebtDisplay();
        UpdateDebtDisplay();
        UpdateDayDisplay();
        UpdateNetWorthDisplay();
    }

    public void UpdateWalletDisplay()
    {
        playerWallet.text = $"Wallet: ${PlayerStats.Instance.PlayerWallet:N0}";

        // After updating the text, check if travel should be unlocked
        if (travelManager != null)
        {
            travelManager.CheckTravelStatus();
        }
    }

    public void UpdateCityInfo()
    {
        var city = PlayerStats.Instance.CurrentCity;
        if (city == null) return;
        cityNameText.text = city.Name;
        cityPopulationText.text = city.Population.ToString();
        cityFavoriteDrugText.text = city.FavoriteDrug != null ? city.FavoriteDrug.Name : "None";
    }
    public void UpdateDebtDisplay()
    {
        if (debtText == null || PlayerStats.Instance == null) return;
        var ps = PlayerStats.Instance;
        if (ps.IsDebtPaidOff)
            debtText.text = "Debt: PAID OFF";
        else
            debtText.text = $"Debt: ${ps.Debt:N0}";
    }

    public void UpdateDayDisplay()
    {
        if (dayText == null) return;
        var gt = FindObjectOfType<GameTime>();
        if (gt == null || PlayerStats.Instance == null) return;
        int daysLeft = Mathf.Max(0, PlayerStats.Instance.DayLimit - gt.Day);
        dayText.text = $"Day {gt.Day} / {PlayerStats.Instance.DayLimit}  ({daysLeft} left)";
    }

    private void OnDestroy()
    {
        var ps = PlayerStats.Instance;
        if (ps != null)
        {
            ps.OnInventoryChanged -= UpdateTrenchSlotsDisplay;
            ps.OnInventoryChanged -= UpdateNetWorthDisplay;
            ps.OnWalletChanged -= OnWalletChangedHandler;
        }
    }

    private void OnWalletChangedHandler(int _) { UpdateWalletDisplay(); UpdateNetWorthDisplay(); }

    public void UpdateNetWorthDisplay()
    {
        if (netWorthText == null || PlayerStats.Instance == null) return;
        netWorthText.text = $"Net Worth: ${PlayerStats.Instance.NetWorth:N0}";
    }

    private void UpdateTrenchSlotsDisplay()
    {
        if (PlayerStats.Instance == null || trenchSlotsText == null) return;
        int used = PlayerStats.Instance.GetUsedSlots();
        int total = PlayerStats.Instance.GetTotalSlots();
        trenchSlotsText.text = $"Slots: {used}/{total}";
    }
}
