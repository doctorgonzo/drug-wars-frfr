using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Auto-spawned overlay UI for contract banners. Lives on its own canvas at top-center,
// independent of the dealer panel's layout. DealerClicks calls ShowFor(dealer) when a
// dealer panel opens and HideIfFor(dealer) when it closes — letting one shared banner
// service every dealer in every city.
public class ContractBannerUI : MonoBehaviour
{
    public static ContractBannerUI Instance { get; private set; }

    private Canvas _canvas;
    private GameObject _panel;
    private TMP_Text _titleText;
    private TMP_Text _detailText;
    private TMP_Text _paymentText;
    private TMP_Text _advanceText;
    private Button _acceptBtn;
    private Button _declineBtn;
    private Button _deliverBtn;
    private bool _built;
    private bool _subscribed;

    // The dealer this banner is currently displaying for (null = hidden).
    private Dealer _trackedDealer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("ContractBannerUI");
        go.AddComponent<ContractBannerUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unsubscribe();
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        Hide();
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (ContractManager.Instance != null)
            ContractManager.Instance.OnContractsChanged += Refresh;
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged += Refresh;
        if (ContractManager.Instance != null || PlayerStats.Instance != null)
            _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (ContractManager.Instance != null)
            ContractManager.Instance.OnContractsChanged -= Refresh;
        if (PlayerStats.Instance != null)
            PlayerStats.Instance.OnInventoryChanged -= Refresh;
        _subscribed = false;
    }

    private void Update()
    {
        if (!_subscribed) TrySubscribe();
    }

    // ----- Public API -----

    public void ShowFor(Dealer dealer)
    {
        _trackedDealer = dealer;
        if (!_built) BuildUI();
        Refresh();
    }

    public void HideIfFor(Dealer dealer)
    {
        if (_trackedDealer == dealer) Hide();
    }

    public void Hide()
    {
        _trackedDealer = null;
        if (_built) _panel.SetActive(false);
    }

    // ----- Refresh -----

    private void Refresh()
    {
        if (!_built || _trackedDealer == null) { if (_built) _panel.SetActive(false); return; }
        var cm = ContractManager.Instance;
        if (cm == null) { _panel.SetActive(false); return; }

        var offer = cm.GetOfferForDealer(_trackedDealer);
        var active = cm.GetActiveContractForDealer(_trackedDealer);
        if (offer == null && active == null) { _panel.SetActive(false); return; }

        _panel.SetActive(true);
        int currentDay = GameTime.Instance != null ? GameTime.Instance.Day : 1;

        if (offer != null)
        {
            int daysLeft = Mathf.Max(0, offer.deadlineDay - currentDay);
            int advance = Mathf.RoundToInt(offer.totalPayment * 0.5f);
            _titleText.text   = $"<color=#FFD700>JOB OFFER</color>  •  {_trackedDealer.Name}";
            _detailText.text  = $"{offer.drugName} × {offer.quantityRequired}     in {daysLeft}d";
            _paymentText.text = $"${offer.totalPayment:N0}";
            _advanceText.text = $"${advance:N0} advance";
            _acceptBtn.gameObject.SetActive(true);
            _declineBtn.gameObject.SetActive(true);
            _deliverBtn.gameObject.SetActive(false);
        }
        else
        {
            int daysLeft = active.deadlineDay - currentDay;
            var item = PlayerStats.Instance?.inventory
                .FirstOrDefault(i => i.Type == ItemType.Drug && i.Name == active.drugName);
            int playerHas = item != null ? item.Amount : 0;
            bool canDeliver = playerHas >= active.quantityRequired;
            string deadlineStr = daysLeft <= 0
                ? "<color=#FF4444>OVERDUE</color>"
                : $"{daysLeft}d left";
            string ownStr = canDeliver
                ? $"<color=#44FF44>{playerHas}/{active.quantityRequired} ready</color>"
                : $"have {playerHas}/{active.quantityRequired}";

            _titleText.text   = $"<color=#FFD700>DELIVER</color>  •  {_trackedDealer.Name}";
            _detailText.text  = $"{active.drugName} × {active.quantityRequired}     {deadlineStr}";
            _paymentText.text = $"${active.RemainingPayment:N0}";
            _advanceText.text = ownStr;
            _acceptBtn.gameObject.SetActive(false);
            _declineBtn.gameObject.SetActive(false);
            _deliverBtn.gameObject.SetActive(true);
            _deliverBtn.interactable = canDeliver;
        }
    }

    // ----- Click handlers -----

    private void OnAccept()
    {
        if (_trackedDealer == null) return;
        if (ContractManager.Instance != null && ContractManager.Instance.AcceptOffer(_trackedDealer))
            Refresh();
    }

    private void OnDecline()
    {
        if (_trackedDealer == null) return;
        ContractManager.Instance?.DeclineOffer(_trackedDealer);
        Refresh();
    }

    private void OnDeliver()
    {
        if (_trackedDealer == null) return;
        if (ContractManager.Instance != null && ContractManager.Instance.TryDeliver(_trackedDealer))
            Refresh();
    }

    // ----- UI build -----

    private void BuildUI()
    {
        var canvasGO = new GameObject("ContractBannerCanvas");
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 4500;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // Panel: top-center of screen, fixed size
        _panel = new GameObject("ContractBannerPanel");
        _panel.transform.SetParent(canvasGO.transform, false);
        var rt = _panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -16);
        rt.sizeDelta = new Vector2(630, 330);

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.13f, 0.16f, 0.28f, 0.97f);

        var vlg = _panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 14, 14);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        _titleText   = AddRow(_panel.transform, "Title",   44, 24, FontStyles.Bold,    Color.white);
        _detailText  = AddRow(_panel.transform, "Detail",  38, 22, FontStyles.Normal,  Color.white);
        _paymentText = AddRow(_panel.transform, "Payment", 44, 30, FontStyles.Bold,    new Color(0.55f, 1f, 0.55f));
        _advanceText = AddRow(_panel.transform, "Advance", 32, 16, FontStyles.Italic,  new Color(0.85f, 0.85f, 0.7f));

        // Button row
        var btnRow = new GameObject("Buttons");
        btnRow.transform.SetParent(_panel.transform, false);
        btnRow.AddComponent<RectTransform>();
        var rowLE = btnRow.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 54;
        rowLE.minHeight = 54;
        var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.padding = new RectOffset(0, 0, 6, 0);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        _acceptBtn  = BuildButton(btnRow.transform, "ACCEPT",  new Color(0.20f, 0.55f, 0.20f), OnAccept);
        _declineBtn = BuildButton(btnRow.transform, "DECLINE", new Color(0.45f, 0.45f, 0.45f), OnDecline);
        _deliverBtn = BuildButton(btnRow.transform, "DELIVER", new Color(0.85f, 0.65f, 0.15f), OnDeliver);

        _panel.SetActive(false);
        _built = true;
    }

    private TMP_Text AddRow(Transform parent, string name, int height, int fontSize, FontStyles style, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = "";
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.fontStyle = style;
        t.fontSize = fontSize;
        t.enableAutoSizing = false;
        t.enableWordWrapping = false;
        t.richText = true;
        return t;
    }

    private Button BuildButton(Transform parent, string label, Color color, System.Action onClick)
    {
        var go = new GameObject($"Btn_{label}");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(color.r * 1.2f, color.g * 1.2f, color.b * 1.2f, 1f);
        colors.pressedColor     = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var labelGO = new GameObject("Text");
        labelGO.transform.SetParent(go.transform, false);
        var lr = labelGO.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.sizeDelta = Vector2.zero;
        var t = labelGO.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
        t.fontSize = 22;
        return btn;
    }
}
