using UnityEngine;
using Photon.Pun;

public class PlayerHP : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private float maxHP = 100f;
    private float currentHP;
    public int winStreak { get; private set; } = 0;
    private bool isFirstRound = true;
    private int roundNumber = 0;

    // Damage formula constants - adjusted for shorter games
    [Header("Damage Settings")]
    [SerializeField] private float baseDamage = 10f;            // Increased from 7f
    [SerializeField] private float damagePerUnit = 1.5f;        // Increased from 1.25f
    [SerializeField] private float winStreakBonus = 1.5f;       // Increased from 1f
    [SerializeField] private float roundProgressiveBonus = 0.15f; // 15% more damage per round
    [SerializeField] private float minDamage = 12f;             // Minimum damage per round

    public event System.Action OnHPChanged;

    private void Start()
    {
        currentHP = maxHP;
    }

    public void TakeDamage(int survivingUnits)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (isFirstRound)
        {
            isFirstRound = false;
        }

        roundNumber++;

        // Base formula with increased values
        float damage = baseDamage + (damagePerUnit * survivingUnits) + (winStreakBonus * winStreak);
        
        // Progressive damage increase - damage increases by 15% each round
        float roundMultiplier = 1f + (roundNumber * roundProgressiveBonus);
        damage *= roundMultiplier;
        
        // Ensure minimum damage
        damage = Mathf.Max(damage, minDamage);
        
        Debug.Log($"Round {roundNumber} Damage: Base {baseDamage} + ({damagePerUnit} × {survivingUnits} units) " +
                $"+ ({winStreakBonus} × {winStreak} streak) = {damage/roundMultiplier:F1} " +
                $"× {roundMultiplier:F2} (round bonus) = {damage:F1} total, Current HP: {currentHP}, New HP: {currentHP - damage}");
        
        photonView.RPC("RPCTakeDamage", RpcTarget.All, damage);
    }

    [PunRPC]
    private void RPCTakeDamage(float damage)
    {
        float previousHP = currentHP;
        currentHP = Mathf.Max(0, currentHP - damage);
        Debug.Log($"HP changed from {previousHP} to {currentHP} (damage: {damage})");
        StartCoroutine(TriggerHPChangedNextFrame());
    }

    public void CheckDeathCondition()
    {
        if (currentHP <= 0 && !IsDead())
        {
            // Force a match end
            if (BattleRoundManager.Instance != null)
            {
                // BattleRoundManager will determine the winner
                BattleRoundManager.Instance.photonView.RPC("RPCForceMatchEnd", RpcTarget.All);
            }
        }
    }

    private System.Collections.IEnumerator TriggerHPChangedNextFrame()
    {
        yield return null;
        OnHPChanged?.Invoke();
    }

    public void IncrementWinStreak()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPCIncrementWinStreak", RpcTarget.All);
    }

    [PunRPC]
    private void RPCIncrementWinStreak()
    {
        winStreak++;
    }

    public void ResetWinStreak()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        photonView.RPC("RPCResetWinStreak", RpcTarget.All);
    }

    [PunRPC]
    private void RPCResetWinStreak()
    {
        winStreak = 0;
    }

    // Add method to reset round counter when starting new match
    public void ResetForNewMatch()
    {
        roundNumber = 0;
        winStreak = 0;
        isFirstRound = true;
        currentHP = maxHP;
        TriggerHPChanged();
    }

    public float GetCurrentHP() => currentHP;
    public bool IsDead() => currentHP <= 0;

    public void TriggerHPChanged()
    {
        OnHPChanged?.Invoke();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(currentHP);
            stream.SendNext(winStreak);
            stream.SendNext(isFirstRound);
            stream.SendNext(roundNumber);
        }
        else
        {
            // Receive data
            this.currentHP = (float)stream.ReceiveNext();
            this.winStreak = (int)stream.ReceiveNext();
            this.isFirstRound = (bool)stream.ReceiveNext();
            this.roundNumber = (int)stream.ReceiveNext();
        }
    }
}