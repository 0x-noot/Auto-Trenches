using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MainMenuInitializer : MonoBehaviour
{
    [SerializeField] private GameObject walletPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject usernamePanel;
    
    [SerializeField] private float initialDelay = 0.5f;
    
    private void Awake()
    {
        if (usernamePanel == null)
        {
            usernamePanel = GameObject.Find("UsernamePanel");
        }
    }
    
    private void Start()
    {
        StartCoroutine(CheckMenuStateAfterDelay());
    }
    
    private IEnumerator CheckMenuStateAfterDelay()
    {
        yield return new WaitForSeconds(initialDelay);
        
        if (usernamePanel == null)
        {
            usernamePanel = GameObject.Find("UsernamePanel");
        }
        
        if (usernamePanel != null && usernamePanel.activeSelf)
        {
            yield break;
        }
        
        bool returningFromGame = PlayerPrefs.GetInt("ReturningFromGame", 0) == 1;
        bool keepWalletConnected = PlayerPrefs.GetInt("KeepWalletConnected", 0) == 1;
        bool hasUsername = !string.IsNullOrEmpty(PlayerPrefs.GetString("PlayerUsername", ""));
        
        bool walletActuallyConnected = (WalletManager.Instance != null && WalletManager.Instance.IsConnected);
        
        Debug.Log($"MainMenuInitializer - WalletActuallyConnected: {walletActuallyConnected}, ReturningFromGame: {returningFromGame}, KeepWalletConnected: {keepWalletConnected}, HasUsername: {hasUsername}");
        
        if (returningFromGame)
        {
            PlayerPrefs.SetInt("ReturningFromGame", 0);
            PlayerPrefs.Save();
            
            if (walletActuallyConnected)
            {
                ForceShowMainMenu();
            }
            else
            {
                PlayerPrefs.SetInt("KeepWalletConnected", 0);
                PlayerPrefs.SetInt("ShowMainMenu", 0);
                PlayerPrefs.Save();
                ForceShowWalletPanel();
            }
        }
        else if (walletActuallyConnected)
        {
            ForceShowMainMenu();
        }
        else if (keepWalletConnected && hasUsername)
        {
            Debug.Log("Attempting to reconnect wallet with cached credentials");
            yield return StartCoroutine(TryReconnectWallet());
        }
        else
        {
            PlayerPrefs.SetInt("ShowMainMenu", 0);
            PlayerPrefs.SetInt("ReturningFromGame", 0);
            PlayerPrefs.SetInt("KeepWalletConnected", 0);
            PlayerPrefs.Save();
            ForceShowWalletPanel();
        }
    }

    private IEnumerator TryReconnectWallet()
    {
        if (WalletManager.Instance != null)
        {
            ForceShowWalletPanel();
            
            yield return null;
            
            var connectTask = WalletManager.Instance.ConnectWallet();
            
            yield return new WaitUntil(() => connectTask.IsCompleted);
            
            bool connected = false;
            if (connectTask.IsFaulted)
            {
                Debug.LogError($"Error reconnecting wallet: {connectTask.Exception?.GetBaseException()?.Message}");
                connected = false;
            }
            else
            {
                connected = connectTask.Result;
            }
            
            if (connected)
            {
                ForceShowMainMenu();
            }
            else
            {
                PlayerPrefs.SetInt("KeepWalletConnected", 0);
                PlayerPrefs.SetInt("ShowMainMenu", 0);
                PlayerPrefs.Save();
                ForceShowWalletPanel();
            }
        }
        else
        {
            ForceShowWalletPanel();
        }
    }
    
    private void ForceShowMainMenu()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        if (walletPanel != null)
        {
            walletPanel.SetActive(false);
        }
        
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
        
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ShowMainMenu();
        }
    }
    
    private void ForceShowWalletPanel()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
        
        if (walletPanel != null)
        {
            walletPanel.SetActive(true);
        }
        
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ShowWalletPanel();
        }
    }
}