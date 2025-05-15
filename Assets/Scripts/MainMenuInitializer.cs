using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using System.Collections;

public class MainMenuInitializer : MonoBehaviour
{
    [SerializeField] private GameObject walletPanel;
    [SerializeField] private GameObject mainMenuPanel;
    
    [Tooltip("Time to wait before forcing main menu to appear")]
    [SerializeField] private float forceMainMenuDelay = 0.5f;
    
    private void Start()
    {
        // Delay before forcing UI state to avoid conflicts with other initializers
        StartCoroutine(ForceMainMenuAfterDelay());
    }
    
    private IEnumerator ForceMainMenuAfterDelay()
    {
        // Wait to ensure all managers have initialized
        yield return new WaitForSeconds(forceMainMenuDelay);
        
        // Log UI state for debugging
        Debug.Log($"[MainMenuInitializer] Current UI state - WalletPanel: {(walletPanel ? walletPanel.activeSelf : false)}, MainMenuPanel: {(mainMenuPanel ? mainMenuPanel.activeSelf : false)}");
        
        // First check if we're returning from a game
        bool returningFromGame = PlayerPrefs.GetInt("ReturningFromGame", 0) == 1;
        
        if (returningFromGame)
        {
            Debug.Log("[MainMenuInitializer] Detected return from game, forcing main menu to appear");
            
            // Reset the flag immediately
            PlayerPrefs.SetInt("ReturningFromGame", 0);
            PlayerPrefs.Save();
            
            ForceShowMainMenu();
        }
        else 
        {
            // If wallet is connected, force main menu
            if (WalletManager.Instance != null && WalletManager.Instance.IsConnected)
            {
                Debug.Log("[MainMenuInitializer] Wallet is connected, ensuring main menu is shown");
                ForceShowMainMenu();
            }
            else
            {
                Debug.Log("[MainMenuInitializer] No action taken - not returning from game and wallet not connected");
                
                // Check if the wallet panel is still showing despite being in a "keepWalletConnected" state
                if (PlayerPrefs.GetInt("KeepWalletConnected", 0) == 1 && walletPanel != null && walletPanel.activeSelf)
                {
                    Debug.Log("[MainMenuInitializer] Fixing UI state - KeepWalletConnected is true but wallet panel is showing");
                    ForceShowMainMenu();
                }
            }
        }
    }
    
    private void ForceShowMainMenu()
    {
        // Hide wallet panel forcefully
        if (walletPanel != null)
        {
            walletPanel.SetActive(false);
            Debug.Log("[MainMenuInitializer] Forcefully disabled wallet panel");
        }
        
        // Show main menu panel forcefully
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("[MainMenuInitializer] Forcefully enabled main menu panel");
        }
        
        // Also try to use MenuManager if available
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ShowMainMenu();
            Debug.Log("[MainMenuInitializer] Called MenuManager.ShowMainMenu()");
        }
    }
}