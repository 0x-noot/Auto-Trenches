using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private string battleSceneName = "BattleScene";
    
    [Header("Panel References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject infoPanel;

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"MenuManager: Scene '{scene.name}' loaded");
        if (scene.name == "MainMenu")
        {
            InitializePanels();
            ShowMainMenu();
        }
    }

    private void OnEnable()
    {
        Debug.Log("MenuManager: OnEnable called");
        SceneManager.sceneLoaded += OnSceneLoaded;
        InitializePanels();
    }

    private void OnDisable()
    {
        Debug.Log("MenuManager: OnDisable called");
        SceneManager.sceneLoaded -= OnSceneLoaded;
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

        // First ensure all panels exist
        if (mainMenuPanel == null || settingsPanel == null || infoPanel == null)
        {
            Debug.LogError("MenuManager: One or more panels are missing!");
            return;
        }

        // Enable all panels to ensure proper initialization
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(true);
        infoPanel.SetActive(true);

        // Then disable all
        DisableAllPanels();
        
        isInitialized = true;
        Debug.Log("MenuManager: Panels initialized");
    }

    private void DisableAllPanels()
    {
        Debug.Log("MenuManager: Disabling all panels");
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
            Debug.Log("MenuManager: Main menu panel disabled");
        }
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            Debug.Log("MenuManager: Settings panel disabled");
        }
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
            Debug.Log("MenuManager: Info panel disabled");
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
    }

    public void ShowMainMenu()
    {
        Debug.Log("MenuManager: ShowMainMenu called");
        DisableAllPanels();
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("MenuManager: Main menu panel enabled");
        }
    }

    public void ShowSettings()
    {
        Debug.Log("MenuManager: ShowSettings called");
        DisableAllPanels();
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            Debug.Log("MenuManager: Settings panel enabled");
        }
    }

    public void ShowInfo()
    {
        Debug.Log("MenuManager: ShowInfo called");
        DisableAllPanels();
        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
            Debug.Log("MenuManager: Info panel enabled");
        }
    }

    public void StartGame()
    {
        Debug.Log("MenuManager: StartGame called");
        DisableAllPanels();
        SceneManager.LoadScene(battleSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("MenuManager: QuitGame called");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}