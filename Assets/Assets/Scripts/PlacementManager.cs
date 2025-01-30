using UnityEngine;
using System.Collections.Generic;
using System;

public class PlacementManager : MonoBehaviour
{
    [System.Serializable]
    public class UnitPrefab
    {
        public string name;
        public GameObject prefab;
        public UnitType type;
    }

    [Header("Unit Settings")]
    [SerializeField] private List<UnitPrefab> unitPrefabs;
    [SerializeField] private int maxUnits = 3;
    [SerializeField] private Transform unitsParent;

    [Header("Current Selection")]
    [SerializeField] private UnitType selectedUnitType = UnitType.Fighter;

    private List<BaseUnit> placedUnits = new List<BaseUnit>();
    private GameManager gameManager;
    
    // Event for UI updates
    public event Action OnUnitsChanged;

    private void Start()
    {
        gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found in scene!");
        }

        if (unitsParent == null)
        {
            unitsParent = transform;
        }
    }

    public bool CanPlaceUnit()
    {
        return placedUnits.Count < maxUnits;
    }

    public void SelectUnitType(UnitType type)
    {
        selectedUnitType = type;
        Debug.Log($"Selected unit type: {type}");
    }

    public void PlaceUnit(Vector3 position)
    {
        if (!CanPlaceUnit())
        {
            Debug.Log("Maximum number of units reached!");
            return;
        }

        GameObject prefab = GetPrefabForType(selectedUnitType);
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for unit type: {selectedUnitType}");
            return;
        }

        GameObject unitObject = Instantiate(prefab, position, Quaternion.identity, unitsParent);
        BaseUnit unit = unitObject.GetComponent<BaseUnit>();
        
        if (unit == null)
        {
            Debug.LogError($"Prefab {prefab.name} does not have a BaseUnit component!");
            Destroy(unitObject);
            return;
        }

        placedUnits.Add(unit);
        gameManager?.RegisterPlayerUnit(unit);
        
        // Notify UI
        OnUnitsChanged?.Invoke();

        Debug.Log($"Placed {selectedUnitType} unit at {position}");
    }

    private GameObject GetPrefabForType(UnitType type)
    {
        UnitPrefab unitPrefab = unitPrefabs.Find(u => u.type == type);
        return unitPrefab?.prefab;
    }

    public void ClearUnits()
    {
        foreach (BaseUnit unit in placedUnits)
        {
            if (unit != null && unit.gameObject != null)
            {
                Destroy(unit.gameObject);
            }
        }
        placedUnits.Clear();
        OnUnitsChanged?.Invoke();
    }

    public int GetPlacedUnitsCount()
    {
        return placedUnits.Count;
    }

    public int GetMaxUnits()
    {
        return maxUnits;
    }

    public List<BaseUnit> GetPlacedUnits()
    {
        return placedUnits;
    }

    public bool IsPositionOccupied(Vector3 position, float threshold = 0.5f)
    {
        foreach (BaseUnit unit in placedUnits)
        {
            if (unit != null && Vector3.Distance(unit.transform.position, position) < threshold)
            {
                return true;
            }
        }
        return false;
    }
}