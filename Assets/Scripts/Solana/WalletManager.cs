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

public class WalletManager : MonoBehaviour
{
    public static WalletManager Instance { get; private set; }
    
    [SerializeField] private SoarManager soarManager;
    
    public bool IsConnected 
    { 
        get 
        { 
            bool basicCheck = Web3.Wallet?.Account != null;
            if (!basicCheck) return false;
            
            return ValidateConnection();
        } 
    }
    
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
        
        if (soarManager == null)
        {
            soarManager = FindFirstObjectByType<SoarManager>();
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
            
            return isValid;
        }
        catch (System.Exception ex)
        {
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
    }
    
    public async Task<bool> ConnectWallet()
    {
        try
        {
            if (Web3.Instance == null || Web3.Wallet == null)
            {
                throw new Exception("Web3 or Wallet is not initialized. Check Web3 component setup.");
            }
            
            if (IsConnected)
            {
                OnWalletConnected?.Invoke(WalletPublicKey);
                return true;
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
        OnWalletConnected?.Invoke(account.PublicKey.ToString());
        PlayerPrefs.SetString("LastWalletAddress", account.PublicKey.ToString());
        PlayerPrefs.Save();

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
        ClearCachedData();
        OnWalletDisconnected?.Invoke();
    }
}