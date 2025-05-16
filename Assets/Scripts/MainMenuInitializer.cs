using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuInitializer : MonoBehaviour
{
    [SerializeField] private GameObject walletPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject usernamePanel;
    
    [Tooltip("Time to wait before fixing menu state")]
    [SerializeField] private float initialDelay = 0.5f;
    
    private void Awake()
    {
        if (usernamePanel == null)
        {
            usernamePanel = GameObject.Find("UsernamePanel");
            if (usernamePanel != null)
            {
                Debug.Log("MainMenuInitializer: Found UsernamePanel by name in Awake");
            }
        }
    }
    
    private void Start()
    {
        // Delay before checking UI state to avoid conflicts with other initializers
        StartCoroutine(CheckMenuStateAfterDelay());
    }
    
    private IEnumerator CheckMenuStateAfterDelay()
    {
        // Wait to ensure all managers have initialized
        yield return new WaitForSeconds(initialDelay);
        
        // Find username panel if not assigned
        if (usernamePanel == null)
        {
            usernamePanel = GameObject.Find("UsernamePanel");
            if (usernamePanel != null)
            {
                Debug.Log("MainMenuInitializer: Found UsernamePanel by name");
            }
        }
        
        // Log UI state for debugging
        Debug.Log($"[MainMenuInitializer] Current UI state - WalletPanel: {(walletPanel ? walletPanel.activeSelf : false)}, MainMenuPanel: {(mainMenuPanel ? mainMenuPanel.activeSelf : false)}, UsernamePanel: {(usernamePanel ? usernamePanel.activeSelf : false)}");
        
        // Check if username panel is active - if so, don't change anything
        if (usernamePanel != null && usernamePanel.activeSelf)
        {
            Debug.Log("[MainMenuInitializer] Username panel is active, not modifying UI state");
            yield break; // Use yield break instead of return
        }
        
        // Check if we're returning from a game
        bool returningFromGame = PlayerPrefs.GetInt("ReturningFromGame", 0) == 1;
        
        // Check if wallet is connected
        bool walletConnected = (WalletManager.Instance != null && WalletManager.Instance.IsConnected);
        
        // Check if we should keep wallet connected from previous session
        bool keepWalletConnected = PlayerPrefs.GetInt("KeepWalletConnected", 0) == 1;
        
        // Check if player has a username saved (indicates registration)
        bool hasUsername = !string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerUsername", ""));
        
        Debug.Log($"[MainMenuInitializer] States - ReturningFromGame: {returningFromGame}, WalletConnected: {walletConnected}, KeepWalletConnected: {keepWalletConnected}, HasUsername: {hasUsername}");
        
        if (returningFromGame)
        {
            Debug.Log("[MainMenuInitializer] Detected return from game, forcing main menu to appear");
            
            // Reset the returning from game flag
            PlayerPrefs.SetInt("ReturningFromGame", 0);
            PlayerPrefs.Save();
            
            ForceShowMainMenu();
        }
        else if (walletConnected || (keepWalletConnected && hasUsername))
        {
            // If wallet is connected or we should keep it connected and user has a username
            Debug.Log("[MainMenuInitializer] Wallet is connected or should be kept connected, ensuring main menu is shown");
            ForceShowMainMenu();
        }
        else
        {
            // This is a fresh session without wallet connection
            Debug.Log("[MainMenuInitializer] Fresh session, no wallet connection - showing wallet panel");
            PlayerPrefs.SetInt("ShowMainMenu", 0);
            PlayerPrefs.SetInt("ReturningFromGame", 0);
            PlayerPrefs.Save();
            ForceShowWalletPanel();
        }
    }
    
    private void ForceShowMainMenu()
    {
        // Don't change state if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("[MainMenuInitializer] Username panel is active, not forcing main menu");
            return;
        }
        
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
    
    private void ForceShowWalletPanel()
    {
        // Don't change state if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("[MainMenuInitializer] Username panel is active, not forcing wallet panel");
            return;
        }
        
        // Hide main menu panel forcefully
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
            Debug.Log("[MainMenuInitializer] Forcefully disabled main menu panel");
        }
        
        // Show wallet panel forcefully
        if (walletPanel != null)
        {
            walletPanel.SetActive(true);
            Debug.Log("[MainMenuInitializer] Forcefully enabled wallet panel");
        }
        
        // Also try to use MenuManager if available
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ShowWalletPanel();
            Debug.Log("[MainMenuInitializer] Called MenuManager.ShowWalletPanel()");
        }
    }
}