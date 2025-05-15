using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ProfileUI : MonoBehaviour
{
    [Header("Profile Panel")]
    [SerializeField] private GameObject profilePanel;
    
    [Header("Profile Information")]
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI walletAddressText;
    [SerializeField] private TextMeshProUGUI eloRatingText;
    [SerializeField] private TextMeshProUGUI totalMatchesText;
    [SerializeField] private TextMeshProUGUI winsText;
    [SerializeField] private TextMeshProUGUI lossesText;
    [SerializeField] private TextMeshProUGUI winRateText;
    
    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    
    private void Start()
    {
        profilePanel.SetActive(false);
        closeButton.onClick.AddListener(OnCloseClicked);
    }
    
    public void ShowProfile(ProfileData data)
    {
        profilePanel.SetActive(true);
        UpdateProfileDisplay(data);
    }
    
    public void HideProfile()
    {
        profilePanel.SetActive(false);
    }
    
    private void OnCloseClicked()
    {
        // Instead of just hiding the profile, go back to main menu
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ShowMainMenu();
        }
        else
        {
            // Fallback if MenuManager isn't available
            HideProfile();
        }
    }
    
    private void UpdateProfileDisplay(ProfileData data)
    {
        usernameText.text = data.username;
        walletAddressText.text = FormatWalletAddress(data.walletAddress);
        eloRatingText.text = $"ELO: {data.eloRating}";
        totalMatchesText.text = $"Total Matches: {data.totalMatches}";
        winsText.text = $"Wins: {data.wins}";
        lossesText.text = $"Losses: {data.losses}";
        
        float winRate = data.totalMatches > 0 ? (float)data.wins / data.totalMatches * 100f : 0f;
        winRateText.text = $"Win Rate: {winRate:F1}%";
    }
    
    private string FormatWalletAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length <= 10)
            return address;
            
        return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
    }
}