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
    [SerializeField] private GameObject usernamePanel; // UI panel for username input
    [SerializeField] private TMP_InputField usernameInput; // Input field for username
    [SerializeField] private TextMeshProUGUI statusText; // Text for success/error messages
    [SerializeField] private UnityEngine.UI.Button submitButton; // Submit button

    private PublicKey gameId = new PublicKey("HLnBwVAc2dNJPLyG81bZkQbEkg1qDB6W8r2gZhq4b7FC");
    private Account currentAccount;

    private void Awake()
    {
        // Hide panel by default
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

    // Called by Submit button's OnClick event
    public async void OnSubmitUsername()
    {
        string username = usernameInput.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            statusText.text = "Username cannot be empty";
            return;
        }
        if (username.Length > 32) // Placeholder max length
        {
            statusText.text = "Username too long (max 32 characters)";
            return;
        }

        submitButton.interactable = false;
        statusText.text = "Registering...";
        await RegisterPlayer(currentAccount, username);
    }

    private async Task RegisterPlayer(Account account, string username)
    {
        try
        {
            Debug.Log("Starting player registration...");

            // Double-check if player is already registered before attempting registration
            var playerAccount = SoarPda.PlayerPda(account.PublicKey);
            var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccount, Commitment.Confirmed);
            
            if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
            {
                statusText.text = "Already registered!";
                submitButton.interactable = true;
                Debug.Log("Player already registered - aborting registration");
                
                // Hide the panel after a short delay
                await Task.Delay(1500);
                usernamePanel.SetActive(false);
                return;
            }

            var tx = new Transaction()
            {
                FeePayer = account.PublicKey,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = await Web3.BlockHash()
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
            
            // Sign and send transaction
            Debug.Log("Signing and sending transaction...");
            var result = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
            
            if (!result.WasSuccessful)
            {
                throw new Exception($"Transaction failed: {result.Reason}");
            }
            
            string txSignature = result.Result;
            Debug.Log($"Transaction sent, signature: {txSignature}");

            // Confirm transaction
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
                
                // You might want to trigger some event here for successful registration
                // For example, loading the player's profile or navigating to the main menu
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
}