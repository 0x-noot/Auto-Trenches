// ELOManager.cs
using System;
using UnityEngine;

public class ELOManager : MonoBehaviour
{
    public static ELOManager Instance { get; private set; }
    
    [Header("ELO Settings")]
    [SerializeField] private float kFactor = 32f; // Standard K-factor
    [SerializeField] private int defaultELO = 1200;
    
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
    
    public int CalculateELOChange(int playerRating, int opponentRating, bool won)
    {
        // Expected score calculation
        float expectedScore = 1f / (1f + Mathf.Pow(10f, (opponentRating - playerRating) / 400f));
        
        // Actual score (1 for win, 0 for loss)
        float actualScore = won ? 1f : 0f;
        
        // ELO change
        int change = Mathf.RoundToInt(kFactor * (actualScore - expectedScore));
        
        return change;
    }
    
    public int GetNewELO(int currentELO, int eloChange)
    {
        return Math.Max(0, currentELO + eloChange);
    }
}