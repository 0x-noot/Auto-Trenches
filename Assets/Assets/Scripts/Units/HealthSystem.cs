using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System;
using System.Collections;

public class HealthSystem : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField] private Slider healthBar;
    private float maxHealth;
    private float currentHealth;
    private bool isProcessingRPC = false;
    private bool isUpdatingUI = false;

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
        UpdateHealthBarSafely();
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
        
        UpdateHealthBarSafely();
        
        // Use a coroutine to trigger the event on the next frame
        // This helps avoid event callbacks during RPC processing
        StartCoroutine(TriggerHPChangedNextFrame());
    }

    // Safe update that prevents UI rebuild errors
    private void UpdateHealthBarSafely()
    {
        if (isUpdatingUI) return;
        
        try {
            isUpdatingUI = true;
            
            if (healthBar != null && healthBar.gameObject.activeInHierarchy)
            {
                // Update slider value directly
                float healthPercentage = maxHealth > 0 ? currentHealth / maxHealth : 0;
                healthBar.value = healthPercentage;
                
                // Delay the force update to avoid nested canvas rebuilds
                StartCoroutine(DelayedCanvasUpdate());
            }
        }
        finally {
            isUpdatingUI = false;
        }
    }
    
    private IEnumerator DelayedCanvasUpdate()
    {
        // Wait for end of frame to update canvas
        yield return new WaitForEndOfFrame();
        
        if (healthBar != null && healthBar.gameObject.activeInHierarchy)
        {
            // Update layout if needed
            LayoutRebuilder.ForceRebuildLayoutImmediate(healthBar.GetComponent<RectTransform>());
        }
    }

    private IEnumerator TriggerHPChangedNextFrame()
    {
        yield return null;
        OnHPChanged?.Invoke();
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
            
            // Update UI safely
            UpdateHealthBarSafely();
        }
    }
    public void SetHealth(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;
        
        // Safe update that prevents UI rebuild errors
        if (healthBar != null && healthBar.gameObject.activeInHierarchy)
        {
            // Update slider value directly without using Canvas.ForceUpdateCanvases()
            healthBar.value = maxHealth > 0 ? currentHealth / maxHealth : 0;
        }
        
        // Notify listeners next frame to avoid callback recursion
        StartCoroutine(TriggerHPChangedNextFrame());
    }
    public void TriggerHPChanged()
    {
        if (photonView.IsMine)
        {
            StartCoroutine(TriggerHPChangedNextFrame());
        }
    }

    // Getter methods
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => currentHealth / maxHealth;
}