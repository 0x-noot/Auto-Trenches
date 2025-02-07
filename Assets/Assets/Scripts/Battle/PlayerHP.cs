using UnityEngine;

public class PlayerHP : MonoBehaviour
{
    [SerializeField] private float maxHP = 100f;
    private float currentHP;
    public int winStreak { get; private set; } = 0;
    private bool isFirstRound = true;

    public event System.Action OnHPChanged;

    private void Start()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(int survivingUnits)
    {
        if (isFirstRound)
        {
            isFirstRound = false;
        }

        float damage = 8f + (1.5f * survivingUnits) + winStreak;  // Increased base damage to 8
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