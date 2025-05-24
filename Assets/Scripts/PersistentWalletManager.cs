using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentWalletManager : MonoBehaviour
{
    private static PersistentWalletManager instance;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Subscribe to scene loading to verify persistence
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            Debug.Log("[PersistentWalletManager] Created and set to DontDestroyOnLoad");
            
            // Ensure all child managers also persist
            EnsureChildManagersPersist();
        }
        else
        {
            Debug.Log("[PersistentWalletManager] Duplicate found, destroying");
            Destroy(gameObject);
        }
    }
    
    private void EnsureChildManagersPersist()
    {
        // Log all children for debugging
        foreach (Transform child in transform)
        {
            Debug.Log($"[PersistentWalletManager] Child found: {child.name}");
            
            // Specifically check for WalletManager
            if (child.GetComponent<WalletManager>() != null)
            {
                Debug.Log("[PersistentWalletManager] WalletManager component found and will persist");
            }
            
            if (child.GetComponent<SoarManager>() != null)
            {
                Debug.Log("[PersistentWalletManager] SoarManager component found and will persist");
            }
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[PersistentWalletManager] Scene loaded: {scene.name}");
        
        // Verify WalletManager is still accessible
        if (WalletManager.Instance != null)
        {
            Debug.Log($"[PersistentWalletManager] WalletManager.Instance is valid in {scene.name}");
            Debug.Log($"[PersistentWalletManager] WalletManager IsConnected: {WalletManager.Instance.IsConnected}");
        }
        else
        {
            Debug.LogError($"[PersistentWalletManager] WalletManager.Instance is NULL in {scene.name}!");
            
            // Try to find it
            var walletManager = FindObjectOfType<WalletManager>();
            if (walletManager != null)
            {
                Debug.Log("[PersistentWalletManager] Found WalletManager in scene, but Instance is null!");
            }
            else
            {
                Debug.LogError("[PersistentWalletManager] No WalletManager found in scene at all!");
            }
        }
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}