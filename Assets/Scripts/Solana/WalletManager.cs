using System;
using System.Threading.Tasks;
using Solana.Unity.Wallet;
using Solana.Unity.SDK;
using Solana.Unity.Soar.Accounts;
using Solana.Unity.Soar.Program;
using Solana.Unity.Soar.Types;
using Solana.Unity.Soar;
using Solana.Unity.Rpc.Types;
using UnityEngine;

public class WalletManager : MonoBehaviour
{
    public static WalletManager Instance { get; private set; }
    
    [SerializeField] private SoarManager soarManager; // Reference to SoarManager
    
    public bool IsConnected => Web3.Wallet?.Account != null;
    public string WalletPublicKey => Web3.Wallet?.Account?.PublicKey.ToString();
    
    public event Action<string> OnWalletConnected;
    public event Action OnWalletDisconnected;
    public event Action<string> OnConnectionError;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // Ensure Web3 is initialized
            if (Web3.Instance == null)
            {
                Debug.LogError("Web3 instance is not initialized. Ensure a Web3 component is present in the scene with wallet adapter settings configured.");
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
        
        // Find SoarManager if not assigned
        if (soarManager == null)
        {
            soarManager = FindFirstObjectByType<SoarManager>();
            Debug.Log($"[WalletManager] Got SoarManager reference: {soarManager != null}");
        }
    }
    
    private void OnDestroy()
    {
        if (Web3.Instance != null)
        {
            Web3.OnLogin -= HandleWalletConnected;
            Web3.OnLogout -= HandleWalletDisconnected;
        }
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
                Debug.Log("[WalletManager] Already connected");
                OnWalletConnected?.Invoke(WalletPublicKey);
                return true;
            }
            
            var account = await Web3.Instance.LoginWalletAdapter();
            if (account == null)
            {
                throw new Exception("Failed to connect wallet: No account returned.");
            }
            
            Debug.Log($"[WalletManager] Successfully connected to wallet: {account.PublicKey}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WalletManager] Error connecting wallet: {ex.Message}");
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
        Debug.Log($"[WalletManager] Wallet connected: {account.PublicKey}");
        OnWalletConnected?.Invoke(account.PublicKey.ToString());
        PlayerPrefs.SetString("LastWalletAddress", account.PublicKey.ToString());
        PlayerPrefs.Save();

        // Check if player is already registered
        bool isRegistered = await CheckPlayerRegistration(account);
        
        if (!isRegistered)
        {
            Debug.Log("[WalletManager] Player NOT registered, showing username panel");
            // Show username panel only if not registered
            if (soarManager != null)
            {
                soarManager.ShowUsernamePanel(account);
            }
            else
            {
                Debug.LogError("[WalletManager] SoarManager reference is missing!");
                // Try to find it again
                soarManager = FindFirstObjectByType<SoarManager>();
                if (soarManager != null)
                {
                    Debug.Log("[WalletManager] Found SoarManager, showing username panel");
                    soarManager.ShowUsernamePanel(account);
                }
                else
                {
                    Debug.LogError("[WalletManager] Failed to find SoarManager!");
                }
            }
        }
        else
        {
            Debug.Log("[WalletManager] Player already registered, skipping username panel");
            // You might want to trigger some event or action here for registered players
            // For example, loading their profile or going directly to main menu
            MenuManager.Instance?.ShowMainMenu();
        }
    }
    
    private async Task<bool> CheckPlayerRegistration(Account account)
    {
        try
        {
            // Get the player PDA
            var playerAccountPda = SoarPda.PlayerPda(account.PublicKey);
            
            // Check if the account exists
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccountPda, Commitment.Confirmed);
            
            // If account exists and has data, player is registered
            bool isRegistered = accountData.Result?.Value != null && 
                              accountData.Result.Value.Data != null &&
                              accountData.Result.Value.Data.Count > 0;
            
            Debug.Log($"[WalletManager] Player registration check: {(isRegistered ? "Registered" : "Not registered")}");
            if (accountData.Result?.Value != null)
            {
                Debug.Log($"[WalletManager] Account data exists, Data length: {accountData.Result?.Value?.Data?.Count ?? 0}");
            }
            else
            {
                Debug.Log("[WalletManager] Account data is null - player not registered");
            }
            
            return isRegistered;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WalletManager] Error checking player registration: {ex.Message}");
            // If we can't check, assume they're not registered
            return false;
        }
    }
    
    private void HandleWalletDisconnected()
    {
        Debug.Log("[WalletManager] Wallet disconnected");
        OnWalletDisconnected?.Invoke();
    }
}