using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharCreationUI : MonoBehaviour
{
    [Header("Panels & Buttons")]
    [SerializeField] GameObject charCreationPanel;
    [SerializeField] Button continueButton;

    [Header("Character")]
    [SerializeField] Image charImage;
    [SerializeField] Sprite[] charSprites;
    [SerializeField] TMP_InputField nameInput;

    [Header("Trenchcoat")]
    [SerializeField] Image trenchImage;
    [SerializeField] Trenchcoat[] trenchcoats;
    [SerializeField] TMP_Text trenchName;
    [SerializeField] TMP_Text trenchDescription;
    [SerializeField] TMP_Text trenchCost;
    [SerializeField] TMP_Text trenchSlots;
    [SerializeField] TMP_Text trenchArmor;

    [Header("Weapon")]
    [SerializeField] Image weaponImage;
    [SerializeField] Weapon[] weapons;
    [SerializeField] TMP_Text weaponName;
    [SerializeField] TMP_Text weaponDescription;
    [SerializeField] TMP_Text weaponCost;
    [SerializeField] TMP_Text weaponDamage;

    [Header("Economy & City")]
    [SerializeField] TMP_Text walletText;
    [SerializeField] City startingCity;
    private int curCharIndex;
    private int curTrenchIndex;
    private int curWeaponIndex;

    private void Start()
    {
        curCharIndex = 0;
        curTrenchIndex = 0;
        curWeaponIndex = 0;
        charImage.sprite = charSprites[0];
        nameInput.onEndEdit.AddListener(delegate { OnEndEdit(); });
        nameInput.onValueChanged.AddListener(OnNameInputChanged);
        continueButton.gameObject.SetActive(false);
        RefreshTrenchDisplay();
        RefreshWeaponDisplay();
        RefreshWalletDisplay();
    }

    private void RefreshTrenchDisplay()
    {
        var t = trenchcoats[curTrenchIndex];
        trenchImage.sprite = t.Image;
        trenchName.text = t.Name;
        trenchDescription.text = t.Description;
        trenchCost.text = $"Cost: ${t.Cost:N0}";
        trenchSlots.text = $"Slots: {t.StorageSlots}";
        trenchArmor.text = $"Armor: {t.ArmorValue}";
    }

    private void RefreshWeaponDisplay()
    {
        var w = weapons[curWeaponIndex];
        weaponImage.sprite = w.Image;
        weaponName.text = w.Name;
        weaponDescription.text = w.Description;
        weaponCost.text = $"Cost: ${w.Cost:N0}";
        weaponDamage.text = $"Damage: {w.Damage}";
    }

    private void RefreshWalletDisplay()
    {
        int remaining = PlayerStats.Instance.PlayerWallet - trenchcoats[curTrenchIndex].Cost - weapons[curWeaponIndex].Cost;
        walletText.text = $"Starting Cash: ${remaining:N0}";
    }

    private void OnNameInputChanged(string value)
    {
        continueButton.gameObject.SetActive(value.Length > 0);
    }

    private void OnEndEdit()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            HandleNameInput();
        }
    }

    public void HandleNameInput()
    {
        PlayerStats.Instance.PlayerName = nameInput.text;
        nameInput.gameObject.SetActive(false);
    }

    public void HandleCharPlus()
    {
        curCharIndex++;
        if (curCharIndex >= charSprites.Length)
        {
            curCharIndex = 0;
        }
        charImage.sprite = charSprites[curCharIndex];
    }

    public void HandleCharMinus()
    {
        curCharIndex--;
        if (curCharIndex < 0)
        {
            curCharIndex = charSprites.Length - 1;
        }
        charImage.sprite = charSprites[curCharIndex];
    }

    public void HandleTrenchPlus()
    {
        curTrenchIndex = (curTrenchIndex + 1) % trenchcoats.Length;
        RefreshTrenchDisplay();
        RefreshWalletDisplay();
    }

    public void HandleTrenchMinus()
    {
        curTrenchIndex = (curTrenchIndex - 1 + trenchcoats.Length) % trenchcoats.Length;
        RefreshTrenchDisplay();
        RefreshWalletDisplay();
    }

    public void HandleWeaponPlus()
    {
        curWeaponIndex = (curWeaponIndex + 1) % weapons.Length;
        RefreshWeaponDisplay();
        RefreshWalletDisplay();
    }

    public void HandleWeaponMinus()
    {
        curWeaponIndex = (curWeaponIndex - 1 + weapons.Length) % weapons.Length;
        RefreshWeaponDisplay();
        RefreshWalletDisplay();
    }

    public void HandleContinue()
    {
        PlayerStats.Instance.PlayerName = nameInput.text;
        PlayerStats.Instance.PlayerWallet -= (trenchcoats[curTrenchIndex].Cost + weapons[curWeaponIndex].Cost);
        PlayerStats.Instance.PlayerSprite = charSprites[curCharIndex];
        PlayerStats.Instance.CurrentTrench = trenchcoats[curTrenchIndex];
        PlayerStats.Instance.CurrentCity = startingCity;
        PlayerStats.Instance.CurrentWeapon = weapons[curWeaponIndex];
        PlayerStats.Instance.InitializeDebt();
        FadeController.Instance.FadeIn(1f);
        SceneManager.LoadScene(startingCity.SceneName);
    }
}
