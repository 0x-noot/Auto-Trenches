using UnityEngine;
using UnityEngine.UI;

public class ModeSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private Button practiceButton;
    [SerializeField] private Button rankedButton;
    [SerializeField] private Button backButton;
    
    private void Start()
    {
        practiceButton.onClick.AddListener(OnPracticeModeSelected);
        rankedButton.onClick.AddListener(OnRankedModeSelected);
        backButton.onClick.AddListener(OnBackClicked);
        
    }
    
    private void OnDestroy()
    {
        practiceButton.onClick.RemoveListener(OnPracticeModeSelected);
        rankedButton.onClick.RemoveListener(OnRankedModeSelected);
        backButton.onClick.RemoveListener(OnBackClicked);
    }
    
    public void ShowModeSelection()
    {
        modeSelectionPanel.SetActive(true);
    }
    
    public void HideModeSelection()
    {
        modeSelectionPanel.SetActive(false);
    }
    
    private void OnPracticeModeSelected()
    {
        GameModeManager.Instance.SetGameMode(GameMode.Practice);
        HideModeSelection();
        MenuManager.Instance.ShowLobby();
    }
    
    private void OnRankedModeSelected()
    {
        GameModeManager.Instance.SetGameMode(GameMode.Ranked);
        HideModeSelection();
        MenuManager.Instance.ShowLobby();
    }
    
    private void OnBackClicked()
    {
        HideModeSelection();
        MenuManager.Instance.ShowMainMenu();
    }
}