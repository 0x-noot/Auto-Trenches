using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class HealthSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private Slider healthBar;
    private float maxHealth;
    private float currentHealth;

    private void Awake()
    {
        // Optional: validate the reference
        if (healthBar == null)
        {
            Debug.LogWarning("HealthBar reference is missing in HealthSystem!");
        }
    }

    public void Initialize(float max)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCInitialize", RpcTarget.All, max);
    }

    [PunRPC]
    private void RPCInitialize(float max)
    {
        maxHealth = max;
        currentHealth = max;
        UpdateHealthBar();
    }

    public void TakeDamage(float damage)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCTakeDamage", RpcTarget.All, damage);
    }

    [PunRPC]
    private void RPCTakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.value = currentHealth / maxHealth;
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // We own this player: send the others our data
            stream.SendNext(currentHealth);
            stream.SendNext(maxHealth);
        }
        else
        {
            // Network player, receive data
            this.currentHealth = (float)stream.ReceiveNext();
            this.maxHealth = (float)stream.ReceiveNext();
            UpdateHealthBar(); // Update the visual when we receive new data
        }
    }

    // Getter methods
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => currentHealth / maxHealth;
}