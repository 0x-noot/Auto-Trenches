using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class MenuManager : MonoBehaviourPunCallbacks
{
    public static MenuManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private string battleSceneName = "BattleScene";
    
    [Header("Panel References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private GameObject lobbyPanel;

    private bool isInitialized = false;

    private void Awake()
    {
        Debug.Log("MenuManager: Awake called");
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("MenuManager: Instance set");
        }
        else
        {
            Debug.LogWarning("MenuManager: Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        ValidatePanelReferences();
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
    }

    private void Start()
    {
        Debug.Log("MenuManager: Start called");
        if (!isInitialized)
        {
            InitializePanels();
        }
        ShowMainMenu();
    }

    private void InitializePanels()
    {
        if (isInitialized) return;

        Debug.Log("MenuManager: Initializing panels");
        ValidatePanelReferences();

        // First ensure all panels exist and are initialized
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(true);
        infoPanel.SetActive(true);
        lobbyPanel.SetActive(true);

        // Then disable all
        DisableAllPanels();
        
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
    }

    public void ShowMainMenu()
    {
        Debug.Log("MenuManager: ShowMainMenu called");
        DisableAllPanels();
        mainMenuPanel?.SetActive(true);
    }

    public void ShowSettings()
    {
        Debug.Log("MenuManager: ShowSettings called");
        DisableAllPanels();
        settingsPanel?.SetActive(true);
    }

    public void ShowInfo()
    {
        Debug.Log("MenuManager: ShowInfo called");
        DisableAllPanels();
        infoPanel?.SetActive(true);
    }

    public void ShowLobby()
    {
        Debug.Log("MenuManager: ShowLobby called");
        DisableAllPanels();
        lobbyPanel?.SetActive(true);

        // Connect to Photon if not already connected
        if (!PhotonNetwork.IsConnected)
        {
            PhotonManager.Instance.ConnectToPhoton();
        }
    }

    public void QuitGame()
    {
        Debug.Log("MenuManager: QuitGame called");
        // Disconnect from Photon if connected
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

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        Debug.Log($"MenuManager: Disconnected from Photon with cause: {cause}");
        // Return to main menu if disconnected
        ShowMainMenu();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("MenuManager: Left room, returning to lobby");
        // Return to lobby when leaving a room
        ShowLobby();
    }
}