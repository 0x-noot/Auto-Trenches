[System.Serializable]
public class ProfileData
{
    public string username;
    public string walletAddress;
    public int eloRating;
    public int totalMatches;
    public int wins;
    public int losses;
    
    public ProfileData()
    {
        username = "Unknown";
        walletAddress = "";
        eloRating = 1200; // Default ELO
        totalMatches = 0;
        wins = 0;
        losses = 0;
    }
}