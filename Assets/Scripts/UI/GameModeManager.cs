using UnityEngine;
using System;

public enum GameMode
{
    Practice,
    Ranked
}

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }
    
    private GameMode currentMode = GameMode.Practice;
    
    public GameMode CurrentMode => currentMode;
    
    public event Action<GameMode> OnGameModeChanged;
    
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
    
    public void SetGameMode(GameMode mode)
    {
        currentMode = mode;
        Debug.Log($"Game mode set to: {mode}");
        OnGameModeChanged?.Invoke(mode);
    }
}