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
using TMPro;

public class SoarManager : MonoBehaviour
{
    [SerializeField] private GameObject usernamePanel;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private UnityEngine.UI.Button submitButton;

    private PublicKey gameId = new PublicKey("HLnBwVAc2dNJPLyG81bZkQbEkg1qDB6W8r2gZhq4b7FC");
    private PublicKey leaderboardPda = new PublicKey("3nVK66juaCJ7p2AzqGzjhkkwSHjXokPPSPrJqSZY19ge");
    private Account currentAccount;

    public static SoarManager Instance { get; private set; }
    
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
        
        usernamePanel.SetActive(false);
    }

    public void ShowUsernamePanel(Account account)
    {
        currentAccount = account;
        usernamePanel.SetActive(true);
        usernameInput.text = "";
        statusText.text = "Enter your username";
        submitButton.interactable = true;
    }

    public async void OnSubmitUsername()
    {
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

        submitButton.interactable = false;
        statusText.text = "Checking registration...";
        
        // Double-check if already registered before attempting registration
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
            Debug.Log("Starting player registration...");

            var playerAccount = SoarPda.PlayerPda(account.PublicKey);
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccount, Commitment.Confirmed);
            
            if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
            {
                statusText.text = "Already registered!";
                submitButton.interactable = true;
                Debug.Log("Player already registered - aborting registration");
                
                await Task.Delay(1500);
                usernamePanel.SetActive(false);
                return;
            }

            // Get fresh blockhash
            var blockHash = await Web3.Rpc.GetLatestBlockHashAsync();
            if (blockHash == null || blockHash.Result == null)
            {
                throw new Exception("Failed to get block hash");
            }

            var tx = new Transaction()
            {
                FeePayer = account.PublicKey,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = blockHash.Result.Value.Blockhash // Use the fresh blockhash
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
            
            Debug.Log("Signing and sending transaction...");
            var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
            
            if (!result.WasSuccessful)
            {
                throw new Exception($"Transaction failed: {result.Reason}");
            }
            
            string txSignature = result.Result;
            Debug.Log($"Transaction sent, signature: {txSignature}");

            Debug.Log("Confirming transaction...");
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
                Debug.Log("Player registration confirmed");
                
                // Navigate to main menu after registration
                MenuManager.Instance?.ShowMainMenu();
            }
            else
            {
                statusText.text = "Registration failed. Try again.";
                submitButton.interactable = true;
                Debug.Log("Transaction confirmation failed");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error registering player: {ex.Message}");
            statusText.text = $"Error: {ex.Message}";
            submitButton.interactable = true;
        }
    }

    public async Task<bool> SubmitScoreToLeaderboard(ulong score)
    {
        try
        {
            if (Web3.Wallet == null || Web3.Wallet.Account == null)
            {
                Debug.LogError("Wallet not connected");
                return false;
            }

            var playerAddress = Web3.Wallet.Account.PublicKey;
            var playerAccountPda = SoarPda.PlayerPda(playerAddress);
            
            var tx = new Transaction()
            {
                FeePayer = playerAddress,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = await Web3.BlockHash()
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

            // Send transaction
            var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
            
            if (!result.WasSuccessful)
            {
                Debug.LogError($"Failed to submit score: {result.Reason}");
                return false;
            }

            Debug.Log($"Score submitted successfully. Transaction: {result.Result}");
            
            // Wait for confirmation
            await Web3.Rpc.ConfirmTransaction(result.Result, Commitment.Confirmed);
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error submitting score: {ex.Message}");
            return false;
        }
    }

    // Method to fetch current score
    public async Task<ulong> GetPlayerScore(PublicKey playerPublicKey)
    {
        try
        {
            var playerScoresPda = SoarPda.PlayerScoresPda(SoarPda.PlayerPda(playerPublicKey), leaderboardPda);
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerScoresPda, Commitment.Confirmed);
            
            if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
            {
                // Note: You'll need to deserialize the account data to get the actual score
                // This is a placeholder - the actual implementation depends on your SOAR version
                Debug.Log("Player scores account found, but deserialization not implemented");
                return 1200; // Default ELO
            }
            else
            {
                Debug.Log("No scores found for player");
                return 1200; // Default ELO
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error fetching player score: {ex.Message}");
            return 1200; // Default ELO
        }
    }
}