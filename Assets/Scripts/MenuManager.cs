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
    [SerializeField] private GameObject usernamePanel;
    
    [SerializeField] private UnityEngine.UI.Button profileButton; 
    private ModeSelectionUI modeSelectionUI;
    private bool isInitialized = false;
    private bool processedStartupFlags = false;

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

        ValidatePanelReferences();
        modeSelectionUI = GetComponent<ModeSelectionUI>() ?? GetComponentInChildren<ModeSelectionUI>();
        
        if (usernamePanel == null && SoarManager.Instance != null)
        {
            usernamePanel = GameObject.Find("UsernamePanel");
        }
    }

    private void ValidatePanelReferences()
    {
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
    }

    private void Start()
    {
        if (!isInitialized)
        {
            InitializePanels();
        }
        
        if (profileButton != null)
        {
            profileButton.onClick.AddListener(ShowProfile);
        }
        
        StartCoroutine(DelayedProcessStartupFlags());
    }
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainMenu" && !processedStartupFlags)
        {
            StartCoroutine(DelayedProcessStartupFlags());
        }
    }
    
    private IEnumerator DelayedProcessStartupFlags()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (!processedStartupFlags)
        {
            ProcessStartupFlags();
        }
    }
    
    private void ProcessStartupFlags()
    {
        bool showMainMenu = PlayerPrefs.GetInt("ShowMainMenu", 0) == 1;
        bool keepWalletConnected = PlayerPrefs.GetInt("KeepWalletConnected", 0) == 1;
        string savedUsername = PlayerPrefs.GetString("PlayerUsername", "");
        bool returningFromGame = PlayerPrefs.GetInt("ReturningFromGame", 0) == 1;
        
        processedStartupFlags = true;
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        if (WalletManager.Instance != null)
        {
            if (WalletManager.Instance.IsConnected)
            {
                ShowMainMenu();
                return;
            }
        }
        
        if (returningFromGame)
        {
            PlayerPrefs.SetInt("ReturningFromGame", 0);
            PlayerPrefs.Save();
            
            ShowMainMenu();
            return;
        }
        
        if (keepWalletConnected && !string.IsNullOrEmpty(savedUsername))
        {
            ShowMainMenu();
            return;
        }
        
        if (showMainMenu)
        {
            ShowMainMenu();
            return;
        }
        
        PlayerPrefs.SetInt("ShowMainMenu", 0);
        PlayerPrefs.SetInt("ReturningFromGame", 0);
        PlayerPrefs.Save();
        ShowWalletPanel();
    }

    private void InitializePanels()
    {
        if (isInitialized) return;

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
    }

    private void DisableAllPanels()
    {
        mainMenuPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
        infoPanel?.SetActive(false);
        lobbyPanel?.SetActive(false);
        modeSelectionPanel?.SetActive(false);
        profilePanel?.SetActive(false);
        
        if (walletPanel != null)
        {
            walletPanel.SetActive(false);
        }
        
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            // Do not disable the username panel
        }
    }

    public void ShowMainMenu()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        DisableAllPanels();
        
        if (walletPanel != null)
        {
            walletPanel.SetActive(false);
        }
        
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
    }
    
    public void ShowWalletPanel()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        DisableAllPanels();
        
        if (walletPanel != null)
        {
            walletPanel.SetActive(true);
        }
    }

    public void ShowSettings()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        DisableAllPanels();
        settingsPanel?.SetActive(true);
    }

    public void ShowInfo()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        DisableAllPanels();
        infoPanel?.SetActive(true);
    }

    public void ShowModeSelection()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        DisableAllPanels();
        modeSelectionPanel?.SetActive(true);
    }

    public void ShowProfile()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
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
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        DisableAllPanels();
        lobbyPanel?.SetActive(true);

        if (!PhotonNetwork.IsConnected)
        {
            PhotonManager photonManager = FindFirstObjectByType<PhotonManager>();
            if (photonManager != null)
            {
                photonManager.EnsureConnected();
            }
            else
            {
                Debug.LogError("PhotonManager not found!");
            }
        }
        else if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
        }
        else
        {
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
            lobbyUI.ForceRefreshRoomList();
        }
    }

    public void QuitGame()
    {
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
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        ShowMainMenu();
    }

    public override void OnLeftRoom()
    {
        if (usernamePanel != null && usernamePanel.activeInHierarchy)
        {
            return;
        }
        
        ShowLobby();
    }
}