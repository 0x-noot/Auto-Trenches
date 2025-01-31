using UnityEngine;

public class Unit : MonoBehaviour
{
    private bool isDragging = false;
    private Vector3 originalPosition;
    private ValidPlacementSystem placementSystem;

    private void Start()
    {
        placementSystem = Object.FindFirstObjectByType<ValidPlacementSystem>();
    }

    private void OnMouseDown()
    {
        isDragging = true;
        originalPosition = transform.position;
    }

    private void OnMouseDrag()
    {
        if (isDragging)
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePosition.z = 0;
            transform.position = mousePosition;
        }
    }

    private void OnMouseUp()
    {
        isDragging = false;
        
        if (placementSystem.IsValidPosition(transform.position))
        {
            transform.position = placementSystem.GetNearestValidPosition(transform.position);
        }
        else
        {
            transform.position = originalPosition;
        }
    }
}