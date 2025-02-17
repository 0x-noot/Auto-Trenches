using UnityEngine;
using Photon.Pun;

public class PlayerHP : MonoBehaviourPunCallbacks, IPunObservable
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
        if (!PhotonNetwork.IsMasterClient) return;

        if (isFirstRound)
        {
            isFirstRound = false;
        }

        float damage = 8f + (1.5f * survivingUnits) + winStreak;
        photonView.RPC("RPCTakeDamage", RpcTarget.All, damage);
    }

    [PunRPC]
    private void RPCTakeDamage(float damage)
    {
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
        }
        else
        {
            // Receive data
            this.currentHP = (float)stream.ReceiveNext();
            this.winStreak = (int)stream.ReceiveNext();
            this.isFirstRound = (bool)stream.ReceiveNext();
        }
    }
}