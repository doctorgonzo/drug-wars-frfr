using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DealerManager : MonoBehaviour
{
    [Header("Dealer Spawning")]
    [Tooltip("The prefab that represents a single dealer on the map.")]
    [SerializeField] private GameObject dealerPrefab;
    [Tooltip("A list of empty GameObjects that mark the positions where dealers will be placed.")]
    [SerializeField] private List<Transform> dealerSpawnPoints;

    [Header("Scene UI References")]
    [Tooltip("Reference to the main UI handler for the city.")]
    [SerializeField] private CityUIHandler cityUIHandler;
    [Tooltip("The 'Content' object that is a child of your player inventory's ScrollRect.")]
    [SerializeField] private Transform playerInventoryContent;
    [Tooltip("The panel that displays the selected dealer's inventory.")]
    [SerializeField] private GameObject dealerInfoPanel;
    [Tooltip("The prefab for a single row in an inventory panel.")]
    [SerializeField] private GameObject inventoryItemPrefab;
    [Tooltip("The HeatManager object in your scene.")]
    [SerializeField] private HeatManager heatManager;
    [Tooltip("Optional TMP text for buy/sell feedback messages (e.g. 'No free slots!', 'Not enough cash!').")]
    [SerializeField] private TMP_Text statusText;

    void Start()
    {
        SpawnDealersForCurrentCity();
        StartCoroutine(ResetInventoryScroll());
    }

    private IEnumerator ResetInventoryScroll()
    {
        yield return null;
        var scrollRect = playerInventoryContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    void SpawnDealersForCurrentCity()
    {
        // Clear any old dealers that might already be at the spawn points
        foreach (Transform spawnPoint in dealerSpawnPoints)
        {
            foreach (Transform child in spawnPoint)
            {
                Destroy(child.gameObject);
            }
        }

        City currentCity = PlayerStats.Instance.CurrentCity;
        if (currentCity == null || currentCity.Dealers == null)
        {
            Debug.LogWarning("Current city is not set or has no dealers assigned.");
            return;
        }

        // Loop through the dealers assigned to this city and the spawn points
        for (int i = 0; i < currentCity.Dealers.Length; i++)
        {
            // Stop if we have more dealers than available spawn points
            if (i >= dealerSpawnPoints.Count)
            {
                Debug.LogWarning($"Not enough spawn points for all dealers in {currentCity.Name}. Dealer {currentCity.Dealers[i].Name} will not be spawned.");
                break;
            }

            Dealer dealerData = currentCity.Dealers[i];
            Transform spawnPoint = dealerSpawnPoints[i];
            //dealerData.InitializeRuntimeInventory();
            if (dealerData == null || spawnPoint == null) continue;

            // Create the dealer instance and parent it to its designated spawn point
            GameObject dealerInstance = Instantiate(dealerPrefab, spawnPoint);
            dealerInstance.name = dealerData.Name;

            // Set the dealer's image from the ScriptableObject data
            Image dealerImage = dealerInstance.GetComponent<Image>();
            if (dealerImage != null)
            {
                dealerImage.sprite = dealerData.Image;
            }

            // Get the script on the new instance and configure it with all necessary references
            DealerClicks dealerScript = dealerInstance.GetComponent<DealerClicks>();
            if (dealerScript != null)
            {
                dealerScript.SetupDealer(
                    dealerData,
                    cityUIHandler,
                    playerInventoryContent,
                    dealerInfoPanel,
                    inventoryItemPrefab,
                    heatManager,
                    statusText
                );
            }
        }
    }
}

