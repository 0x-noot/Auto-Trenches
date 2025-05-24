using System;
using System.Threading.Tasks;
using Solana.Unity.Wallet;
using Solana.Unity.SDK;
using Solana.Unity.Soar.Accounts;
using Solana.Unity.Soar.Program;
using Solana.Unity.Soar.Types;
using Solana.Unity.Soar;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Programs;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WalletManager : MonoBehaviour
{
    public static WalletManager Instance { get; private set; }
    
    [SerializeField] private SoarManager soarManager;
    
    // Cache the wallet state
    private bool _isConnected = false;
    private string _cachedPublicKey = "";
    private Account _cachedAccount = null;
    
    public bool IsConnected 
    { 
        get 
        { 
            // First check our cached state
            if (_isConnected && !string.IsNullOrEmpty(_cachedPublicKey))
            {
                // Verify Web3 is still valid
                if (Web3.Instance != null && Web3.Wallet?.Account != null)
                {
                    return true;
                }
                else
                {
                    Debug.LogWarning("[WalletManager] Cached connection state is true but Web3 is invalid. Resetting.");
                    _isConnected = false;
                    _cachedPublicKey = "";
                    _cachedAccount = null;
                    return false;
                }
            }
            
            // Fall back to direct check
            bool basicCheck = Web3.Instance != null && Web3.Wallet?.Account != null;
            if (!basicCheck) return false;
            
            return ValidateConnection();
        } 
    }
    
    public string WalletPublicKey 
    {
        get
        {
            // Return cached key if available
            if (!string.IsNullOrEmpty(_cachedPublicKey))
                return _cachedPublicKey;
                
            // Otherwise get from Web3
            return Web3.Wallet?.Account?.PublicKey.ToString();
        }
    }
    
    public event Action<string> OnWalletConnected;
    public event Action OnWalletDisconnected;
    public event Action<string> OnConnectionError;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Subscribe to scene changes to maintain state
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            if (Web3.Instance == null)
            {
                Debug.LogError("[WalletManager] Web3 instance is not initialized. Ensure a Web3 component is present in the scene with wallet adapter settings configured.");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (Web3.Instance != null)
        {
            Web3.OnLogin += HandleWalletConnected;
            Web3.OnLogout += HandleWalletDisconnected;
        }
        
        if (soarManager == null)
        {
            soarManager = FindFirstObjectByType<SoarManager>();
        }
        
        // Try to restore connection state from PlayerPrefs
        RestoreConnectionState();
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        if (Web3.Instance != null)
        {
            Web3.OnLogin -= HandleWalletConnected;
            Web3.OnLogout -= HandleWalletDisconnected;
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[WalletManager] Scene loaded: {scene.name}, IsConnected: {IsConnected}");
        
        // Ensure Web3 persists across scenes
        if (_isConnected && !string.IsNullOrEmpty(_cachedPublicKey))
        {
            // Verify Web3 is still valid after scene load
            StartCoroutine(VerifyConnectionAfterSceneLoad());
        }
    }
    
    private System.Collections.IEnumerator VerifyConnectionAfterSceneLoad()
    {
        // Wait a frame for scene to fully load
        yield return null;
        
        if (Web3.Instance == null)
        {
            Debug.LogError("[WalletManager] Web3 instance lost after scene load!");
            
            // Try to find Web3 in the new scene
            var web3 = FindFirstObjectByType<Web3>();
            if (web3 == null)
            {
                Debug.LogError("[WalletManager] No Web3 found in new scene!");
                _isConnected = false;
                _cachedPublicKey = "";
                _cachedAccount = null;
            }
        }
        else if (Web3.Wallet?.Account == null && _cachedAccount != null)
        {
            Debug.Log("[WalletManager] Attempting to restore wallet connection...");
            // Try to restore the connection
            yield return RestoreWalletConnection();
        }
    }
    
    private void RestoreConnectionState()
    {
        // Check if we have a cached wallet address
        string lastWallet = PlayerPrefs.GetString("LastWalletAddress", "");
        bool keepConnected = PlayerPrefs.GetInt("KeepWalletConnected", 0) == 1;
        
        if (!string.IsNullOrEmpty(lastWallet) && keepConnected)
        {
            _cachedPublicKey = lastWallet;
            Debug.Log($"[WalletManager] Found cached wallet address: {lastWallet}");
        }
    }
    
    private async System.Threading.Tasks.Task<bool> RestoreWalletConnection()
    {
        try
        {
            if (Web3.Instance == null || string.IsNullOrEmpty(_cachedPublicKey))
                return false;
                
            Debug.Log("[WalletManager] Attempting to restore wallet connection...");
            
            // Try to reconnect
            var account = await Web3.Instance.LoginWalletAdapter();
            if (account != null)
            {
                _isConnected = true;
                _cachedAccount = account;
                _cachedPublicKey = account.PublicKey.ToString();
                Debug.Log("[WalletManager] Wallet connection restored successfully!");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WalletManager] Failed to restore wallet connection: {ex.Message}");
        }
        
        return false;
    }
    
    public bool ValidateConnection()
    {
        try 
        {
            if (Web3.Instance == null || Web3.Wallet == null || Web3.Wallet.Account == null)
            {
                return false;
            }
            
            string publicKey = Web3.Wallet.Account.PublicKey.ToString();
            bool isValid = !string.IsNullOrEmpty(publicKey);
            
            // Update cache if valid
            if (isValid)
            {
                _cachedPublicKey = publicKey;
                _isConnected = true;
            }
            
            return isValid;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[WalletManager] ValidateConnection error: {ex.Message}");
            return false;
        }
    }
    
    public void ClearCachedData()
    {
        PlayerPrefs.SetInt("KeepWalletConnected", 0);
        PlayerPrefs.SetInt("ShowMainMenu", 0);
        PlayerPrefs.SetInt("ReturningFromGame", 0);
        PlayerPrefs.DeleteKey("LastWalletAddress");
        PlayerPrefs.Save();
        
        _isConnected = false;
        _cachedPublicKey = "";
        _cachedAccount = null;
    }
    
    public async Task<string> GetRegisteredUsername()
    {
        if (!IsConnected) return null;
        
        try
        {
            var playerAccountPda = SoarPda.PlayerPda(Web3.Wallet.Account.PublicKey);
            var accountInfo = await Web3.Rpc.GetAccountInfoAsync(playerAccountPda, Commitment.Confirmed);
            
            if (accountInfo?.Result?.Value?.Data != null && accountInfo.Result.Value.Data.Count > 0)
            {
                byte[] accountData = Convert.FromBase64String(accountInfo.Result.Value.Data[0]);
                
                if (accountData.Length > 8)
                {
                    try
                    {
                        var player = Player.Deserialize(accountData);
                        if (player != null && !string.IsNullOrEmpty(player.Username))
                        {
                            return player.Username;
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }
        catch (Exception ex)
        {
        }
        
        return null;
    }
    
    public async Task<string> GetUsernameForWallet(string walletAddress)
    {
        try
        {
            var publicKey = new PublicKey(walletAddress);
            var playerAccountPda = SoarPda.PlayerPda(publicKey);
            var accountInfo = await Web3.Rpc.GetAccountInfoAsync(playerAccountPda, Commitment.Confirmed);
            
            if (accountInfo?.Result?.Value?.Data != null && accountInfo.Result.Value.Data.Count > 0)
            {
                byte[] accountData = Convert.FromBase64String(accountInfo.Result.Value.Data[0]);
                
                if (accountData.Length > 8)
                {
                    try
                    {
                        var player = Player.Deserialize(accountData);
                        if (player != null && !string.IsNullOrEmpty(player.Username))
                        {
                            return player.Username;
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }
        catch (Exception ex)
        {
        }
        
        return null;
    }
    
    public string GetDisplayName()
    {
        if (!IsConnected) return "Not Connected";
        
        string cachedUsername = PlayerPrefs.GetString("PlayerUsername", "");
        if (!string.IsNullOrEmpty(cachedUsername))
        {
            return cachedUsername;
        }
        
        return GetFormattedWalletAddress();
    }
    
    public async Task<bool> ConnectWallet()
    {
        try
        {
            Debug.Log("[WalletManager] ConnectWallet called");
            
            if (Web3.Instance == null || Web3.Wallet == null)
            {
                throw new Exception("Web3 or Wallet is not initialized. Check Web3 component setup.");
            }
            
            if (IsConnected)
            {
                Debug.Log("[WalletManager] Already connected, triggering event");
                OnWalletConnected?.Invoke(WalletPublicKey);
                return true;
            }
            
            var account = await Web3.Instance.LoginWalletAdapter();
            if (account == null)
            {
                throw new Exception("Failed to connect wallet: No account returned.");
            }
            
            // Cache the connection info
            _isConnected = true;
            _cachedAccount = account;
            _cachedPublicKey = account.PublicKey.ToString();
            
            // Save to PlayerPrefs for persistence
            PlayerPrefs.SetString("LastWalletAddress", _cachedPublicKey);
            PlayerPrefs.SetInt("KeepWalletConnected", 1);
            PlayerPrefs.Save();
            
            Debug.Log($"[WalletManager] Wallet connected successfully: {_cachedPublicKey}");
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WalletManager] ConnectWallet error: {ex.Message}");
            OnConnectionError?.Invoke(ex.Message);
            return false;
        }
    }
    
    public void DisconnectWallet()
    {
        if (Web3.Instance != null)
        {
            Web3.Instance.Logout();
        }
        
        ClearCachedData();
    }
    
    public string GetFormattedWalletAddress()
    {
        if (!IsConnected) return "Not Connected";
        string address = WalletPublicKey;
        if (address.Length > 10)
        {
            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }
        return address;
    }
    
    private async void HandleWalletConnected(Account account)
    {
        Debug.Log($"[WalletManager] HandleWalletConnected: {account.PublicKey}");
        
        // Update cache
        _isConnected = true;
        _cachedAccount = account;
        _cachedPublicKey = account.PublicKey.ToString();
        
        OnWalletConnected?.Invoke(account.PublicKey.ToString());
        PlayerPrefs.SetString("LastWalletAddress", account.PublicKey.ToString());
        PlayerPrefs.SetInt("KeepWalletConnected", 1);
        
        string registeredUsername = await GetRegisteredUsername();
        if (!string.IsNullOrEmpty(registeredUsername))
        {
            PlayerPrefs.SetString("PlayerUsername", registeredUsername);
            PlayerPrefs.Save();
        }
        
        bool isRegistered = await CheckPlayerRegistration(account);
        
        if (!isRegistered)
        {
            if (soarManager != null)
            {
                soarManager.ShowUsernamePanel(account);
            }
            else
            {
                soarManager = FindFirstObjectByType<SoarManager>();
                if (soarManager != null)
                {
                    soarManager.ShowUsernamePanel(account);
                }
            }
        }
        else
        {
            MenuManager.Instance?.ShowMainMenu();
        }
    }
    
    private async Task<bool> CheckPlayerRegistration(Account account)
    {
        try
        {
            var playerAccountPda = SoarPda.PlayerPda(account.PublicKey);
            
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccountPda, Commitment.Confirmed);
            
            bool isRegistered = accountData.Result?.Value != null && 
                              accountData.Result.Value.Data != null &&
                              accountData.Result.Value.Data.Count > 0;
            
            return isRegistered;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    private void HandleWalletDisconnected()
    {
        Debug.Log("[WalletManager] HandleWalletDisconnected");
        ClearCachedData();
        OnWalletDisconnected?.Invoke();
    }
    
    // Add a method to force refresh connection state
    public async Task<bool> EnsureConnected()
    {
        Debug.Log($"[WalletManager] EnsureConnected called. Current state: {IsConnected}");
        
        if (IsConnected)
        {
            // Validate the connection is still good
            if (ValidateConnection())
            {
                return true;
            }
        }
        
        // Try to reconnect if we have cached credentials
        if (!string.IsNullOrEmpty(_cachedPublicKey))
        {
            return await RestoreWalletConnection();
        }
        
        return false;
    }
}