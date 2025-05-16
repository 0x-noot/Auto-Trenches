using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Soar.Accounts;
using Solana.Unity.Soar.Program;
using Solana.Unity.Soar.Types;
using Solana.Unity.Soar;
using UnityEngine;
using Photon.Pun;
using TMPro;

public class SoarManager : MonoBehaviour
{
    public static SoarManager Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private GameObject usernamePanel;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private UnityEngine.UI.Button submitButton;
    
    [Header("SOAR Settings")]
    [SerializeField] private string gameIdString = "HLnBwVAc2dNJPLyG81bZkQbEkg1qDB6W8r2gZhq4b7FC";
    [SerializeField] private string leaderboardPdaString = "3nVK66juaCJ7p2AzqGzjhkkwSHjXokPPSPrJqSZY19ge";
    
    private PublicKey gameId;
    private PublicKey leaderboardPda;
    private Account currentAccount;
    private int maxRetries = 3;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[SoarManager] Instance created");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        try {
            gameId = new PublicKey(gameIdString);
            leaderboardPda = new PublicKey(leaderboardPdaString);
            Debug.Log($"[SoarManager] Initialized with gameId: {gameId}, leaderboard: {leaderboardPda}");
        }
        catch (Exception ex) {
            Debug.LogError($"[SoarManager] Error initializing PublicKeys: {ex.Message}");
        }
        
        // Ensure panel is hidden by default
        if (usernamePanel != null)
        {
            usernamePanel.SetActive(false);
            Debug.Log("[SoarManager] Username panel hidden on startup");
        }
        else
        {
            Debug.LogError("[SoarManager] Username panel reference is missing!");
        }
    }

    public void ShowUsernamePanel(Account account)
    {
        Debug.Log($"[SoarManager] ShowUsernamePanel called for account: {account.PublicKey}");
        currentAccount = account;
        
        if (usernamePanel == null)
        {
            Debug.LogError("[SoarManager] Username panel is null! Cannot show panel");
            return;
        }
        
        if (usernameInput == null)
        {
            Debug.LogError("[SoarManager] Username input field is null!");
            return;
        }
        
        if (statusText == null)
        {
            Debug.LogError("[SoarManager] Status text is null!");
            return;
        }
        
        usernamePanel.SetActive(true);
        usernameInput.text = "";
        statusText.text = "Enter your username";
        
        if (submitButton != null)
        {
            submitButton.interactable = true;
        }
        
        // Make sure it's in front of other UI elements
        if (usernamePanel.transform.parent != null)
        {
            usernamePanel.transform.SetAsLastSibling();
        }
        
        Debug.Log("[SoarManager] Username panel activated");
    }

    public async void OnSubmitUsername()
    {
        Debug.Log("[SoarManager] OnSubmitUsername called");
        
        if (usernameInput == null)
        {
            Debug.LogError("[SoarManager] Username input is null!");
            return;
        }
        
        string username = usernameInput.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            statusText.text = "Username cannot be empty";
            return;
        }
        if (username.Length > 32)
        {
            statusText.text = "Username too long (max 32 characters)";
            return;
        }

        // Store the username in PlayerPrefs whether registration succeeds or not
        PlayerPrefs.SetString("PlayerUsername", username);
        PlayerPrefs.Save();

        submitButton.interactable = false;
        statusText.text = "Checking registration...";
        
        var playerAccount = SoarPda.PlayerPda(currentAccount.PublicKey);
        var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccount, Commitment.Confirmed);
        
        if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
        {
            statusText.text = "Already registered!";
            await Task.Delay(1500);
            usernamePanel.SetActive(false);
            MenuManager.Instance?.ShowMainMenu();
            return;
        }
        
        statusText.text = "Registering...";
        await RegisterPlayer(currentAccount, username);
    }

    private async Task RegisterPlayer(Account account, string username)
    {
        try
        {
            Debug.Log("[SoarManager] Starting player registration...");

            var playerAccount = SoarPda.PlayerPda(account.PublicKey);
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccount, Commitment.Confirmed);
            
            if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
            {
                statusText.text = "Already registered!";
                submitButton.interactable = true;
                Debug.Log("[SoarManager] Player already registered - aborting registration");
                
                await Task.Delay(1500);
                usernamePanel.SetActive(false);
                return;
            }

            var blockHash = await Web3.Rpc.GetLatestBlockHashAsync();
            if (blockHash == null || blockHash.Result == null)
            {
                throw new Exception("Failed to get block hash");
            }

            var tx = new Transaction()
            {
                FeePayer = account.PublicKey,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = blockHash.Result.Value.Blockhash
            };

            var accountsInitUser = new InitializePlayerAccounts()
            {
                Payer = account,
                User = account,
                PlayerAccount = playerAccount,
                SystemProgram = SystemProgram.ProgramIdKey
            };
            
            var initPlayerIx = SoarProgram.InitializePlayer(
                accounts: accountsInitUser,
                username: username,
                nftMeta: new PublicKey("BaxBPhbNxqR13QcYPvoTzE9LQZGs71Mu6euywyKHoprc"),
                programId: SoarProgram.ProgramIdKey
            );
            
            tx.Add(initPlayerIx);
            
            Debug.Log("[SoarManager] Signing and sending transaction...");
            var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
            
            if (!result.WasSuccessful)
            {
                throw new Exception($"Transaction failed: {result.Reason}");
            }
            
            string txSignature = result.Result;
            Debug.Log($"[SoarManager] Transaction sent, signature: {txSignature}");

            Debug.Log("[SoarManager] Confirming transaction...");
            var confirmTask = Web3.Rpc.ConfirmTransaction(txSignature, Commitment.Confirmed);
            var confirmResult = await Task.WhenAny(confirmTask, Task.Delay(TimeSpan.FromSeconds(30)));
            
            if (confirmResult != confirmTask)
            {
                throw new Exception("Transaction confirmation timed out after 30 seconds");
            }
            
            var confirmation = await confirmTask;
            if (confirmation)
            {
                statusText.text = "Registration successful!";
                usernamePanel.SetActive(false);
                Debug.Log("[SoarManager] Player registration confirmed");
                
                MenuManager.Instance?.ShowMainMenu();
            }
            else
            {
                statusText.text = "Registration failed. Try again.";
                submitButton.interactable = true;
                Debug.Log("[SoarManager] Transaction confirmation failed");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SoarManager] Error registering player: {ex.Message}");
            statusText.text = $"Error: {ex.Message}";
            submitButton.interactable = true;
        }
    }

    public async Task<bool> SubmitScoreToLeaderboard(ulong score)
    {
        int retryCount = 0;
        bool success = false;
        
        while (retryCount < maxRetries && !success)
        {
            try
            {
                if (Web3.Wallet == null || Web3.Wallet.Account == null)
                {
                    Debug.LogError("[SoarManager] Wallet not connected");
                    return false;
                }

                if (!WalletManager.Instance.IsConnected)
                {
                    Debug.Log("[SoarManager] Wallet disconnected. Attempting to reconnect...");
                    bool reconnected = await WalletManager.Instance.ConnectWallet();
                    if (!reconnected)
                    {
                        Debug.LogError("[SoarManager] Failed to reconnect wallet");
                        return false;
                    }
                }

                var playerAddress = Web3.Wallet.Account.PublicKey;
                var playerAccountPda = SoarPda.PlayerPda(playerAddress);
                
                var blockHashResult = await Web3.Rpc.GetLatestBlockHashAsync();
                if (blockHashResult == null || blockHashResult.Result == null)
                {
                    throw new Exception("Failed to get block hash");
                }
                
                var tx = new Transaction()
                {
                    FeePayer = playerAddress,
                    Instructions = new List<TransactionInstruction>(),
                    RecentBlockHash = blockHashResult.Result.Value.Blockhash
                };

                var accounts = new SubmitScoreAccounts()
                {
                    Payer = playerAddress,
                    Authority = playerAddress,
                    PlayerAccount = playerAccountPda,
                    Leaderboard = leaderboardPda,
                    PlayerScores = SoarPda.PlayerScoresPda(playerAccountPda, leaderboardPda),
                    SystemProgram = SystemProgram.ProgramIdKey
                };

                var submitScoreIx = SoarProgram.SubmitScore(
                    accounts: accounts,
                    score: score,
                    programId: SoarProgram.ProgramIdKey
                );

                tx.Add(submitScoreIx);

                Debug.Log("[SoarManager] About to sign and send transaction...");
                var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
                
                if (!result.WasSuccessful)
                {
                    Debug.LogError($"[SoarManager] Failed to submit score: {result.Reason}");
                    retryCount++;
                    await Task.Delay(500);
                    continue;
                }

                Debug.Log($"[SoarManager] Score transaction sent. Signature: {result.Result}");
                
                bool confirmed = await Web3.Rpc.ConfirmTransaction(result.Result, Commitment.Confirmed);
                
                if (confirmed)
                {
                    Debug.Log("[SoarManager] Score submission confirmed on-chain!");
                    if (ProfileManager.Instance != null && GameModeManager.Instance.CurrentMode == GameMode.Ranked)
                    {
                        bool playerWon = false;
                        if (BattleRoundManager.Instance != null)
                        {
                            if (PhotonNetwork.IsMasterClient)
                            {
                                playerWon = BattleRoundManager.Instance.GetPlayerAHP() > BattleRoundManager.Instance.GetPlayerBHP();
                            }
                            else
                            {
                                playerWon = BattleRoundManager.Instance.GetPlayerBHP() > BattleRoundManager.Instance.GetPlayerAHP();
                            }
                        }
                        ProfileManager.Instance.RecordMatch(playerWon);
                    }
                    success = true;
                    return true;
                }
                else
                {
                    Debug.LogWarning("[SoarManager] Transaction sent but not confirmed. Retrying...");
                    retryCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SoarManager] Error submitting score (attempt {retryCount+1}): {ex.Message}");
                retryCount++;
                await Task.Delay(500);
            }
        }
        
        Debug.LogError($"[SoarManager] Failed to submit score after {maxRetries} attempts");
        return false;
    }

    public async Task<ulong> GetPlayerScore(PublicKey playerPublicKey)
    {
        try
        {
            var playerScoresPda = SoarPda.PlayerScoresPda(SoarPda.PlayerPda(playerPublicKey), leaderboardPda);
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerScoresPda, Commitment.Confirmed);
            
            if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
            {
                Debug.Log("[SoarManager] Player scores account found, but deserialization not implemented");
                return 1200;
            }
            else
            {
                Debug.Log("[SoarManager] No scores found for player");
                return 1200;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SoarManager] Error fetching player score: {ex.Message}");
            return 1200;
        }
    }
}