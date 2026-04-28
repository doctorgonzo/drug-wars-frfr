using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TabbedPanel : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public string label;
        public Button tabButton;
        public GameObject contentPanel;
    }

    [Header("Tabs")]
    [SerializeField] private Tab[] tabs;

    [Header("Colors")]
    [SerializeField] private Color activeTabColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Color inactiveTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private int activeIndex = 0;

    private void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i; // capture for closure
            tabs[i].tabButton.onClick.AddListener(() => SetActiveTab(index));

            // Set label if the button has a TMP_Text child
            var label = tabs[i].tabButton.GetComponentInChildren<TMP_Text>();
            if (label != null && !string.IsNullOrEmpty(tabs[i].label))
                label.text = tabs[i].label;
        }

        SetActiveTab(0);
    }

    public void SetActiveTab(int index)
    {
        if (index < 0 || index >= tabs.Length) return;
        activeIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = i == activeIndex;
            tabs[i].contentPanel.SetActive(isActive);

            // Tint the tab button
            var colors = tabs[i].tabButton.colors;
            colors.normalColor = isActive ? activeTabColor : inactiveTabColor;
            colors.highlightedColor = isActive ? activeTabColor : inactiveTabColor;
            tabs[i].tabButton.colors = colors;

            // Tint the tab label
            var label = tabs[i].tabButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = isActive ? activeTextColor : inactiveTextColor;
        }
    }

    public void NextTab()
    {
        SetActiveTab((activeIndex + 1) % tabs.Length);
    }

    public void PreviousTab()
    {
        SetActiveTab((activeIndex - 1 + tabs.Length) % tabs.Length);
    }
}
