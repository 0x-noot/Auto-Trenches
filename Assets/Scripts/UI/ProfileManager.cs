using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Soar.Program;
using Solana.Unity.Soar.Accounts;
using Solana.Unity.Soar;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Programs;

public class ProfileManager : MonoBehaviour
{
    public static ProfileManager Instance { get; private set; }
    
    [Header("References")]
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private SoarManager soarManager;
    
    [Header("SOAR Settings")]
    [SerializeField] private string gameId = "HLnBwVAc2dNJPLyG81bZkQbEkg1qDB6W8r2gZhq4b7FC";
    [SerializeField] private string leaderboardPublicKey = "3nVK66juaCJ7p2AzqGzjhkkwSHjXokPPSPrJqSZY19ge";
    
    private ProfileData currentProfile;
    
    public event Action<ProfileData> OnProfileDataLoaded;
    public event Action<string> OnProfileError;
    
    private const string PREF_TOTAL_MATCHES = "TotalMatches";
    private const string PREF_WINS = "Wins";
    private const string PREF_LOSSES = "Losses";
    
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
    
    public async void ShowProfile()
    {
        if (!WalletManager.Instance.IsConnected)
        {
            OnProfileError?.Invoke("Please connect your wallet first");
            return;
        }
        
        profileUI.ShowProfile(new ProfileData { username = "Loading..." });
        
        try
        {
            ProfileData profileData = await FetchPlayerProfile(Web3.Wallet.Account.PublicKey);
            currentProfile = profileData;
            
            profileUI.ShowProfile(profileData);
            OnProfileDataLoaded?.Invoke(profileData);
        }
        catch (Exception ex)
        {
            OnProfileError?.Invoke("Failed to load profile data");
            profileUI.HideProfile();
        }
    }
    
    private async Task<ProfileData> FetchPlayerProfile(PublicKey playerPublicKey)
    {
        ProfileData profile = new ProfileData();
        
        try
        {
            profile.walletAddress = playerPublicKey.ToString();
            
            var playerAccountPda = SoarPda.PlayerPda(playerPublicKey);
            
            try
            {
                var accountData = await Web3.Rpc.GetAccountInfoAsync(playerAccountPda, Commitment.Confirmed);
                
                if (accountData.Result?.Value != null && accountData.Result.Value.Data?.Count > 0)
                {
                    string registeredUsername = await GetUsernameFromSoarAccount(playerAccountPda);
                    
                    if (!string.IsNullOrEmpty(registeredUsername))
                    {
                        profile.username = registeredUsername;
                        PlayerPrefs.SetString("PlayerUsername", registeredUsername);
                        PlayerPrefs.Save();
                    }
                    else
                    {
                        profile.username = PlayerPrefs.GetString("PlayerUsername", "Player");
                    }
                    
                    await FetchLeaderboardScore(profile, playerPublicKey);
                    
                    profile.totalMatches = PlayerPrefs.GetInt(PREF_TOTAL_MATCHES, 0);
                    profile.wins = PlayerPrefs.GetInt(PREF_WINS, 0);
                    profile.losses = PlayerPrefs.GetInt(PREF_LOSSES, 0);
                }
                else
                {
                    profile.username = "Not Registered";
                    profile.eloRating = 1200;
                    profile.totalMatches = 0;
                    profile.wins = 0;
                    profile.losses = 0;
                }
            }
            catch (Exception ex)
            {
                profile.username = PlayerPrefs.GetString("PlayerUsername", "Not Registered");
                profile.eloRating = 1200;
                profile.totalMatches = 0;
                profile.wins = 0;
                profile.losses = 0;
            }
        }
        catch (Exception ex)
        {
            profile.username = "Error Loading";
            profile.eloRating = 1200;
            profile.totalMatches = 0;
            profile.wins = 0;
            profile.losses = 0;
        }
        
        return profile;
    }
    
    private async Task<string> GetUsernameFromSoarAccount(PublicKey playerAccountPda)
    {
        try
        {
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
                        // Fallback silently
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback silently
        }
        
        return null;
    }
    
    private async Task FetchLeaderboardScore(ProfileData profile, PublicKey playerPublicKey)
    {
        try
        {
            if (soarManager != null)
            {
                ulong score = await soarManager.GetPlayerScore(playerPublicKey);
                profile.eloRating = (int)score;
            }
            else
            {
                profile.eloRating = 1200;
            }
        }
        catch (Exception ex)
        {
            profile.eloRating = 1200;
        }
    }
    
    public void RecordMatch(bool won)
    {
        int totalMatches = PlayerPrefs.GetInt(PREF_TOTAL_MATCHES, 0) + 1;
        int wins = PlayerPrefs.GetInt(PREF_WINS, 0);
        int losses = PlayerPrefs.GetInt(PREF_LOSSES, 0);
        
        if (won)
        {
            wins++;
        }
        else
        {
            losses++;
        }
        
        PlayerPrefs.SetInt(PREF_TOTAL_MATCHES, totalMatches);
        PlayerPrefs.SetInt(PREF_WINS, wins);
        PlayerPrefs.SetInt(PREF_LOSSES, losses);
        PlayerPrefs.Save();
    }
    
    public ProfileData GetCurrentProfile()
    {
        return currentProfile;
    }

    public async Task LoadProfileData()
    {
        if (!WalletManager.Instance.IsConnected)
        {
            return;
        }
        
        try
        {
            ProfileData profileData = await FetchPlayerProfile(Web3.Wallet.Account.PublicKey);
            currentProfile = profileData;
            OnProfileDataLoaded?.Invoke(profileData);
        }
        catch (Exception ex)
        {
            // Handle silently
        }
    }
    
    public void ResetStats()
    {
        PlayerPrefs.SetInt(PREF_TOTAL_MATCHES, 0);
        PlayerPrefs.SetInt(PREF_WINS, 0);
        PlayerPrefs.SetInt(PREF_LOSSES, 0);
        PlayerPrefs.Save();
    }
}