using UnityEngine;

public class Unit : MonoBehaviour
{
    private bool isDragging = false;
    private Vector3 originalPosition;
    private TileDetector tileDetector;
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
            // Get mouse position in world space
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePosition.z = 0;
            transform.position = mousePosition;
        }
    }

    private void OnMouseUp()
    {
        isDragging = false;
        
        // Get current position
        Vector3 currentPosition = transform.position;
        
        // Check if it's a valid placement
        if (placementSystem.IsValidPosition(transform.position))
        {
            // Snap to nearest valid position
            transform.position = placementSystem.GetNearestValidPosition(transform.position);
        }
        else
        {
            // Return to original position
            transform.position = originalPosition;
        }
    }
}