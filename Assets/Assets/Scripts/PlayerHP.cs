using UnityEngine;

public class PlayerHP : MonoBehaviour
{
    [SerializeField] private float maxHP = 100f;
    private float currentHP;
    private int winStreak = 0;
    private bool isFirstRound = true;

    public event System.Action OnHPChanged;

    private void Start()
    {
        // Only set to full HP on first round
        currentHP = maxHP;
    }

    public void TakeDamage(int survivingUnits)
    {
        // Disable full HP reset after first round
        if (isFirstRound)
        {
            isFirstRound = false;
        }

        float damage = 5f + (1.5f * survivingUnits) + winStreak;
        currentHP = Mathf.Max(0, currentHP - damage);
        
        StartCoroutine(TriggerHPChangedNextFrame());
    }

    private System.Collections.IEnumerator TriggerHPChangedNextFrame()
    {
        yield return null;
        OnHPChanged?.Invoke();
    }

    public void IncrementWinStreak()
    {
        winStreak++;
    }

    public void ResetWinStreak()
    {
        winStreak = 0;
    }

    public float GetCurrentHP() => currentHP;
    public bool IsDead() => currentHP <= 0;

    public void TriggerHPChanged()
    {
        OnHPChanged?.Invoke();
    }
}