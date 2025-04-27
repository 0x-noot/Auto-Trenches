using System;
using System.Threading.Tasks;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

public class WalletManager : MonoBehaviour
{
    public static WalletManager Instance { get; private set; }
    
    [SerializeField] private SolanaWalletAdapterOptions walletAdapterOptions;
    
    public bool IsConnected => Web3.Account != null;
    public string WalletPublicKey => Web3.Account?.PublicKey;
    
    // Events
    public event Action<string> OnWalletConnected;
    public event Action OnWalletDisconnected;
    public event Action<string> OnConnectionError;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Subscribe to Web3 SDK events
        Web3.OnLogin += HandleWalletConnected;
        Web3.OnLogout += HandleWalletDisconnected;
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        Web3.OnLogin -= HandleWalletConnected;
        Web3.OnLogout -= HandleWalletDisconnected;
    }
    
    public async Task<bool> ConnectWallet()
    {
        try
        {
            // For WebGL, use wallet adapter (for external wallet apps)
            var account = await Web3.Instance.LoginWalletAdapter();
            return account != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error connecting wallet: {ex.Message}");
            OnConnectionError?.Invoke(ex.Message);
            return false;
        }
    }
    
    public void DisconnectWallet()
    {
        Web3.Instance.Logout();
    }
    
    // Format wallet address for display (e.g., "Addr...xyz")
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
    
    private void HandleWalletConnected(Account account)
    {
        Debug.Log($"Wallet connected: {account.PublicKey}");
        OnWalletConnected?.Invoke(account.PublicKey);
        
        // Save wallet address (optional, just for quick reconnect display)
        PlayerPrefs.SetString("LastWalletAddress", account.PublicKey);
        PlayerPrefs.Save();
    }
    
    private void HandleWalletDisconnected()
    {
        Debug.Log("Wallet disconnected");
        OnWalletDisconnected?.Invoke();
    }
}
