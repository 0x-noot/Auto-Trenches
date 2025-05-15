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
            Debug.LogError("Wallet not connected");
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
            Debug.LogError($"Error loading profile: {ex.Message}");
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
                    profile.username = PlayerPrefs.GetString("PlayerUsername", "Player");
                    
                    await FetchLeaderboardScore(profile, playerPublicKey);
                    
                    profile.totalMatches = PlayerPrefs.GetInt(PREF_TOTAL_MATCHES, 0);
                    profile.wins = PlayerPrefs.GetInt(PREF_WINS, 0);
                    profile.losses = PlayerPrefs.GetInt(PREF_LOSSES, 0);
                    
                    Debug.Log($"Player profile loaded: ELO={profile.eloRating}, Matches={profile.totalMatches}, W/L={profile.wins}/{profile.losses}");
                }
                else
                {
                    Debug.LogWarning("Player not registered in SOAR");
                    profile.username = "Not Registered";
                    profile.eloRating = 1200;
                    profile.totalMatches = 0;
                    profile.wins = 0;
                    profile.losses = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error fetching player account: {ex.Message}");
                profile.username = PlayerPrefs.GetString("PlayerUsername", "Not Registered");
                profile.eloRating = 1200;
                profile.totalMatches = 0;
                profile.wins = 0;
                profile.losses = 0;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error fetching player profile: {ex.Message}");
            profile.username = "Error Loading";
            profile.eloRating = 1200;
            profile.totalMatches = 0;
            profile.wins = 0;
            profile.losses = 0;
        }
        
        return profile;
    }
    
    private async Task FetchLeaderboardScore(ProfileData profile, PublicKey playerPublicKey)
    {
        try
        {
            // Use SoarManager to get the player's score
            if (soarManager != null)
            {
                ulong score = await soarManager.GetPlayerScore(playerPublicKey);
                profile.eloRating = (int)score;
                Debug.Log($"Fetched player score: {score}");
            }
            else
            {
                Debug.LogWarning("SoarManager not available, using default ELO");
                profile.eloRating = 1200;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error fetching leaderboard score: {ex.Message}");
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
        
        Debug.Log($"Match recorded: Won={won}, Total={totalMatches}, W/L={wins}/{losses}");
    }
    
    public ProfileData GetCurrentProfile()
    {
        return currentProfile;
    }

    public async Task LoadProfileData()
    {
        if (!WalletManager.Instance.IsConnected)
        {
            Debug.LogError("Wallet not connected");
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
            Debug.LogError($"Error loading profile data: {ex.Message}");
        }
    }
    public void ResetStats()
    {
        PlayerPrefs.SetInt(PREF_TOTAL_MATCHES, 0);
        PlayerPrefs.SetInt(PREF_WINS, 0);
        PlayerPrefs.SetInt(PREF_LOSSES, 0);
        PlayerPrefs.Save();
        
        Debug.Log("Stats reset");
    }
}