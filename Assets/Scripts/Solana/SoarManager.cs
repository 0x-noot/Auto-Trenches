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
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        try {
            gameId = new PublicKey(gameIdString);
            leaderboardPda = new PublicKey(leaderboardPdaString);
        }
        catch (Exception ex) { }
        
        if (usernamePanel != null)
        {
            usernamePanel.SetActive(false);
        }
    }

    public void ShowUsernamePanel(Account account)
    {
        currentAccount = account;
        
        if (usernamePanel == null || usernameInput == null || statusText == null)
        {
            return;
        }
        
        usernamePanel.SetActive(true);
        usernameInput.text = "";
        statusText.text = "Enter your username";
        
        if (submitButton != null)
        {
            submitButton.interactable = true;
        }
        
        if (usernamePanel.transform.parent != null)
        {
            usernamePanel.transform.SetAsLastSibling();
        }
    }

    public async void OnSubmitUsername()
    {
        if (usernameInput == null)
        {
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
            var playerAccount = SoarPda.PlayerPda(account.PublicKey);
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccount, Commitment.Confirmed);
            
            if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
            {
                statusText.text = "Already registered!";
                submitButton.interactable = true;
                
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
            
            var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
            
            if (!result.WasSuccessful)
            {
                throw new Exception($"Transaction failed: {result.Reason}");
            }
            
            string txSignature = result.Result;

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
                
                MenuManager.Instance?.ShowMainMenu();
            }
            else
            {
                statusText.text = "Registration failed. Try again.";
                submitButton.interactable = true;
            }
        }
        catch (Exception ex)
        {
            statusText.text = $"Error: {ex.Message}";
            submitButton.interactable = true;
        }
    }

    public async Task<bool> SubmitScoreToLeaderboard(ulong score)
    {
        Debug.Log($"[SoarManager] SubmitScoreToLeaderboard called with score: {score}");
        int retryCount = 0;
        bool success = false;
        
        while (retryCount < maxRetries && !success)
        {
            try
            {
                if (Web3.Wallet == null || Web3.Wallet.Account == null)
                {
                    Debug.LogError("[SoarManager] Web3 Wallet or Account is null!");
                    return false;
                }

                if (!WalletManager.Instance.IsConnected)
                {
                    Debug.Log("[SoarManager] Wallet not connected, attempting to reconnect...");
                    bool reconnected = await WalletManager.Instance.ConnectWallet();
                    if (!reconnected)
                    {
                        Debug.LogError("[SoarManager] Failed to reconnect wallet!");
                        return false;
                    }
                }

                var playerAddress = Web3.Wallet.Account.PublicKey;
                var playerAccountPda = SoarPda.PlayerPda(playerAddress);
                
                Debug.Log($"[SoarManager] Player address: {playerAddress}");
                Debug.Log($"[SoarManager] Player account PDA: {playerAccountPda}");
                Debug.Log($"[SoarManager] Leaderboard PDA: {leaderboardPda}");
                
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

                Debug.Log("[SoarManager] Creating submit score instruction...");
                var submitScoreIx = SoarProgram.SubmitScore(
                    accounts: accounts,
                    score: score,
                    programId: SoarProgram.ProgramIdKey
                );

                tx.Add(submitScoreIx);

                Debug.Log("[SoarManager] Sending transaction to wallet for signing...");
                var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
                
                if (!result.WasSuccessful)
                {
                    Debug.LogError($"[SoarManager] Transaction failed: {result.Reason}");
                    retryCount++;
                    await Task.Delay(500);
                    continue;
                }
                
                Debug.Log($"[SoarManager] Transaction sent successfully! Signature: {result.Result}");
                Debug.Log("[SoarManager] Waiting for confirmation...");
                
                bool confirmed = await Web3.Rpc.ConfirmTransaction(result.Result, Commitment.Confirmed);
                
                if (confirmed)
                {
                    Debug.Log("[SoarManager] Transaction confirmed successfully!");
                    
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
                    Debug.LogError("[SoarManager] Transaction confirmation failed!");
                    retryCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SoarManager] Exception during score submission (attempt {retryCount + 1}): {ex.Message}");
                Debug.LogError($"[SoarManager] Stack trace: {ex.StackTrace}");
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
                return 1200;
            }
            else
            {
                return 1200;
            }
        }
        catch (Exception ex)
        {
            return 1200;
        }
    }
}