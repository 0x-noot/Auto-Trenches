using System;
using System.Threading.Tasks;
using Solana.Unity.Wallet;
using Solana.Unity.SDK;
using Solana.Unity.Soar;
using Solana.Unity.Soar.Accounts;
using Solana.Unity.Soar.Program;
using Solana.Unity.Soar.Types;
using Solana.Unity.Rpc.Types;
using UnityEngine;

public class WalletManager : MonoBehaviour
{
    public static WalletManager Instance { get; private set; }
    
    [SerializeField] private SoarManager soarManager;
    
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
            if (Web3.Instance == null || Web3.Wallet == null)
            {
                throw new Exception("Web3 or Wallet is not initialized. Check Web3 component setup.");
            }
            var account = await Web3.Instance.LoginWalletAdapter();
            if (account == null)
            {
                throw new Exception("Failed to connect wallet: No account returned.");
            }
            return true;
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
        Debug.Log($"Wallet connected: {account.PublicKey}");
        OnWalletConnected?.Invoke(account.PublicKey.ToString());
        PlayerPrefs.SetString("LastWalletAddress", account.PublicKey.ToString());
        PlayerPrefs.Save();

        bool isRegistered = await CheckPlayerRegistration(account);
        
        if (!isRegistered)
        {
            soarManager.ShowUsernamePanel(account);
        }
        else
        {
            Debug.Log("Player already registered, skipping username panel");
        }
    }
    
    private async Task<bool> CheckPlayerRegistration(Account account)
    {
        try
        {
            var playerAccountPda = SoarPda.PlayerPda(account.PublicKey);
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccountPda, Commitment.Confirmed);
            
            if (accountData.Result?.Value == null || accountData.Result.Value.Data?.Count == 0)
            {
                Debug.Log("Player account doesn't exist");
                return false;
            }
            
            var gameId = new PublicKey("HLnBwVAc2dNJPLyG81bZkQbEkg1qDB6W8r2gZhq4b7FC");
            var playerScoresAccount = SoarPda.PlayerScoresPda(account.PublicKey, gameId);
            var gameAccountData = await Web3.Rpc.GetAccountInfoAsync(playerScoresAccount, Commitment.Confirmed);
            
            bool isFullyRegistered = gameAccountData.Result?.Value != null && 
                                    gameAccountData.Result.Value.Data?.Count > 0;
            
            Debug.Log($"Player registration check - Player account: exists, Game registration: {(isFullyRegistered ? "exists" : "missing")}");
            return isFullyRegistered;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking player registration: {ex.Message}");
            return false;
        }
    }
    
    private void HandleWalletDisconnected()
    {
        Debug.Log("Wallet disconnected");
        OnWalletDisconnected?.Invoke();
    }
}