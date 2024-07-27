using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PagingScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public ScrollRect scrollRect; // Reference to the ScrollRect component
    public RectTransform contentPanel; // Reference to the content panel of the ScrollRect
    public float snapSpeed = 5f; // Speed at which the snapping occurs

    private bool isDragging = false;
    private Vector2 targetPosition;

    void Start()
    {
        // Ensure the ScrollRect component is assigned
        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
        }
    }

    void Update()
    {
        // If not dragging, lerp the position to the target position
        if (!isDragging)
        {
            contentPanel.anchoredPosition = Vector2.Lerp(contentPanel.anchoredPosition, targetPosition, Time.deltaTime * snapSpeed);
        }
    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        SnapToClosest();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // No additional logic needed during dragging
    }

    void SnapToClosest()
    {
        Debug.Log("Snapping to closest");
        // Find the closest child to the current position
        float closestDistance = float.MaxValue;
        RectTransform closestChild = null;

        foreach (RectTransform child in contentPanel)
        {
            float distance = Vector2.Distance(contentPanel.anchoredPosition, -child.anchoredPosition + new Vector2(child.rect.width / 2, 0));

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestChild = child;
            }
        }

        if (closestChild != null)
        {
            targetPosition = -closestChild.anchoredPosition + new Vector2(closestChild.rect.width / 2, 0);

            if (!scrollRect.horizontal)
            {
                targetPosition.x = contentPanel.anchoredPosition.x;
            }
            if (!scrollRect.vertical)
            {
                targetPosition.y = contentPanel.anchoredPosition.y;
            }
        }
    }
}
