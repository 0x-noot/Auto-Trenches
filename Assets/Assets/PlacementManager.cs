// Create PlacementManager.cs
using UnityEngine;
using System.Collections.Generic;

public class PlacementManager : MonoBehaviour
{
    [SerializeField] private GameObject unitPrefab;
    [SerializeField] private int maxUnits = 3;
    
    private List<Unit> placedUnits = new List<Unit>();

    public bool CanPlaceUnit()
    {
        return placedUnits.Count < maxUnits;
    }

    public void PlaceUnit(Vector3 position)
    {
        if (CanPlaceUnit())
        {
            GameObject unitObject = Instantiate(unitPrefab, position, Quaternion.identity);
            Unit unit = unitObject.GetComponent<Unit>();
            placedUnits.Add(unit);
        }
    }

    public void ClearUnits()
    {
        foreach (Unit unit in placedUnits)
        {
            Destroy(unit.gameObject);
        }
        placedUnits.Clear();
    }
}