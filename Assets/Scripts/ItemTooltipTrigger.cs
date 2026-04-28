using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Add public references to the buttons
    public Button buttonPlus;
    public Button buttonMinus;

    private InventoryItemUI parentUI;
    private GraphicRaycaster graphicRaycaster;
    private PointerEventData pointerEventData;

    private void Awake()
    {
        parentUI = GetComponentInParent<InventoryItemUI>();
        // Get the GraphicRaycaster from the main canvas to perform our own checks
        graphicRaycaster = GetComponentInParent<Canvas>().GetComponent<GraphicRaycaster>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Create a list to hold the results of our manual check
        List<RaycastResult> results = new List<RaycastResult>();

        // Manually cast a ray at the current mouse position
        graphicRaycaster.Raycast(eventData, results);

        // If the ray hit something...
        if (results.Count > 0)
        {
            // Get the GameObject that is at the very top layer
            GameObject topHit = results[0].gameObject;

            // **THE CHECK**: If the top object is NOT the plus button and NOT the minus button...
            if (topHit != buttonPlus.gameObject && topHit != buttonMinus.gameObject)
            {
                // ...then it's safe to show the tooltip.
                parentUI.ShowItemTooltip();
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Hiding the tooltip is always safe
        parentUI.HideItemTooltip();
    }
}