using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using System.Collections;

public class MenuManager : MonoBehaviourPunCallbacks
{
    public static MenuManager Instance { get; private set; }

    [SerializeField] private string battleSceneName = "BattleScene";
    
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject walletPanel;
    
    // Reference to username panel so we don't accidentally disable it
    [SerializeField] private GameObject usernamePanel;
    
    [SerializeField] private UnityEngine.UI.Button profileButton; 
    private ModeSelectionUI modeSelectionUI;
    private bool isInitialized = false;
    private bool processedStartupFlags = false;

    private void Awake()
    {
        Debug.Log("MenuManager: Awake called");
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("MenuManager: Instance set");
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.LogWarning("MenuManager: Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        ValidatePanelReferences();
        modeSelectionUI = GetComponent<ModeSelectionUI>() ?? GetComponentInChildren<ModeSelectionUI>();
        
        // Try to find the username panel if not assigned
        if (usernamePanel == null && SoarManager.Instance != null)
        {
            // Look for username panel by reflection or by name
            usernamePanel = GameObject.Find("UsernamePanel");
            if (usernamePanel != null)
            {
                Debug.Log("MenuManager: Found UsernamePanel by name");
            }
        }
    }

    private void ValidatePanelReferences()
    {
        Debug.Log("MenuManager: Validating panel references");
        if (mainMenuPanel == null)
            Debug.LogError("MainMenuPanel reference is missing in MenuManager!");
        if (settingsPanel == null)
            Debug.LogError("SettingsPanel reference is missing in MenuManager!");
        if (infoPanel == null)
            Debug.LogError("InfoPanel reference is missing in MenuManager!");
        if (lobbyPanel == null)
            Debug.LogError("LobbyPanel reference is missing in MenuManager!");
        if (modeSelectionPanel == null)
            Debug.LogError("ModeSelectionPanel reference is missing in MenuManager!");
        if (profilePanel == null)
            Debug.LogWarning("ProfilePanel reference is missing in MenuManager!");
        if (walletPanel == null)
            Debug.LogWarning("WalletPanel reference is missing in MenuManager!");
    }

    private void Start()
    {
        Debug.Log("MenuManager: Start called");
        if (!isInitialized)
        {
            InitializePanels();
        }
        
        if (profileButton != null)
        {
            profileButton.onClick.AddListener(ShowProfile);
        }
        
        // Delay processing startup flags to ensure all managers are initialized
        StartCoroutine(DelayedProcessStartupFlags());
    }
    
    private void OnEnable()
    {
        // Subscribe to scene loaded event to handle reloading of main menu
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"MenuManager: Scene loaded: {scene.name}");
        
        // If we loaded the main menu scene again, process startup flags
        if (scene.name == "MainMenu" && !processedStartupFlags)
        {
            // Delay processing startup flags to ensure all managers are initialized
            StartCoroutine(DelayedProcessStartupFlags());
        }
    }
    
    private IEnumerator DelayedProcessStartupFlags()
    {
        // Wait a short frame delay to ensure other managers are initialized
        yield return new WaitForSeconds(0.2f);
        
        // Process startup flags if not already processed
        if (!processedStartupFlags)
        {
            ProcessStartupFlags();
        }
    }
    
    private void ProcessStartupFlags()
    {
        Debug.Log("MenuManager: Processing startup flags");
        
        bool showMainMenu = PlayerPrefs.GetInt("ShowMainMenu", 0) == 1;
        bool keepWalletConnected = PlayerPrefs.GetInt("KeepWalletConnected", 0) == 1;
        string savedUsername = PlayerPrefs.GetString("PlayerUsername", "");
        bool returningFromGame = PlayerPrefs.GetInt("ReturningFromGame", 0) == 1;
        
        Debug.Log($"Flags: ShowMainMenu={showMainMenu}, KeepWalletConnected={keepWalletConnected}, HasUsername={!string.IsNullOrEmpty(savedUsername)}, ReturningFromGame={returningFromGame}");
        
        // Set processedStartupFlags to true before any early returns to prevent future reprocessing
        processedStartupFlags = true;
        
        // Don't process if the username panel is active - this means registration is in progress
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, skipping startup flags processing");
            return;
        }
        
        // FIRST PRIORITY: Check if wallet is connected
        if (WalletManager.Instance != null)
        {
            Debug.Log($"WalletManager exists, IsConnected = {WalletManager.Instance.IsConnected}");
            if (WalletManager.Instance.IsConnected)
            {
                Debug.Log("WalletManager is connected, showing main menu");
                ShowMainMenu();
                return;
            }
        }
        else
        {
            Debug.LogWarning("WalletManager.Instance is null! Checking other conditions...");
        }
        
        // SECOND PRIORITY: Check if returning from game
        if (returningFromGame)
        {
            // Reset flag
            PlayerPrefs.SetInt("ReturningFromGame", 0);
            PlayerPrefs.Save();
            
            Debug.Log("Returning from game, showing main menu");
            ShowMainMenu();
            return;
        }
        
        // THIRD PRIORITY: Check for saved username and keepWalletConnected
        if (keepWalletConnected && !string.IsNullOrEmpty(savedUsername))
        {
            Debug.Log("KeepWalletConnected and username exists, showing main menu");
            ShowMainMenu();
            return;
        }
        
        // FOURTH PRIORITY: Check showMainMenu flag
        if (showMainMenu)
        {
            Debug.Log("ShowMainMenu flag set, showing main menu");
            ShowMainMenu();
            return;
        }
        
        // DEFAULT: If none of the above conditions are met, show wallet panel for new sessions
        Debug.Log("No flags set for showing main menu, showing wallet panel");
        PlayerPrefs.SetInt("ShowMainMenu", 0);
        PlayerPrefs.SetInt("ReturningFromGame", 0);
        PlayerPrefs.Save();
        ShowWalletPanel();
    }

    private void InitializePanels()
    {
        if (isInitialized) return;

        Debug.Log("MenuManager: Initializing panels");
        ValidatePanelReferences();

        mainMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        infoPanel?.SetActive(false);
        lobbyPanel?.SetActive(false);
        modeSelectionPanel?.SetActive(false);
        profilePanel?.SetActive(false);
        if (walletPanel != null)
            walletPanel.SetActive(false);
        
        isInitialized = true;
        Debug.Log("MenuManager: Panels initialized");
    }

    private void DisableAllPanels()
    {
        Debug.Log("MenuManager: Disabling all panels");
        mainMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        infoPanel?.SetActive(false);
        lobbyPanel?.SetActive(false);
        modeSelectionPanel?.SetActive(false);
        profilePanel?.SetActive(false);
        
        // CRITICAL: Always disable the wallet panel when switching to another panel
        if (walletPanel != null)
        {
            Debug.Log("MenuManager: Forcibly disabling wallet panel");
            walletPanel.SetActive(false);
        }
        
        // IMPORTANT: Do not disable the username panel as it's handled by SoarManager
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active - NOT disabling it");
            // Keep username panel visible if it's being shown
        }
    }

    public void ShowMainMenu()
    {
        Debug.Log("MenuManager: ShowMainMenu called");
        
        // Don't disable panels if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing main menu yet");
            return;
        }
        
        DisableAllPanels();
        
        // CRITICAL: Forcibly disable wallet panel again just to be sure
        if (walletPanel != null)
        {
            walletPanel.SetActive(false);
        }
        
        // Enable main menu
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
    }
    
    public void ShowWalletPanel()
    {
        Debug.Log("MenuManager: ShowWalletPanel called");
        
        // Don't disable panels if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing wallet panel");
            return;
        }
        
        DisableAllPanels();
        
        if (walletPanel != null)
        {
            Debug.Log("MenuManager: Enabling wallet panel");
            walletPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("MenuManager: WalletPanel reference is null!");
        }
    }

    public void ShowSettings()
    {
        Debug.Log("MenuManager: ShowSettings called");
        
        // Don't disable panels if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing settings");
            return;
        }
        
        DisableAllPanels();
        settingsPanel?.SetActive(true);
    }

    public void ShowInfo()
    {
        Debug.Log("MenuManager: ShowInfo called");
        
        // Don't disable panels if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing info");
            return;
        }
        
        DisableAllPanels();
        infoPanel?.SetActive(true);
    }

    public void ShowModeSelection()
    {
        Debug.Log("MenuManager: ShowModeSelection called");
        
        // Don't disable panels if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing mode selection");
            return;
        }
        
        DisableAllPanels();
        modeSelectionPanel?.SetActive(true);
    }

    public void ShowProfile()
    {
        Debug.Log("MenuManager: ShowProfile called");
        
        // Don't disable panels if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing profile");
            return;
        }
        
        DisableAllPanels();
        profilePanel?.SetActive(true);
        
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.ShowProfile();
        }
        else
        {
            Debug.LogError("ProfileManager instance not found!");
        }
    }

    public void ShowLobby()
    {
        Debug.Log("MenuManager: ShowLobby called");
        
        // Don't disable panels if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing lobby");
            return;
        }
        
        DisableAllPanels();
        lobbyPanel?.SetActive(true);

        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("MenuManager: Not connected to Photon, connecting now");
            PhotonManager photonManager = FindFirstObjectByType<PhotonManager>();
            if (photonManager != null)
            {
                // Call the enhanced method to ensure Photon is connected
                photonManager.EnsureConnected();
            }
            else
            {
                Debug.LogError("PhotonManager not found!");
            }
        }
        else if (!PhotonNetwork.InLobby)
        {
            Debug.Log("MenuManager: Connected but not in lobby, joining lobby");
            PhotonNetwork.JoinLobby();
        }
        else
        {
            Debug.Log("MenuManager: Already connected and in lobby, refreshing room list");
            // Force a refresh of the room list
            PhotonManager photonManager = FindFirstObjectByType<PhotonManager>();
            if (photonManager != null)
            {
                photonManager.RefreshRoomList();
            }
        }
        
        LobbyUI lobbyUI = FindFirstObjectByType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.ShowLobbyListPanel();
            
            // Force a refresh of the room list through the lobby UI
            lobbyUI.ForceRefreshRoomList();
        }
    }

    public void QuitGame()
    {
        Debug.Log("MenuManager: QuitGame called");
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    public void ResetUsername()
    {
        PlayerPrefs.DeleteKey("PlayerUsername");
        
        LobbyUI lobbyUI = FindFirstObjectByType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.ShowWalletPanel();
        }
        
        DisableAllPanels();
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        Debug.Log($"MenuManager: Disconnected from Photon with cause: {cause}");
        
        // Don't show main menu if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing main menu after disconnect");
            return;
        }
        
        ShowMainMenu();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("MenuManager: Left room, returning to lobby");
        
        // Don't show lobby if username panel is active
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            Debug.Log("MenuManager: Username panel is active, not showing lobby after leaving room");
            return;
        }
        
        ShowLobby();
    }
}