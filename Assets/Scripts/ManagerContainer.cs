using UnityEngine;

public class ManagerContainer : MonoBehaviour
{
    private static ManagerContainer instance;
    
    [Header("Manager References")]
    public ProfileManager profileManager;
    public WalletManager walletManager;
    public SoarManager soarManager;
    public ELOManager eloManager;
    public PhotonManager photonManager;
    public GameModeManager gameModeManager;
    
    public static ManagerContainer Instance 
    { 
        get { return instance; }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            ValidateManagers();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void ValidateManagers()
    {
        if (profileManager == null) Debug.LogError("ProfileManager not assigned!");
        if (walletManager == null) Debug.LogError("WalletManager not assigned!");
        if (soarManager == null) Debug.LogError("SoarManager not assigned!");
        if (eloManager == null) Debug.LogError("ELOManager not assigned!");
        if (photonManager == null) Debug.LogError("PhotonManager not assigned!");
        if (gameModeManager == null) Debug.LogError("GameModeManager not assigned!");
    }
}