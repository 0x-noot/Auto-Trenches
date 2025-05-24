using UnityEngine;

public class PersistentManagers : MonoBehaviour
{
    public static PersistentManagers Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[PersistentManagers] Initialized and set to DontDestroyOnLoad");
        }
        else if (Instance != this)
        {
            Debug.Log("[PersistentManagers] Instance already exists, destroying duplicate");
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        Debug.Log("[PersistentManagers] OnEnable called");
        // Log the children for debugging
        for (int i = 0; i < transform.childCount; i++)
        {
            Debug.Log($"[PersistentManagers] Child {i}: {transform.GetChild(i).name}");
        }
    }
}