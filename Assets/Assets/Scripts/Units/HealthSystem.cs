using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System;

public class HealthSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private Slider healthBar;
    private float maxHealth;
    private float currentHealth;
    private bool isProcessingRPC = false;

    public event Action OnHPChanged;

    private void Awake()
    {
        if (healthBar == null)
        {
            Debug.LogWarning("HealthBar reference is missing in HealthSystem!");
        }
    }

    public void Initialize(float max)
    {
        if (!photonView.IsMine) return;
        
        Debug.Log($"Initializing health system with max health: {max}");
        photonView.RPC("RPCInitialize", RpcTarget.All, max);
    }

    [PunRPC]
    private void RPCInitialize(float max)
    {
        maxHealth = max;
        currentHealth = max;
        UpdateHealthBar();
        Debug.Log($"Health system initialized. Max Health: {maxHealth}, Current Health: {currentHealth}");
    }

    public void TakeDamage(float damage)
    {
        if (!photonView.IsMine || isProcessingRPC) return;

        // Prevent recursive RPC calls
        isProcessingRPC = true;
        photonView.RPC("RPCTakeDamage", RpcTarget.All, damage);
        isProcessingRPC = false;
    }

    [PunRPC]
    private void RPCTakeDamage(float damage)
    {
        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        Debug.Log($"Taking damage: {damage}. Health: {previousHealth} -> {currentHealth}");
        
        UpdateHealthBar();
        OnHPChanged?.Invoke();
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.value = currentHealth / maxHealth;
            
            // Ensure the UI is updated immediately
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(healthBar.GetComponent<RectTransform>());
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
            currentHealth = (float)stream.ReceiveNext();
            maxHealth = (float)stream.ReceiveNext();
            UpdateHealthBar();
        }
    }

    public void TriggerHPChanged()
    {
        if (photonView.IsMine)
        {
            OnHPChanged?.Invoke();
        }
    }

    // Getter methods
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => currentHealth / maxHealth;
}