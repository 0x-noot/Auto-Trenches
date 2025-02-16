using UnityEngine;
using System;
using System.Collections;
using Photon.Pun;

public abstract class BaseUnit : MonoBehaviourPunCallbacks
{
    [Header("Base Stats")]
    protected UnitType unitType;
    protected float baseHealth;
    protected float baseDamage;
    protected float baseAttackSpeed;
    protected float baseMoveSpeed;
    protected float attackRange;

    // Current stats
    protected float maxHealth;
    protected float currentHealth;
    protected float attackDamage;
    protected float attackSpeed;
    protected float moveSpeed;

    // Stats with upgrades applied
    protected float currentMaxHealth;
    protected float currentAttackDamage;
    protected float currentAttackSpeed;
    protected float currentMoveSpeed;

    [Header("Team Settings")]
    [SerializeField] protected string teamId;

    [Header("Death Settings")]
    [SerializeField] protected float deathAnimationDuration = 1f;
    [SerializeField] protected bool useDeathAnimation = true;

    [Header("Ability Settings")]
    [SerializeField] protected float baseAbilityCooldown = 15f;
    [SerializeField] protected float abilityChance = 0.2f;
    protected bool isAbilityActive = false;
    protected float nextAbilityTime = 0f;

    protected UnitState currentState;
    protected BaseUnit currentTarget;
    protected float lastAttackTime;
    protected HealthSystem healthSystem;

    private MovementSystem movementSystem;
    private CombatSystem combatSystem;

    public event Action<BaseUnit> OnUnitDeath;
    public event Action<BaseUnit> OnAbilityActivated;
    public event Action<BaseUnit> OnAbilityDeactivated;

    protected virtual void Start()
    {
        InitializeBaseStats();
        ApplyUpgrades();
        currentHealth = currentMaxHealth;
        currentState = UnitState.Idle;

        healthSystem = GetComponent<HealthSystem>();
        movementSystem = GetComponent<MovementSystem>();
        combatSystem = GetComponent<CombatSystem>();

        if (healthSystem != null)
        {
            healthSystem.Initialize(currentMaxHealth);
        }

        nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnUpgradePurchased += HandleUpgradePurchased;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }

        if (movementSystem != null)
        {
            movementSystem.SetMoveSpeed(currentMoveSpeed);
        }
    }

    protected virtual void InitializeBaseStats()
    {
        baseHealth = maxHealth;
        baseDamage = attackDamage;
        baseAttackSpeed = attackSpeed;
        baseMoveSpeed = moveSpeed;
    }

    protected void ApplyUpgrades()
    {
        if (!photonView.IsMine) return;
        
        if (EconomyManager.Instance == null) return;

        float armorMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Armor);
        float damageMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Training);
        float speedMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.Speed);
        float attackSpeedMultiplier = EconomyManager.Instance.GetUpgradeMultiplier(teamId, UpgradeType.AttackSpeed);

        photonView.RPC("RPCApplyUpgrades", RpcTarget.All, armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    [PunRPC]
    protected virtual void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        currentMaxHealth = maxHealth * armorMultiplier;
        currentAttackDamage = attackDamage * damageMultiplier;
        currentMoveSpeed = moveSpeed * speedMultiplier;
        currentAttackSpeed = attackSpeed * attackSpeedMultiplier;

        if (currentHealth > 0)
        {
            float healthPercentage = currentHealth / maxHealth;
            currentHealth = currentMaxHealth * healthPercentage;
            
            if (healthSystem != null)
            {
                healthSystem.Initialize(currentMaxHealth);
            }
        }

        if (movementSystem != null)
        {
            movementSystem.SetMoveSpeed(currentMoveSpeed);
        }
    }

    private void HandleUpgradePurchased(string team, UpgradeType type, int level)
    {
        if (team == teamId && photonView.IsMine)
        {
            ApplyUpgrades();
        }
    }

    protected virtual void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
        }
    }

    protected virtual void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.BattleActive)
        {
            nextAbilityTime = Time.time + UnityEngine.Random.Range(0f, baseAbilityCooldown);
        }
    }

    protected virtual void Update()
    {
        if (!photonView.IsMine) return;

        if (GameManager.Instance != null && 
            GameManager.Instance.GetCurrentState() == GameState.BattleActive && 
            currentState == UnitState.Attacking &&
            currentState != UnitState.Dead && 
            Time.time >= nextAbilityTime)
        {
            TryActivateAbility();
        }
    }

    protected virtual void TryActivateAbility()
    {
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            ActivateAbility();
            nextAbilityTime = Time.time + baseAbilityCooldown;
        }
    }

    protected virtual void ActivateAbility()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCActivateAbility", RpcTarget.All);
    }

    [PunRPC]
    protected virtual void RPCActivateAbility()
    {
        isAbilityActive = true;
        OnAbilityActivated?.Invoke(this);
    }

    protected virtual void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCDeactivateAbility", RpcTarget.All);
    }

    [PunRPC]
    protected virtual void RPCDeactivateAbility()
    {
        isAbilityActive = false;
        OnAbilityDeactivated?.Invoke(this);
    }

    public virtual void UpdateState(UnitState newState)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCUpdateState", RpcTarget.All, (int)newState);
    }

    [PunRPC]
    protected virtual void RPCUpdateState(int newState)
    {
        currentState = (UnitState)newState;
    }

    public virtual void TakeDamage(float damage)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCTakeDamage", RpcTarget.All, damage);
    }

    [PunRPC]
    protected virtual void RPCTakeDamage(float damage)
    {
        if (currentState == UnitState.Dead) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        healthSystem?.TakeDamage(damage);
        
        if (currentHealth <= 0)
        {
            Die();
        }   
    }

    protected virtual void Die()
    {
        if (currentState == UnitState.Dead) return;
        
        if (photonView.IsMine)
        {
            photonView.RPC("RPCDie", RpcTarget.All);
        }
    }

    [PunRPC]
    protected virtual void RPCDie()
    {
        currentState = UnitState.Dead;

        if (movementSystem != null)
        {
            movementSystem.StopMovement();
        }

        var enemyTargeting = GetComponent<EnemyTargeting>();
        if (enemyTargeting != null)
        {
            enemyTargeting.StopTargeting();
        }

        OnUnitDeath?.Invoke(this);

        if (useDeathAnimation)
        {
            StartCoroutine(DeathSequence());
        }
        else
        {
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(gameObject);
            }
        }

        GameManager.Instance?.HandleUnitDeath(this);
    }

    private IEnumerator DeathSequence()
    {
        var colliders = GetComponents<Collider2D>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            float elapsedTime = 0f;
            Color startColor = spriteRenderer.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (elapsedTime < deathAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / deathAnimationDuration;
                spriteRenderer.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(deathAnimationDuration);
        }

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }

    public void SetTeam(string newTeamId)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCSetTeam", RpcTarget.All, newTeamId);
    }

    [PunRPC]
    private void RPCSetTeam(string newTeamId)
    {
        teamId = newTeamId;
        string layerName = newTeamId;
        int layerIndex = LayerMask.NameToLayer(layerName);
        
        if (layerIndex != -1)
        {
            gameObject.layer = layerIndex;
        }
        else
        {
            Debug.LogError($"Failed to find layer: {layerName}");
        }
    }

    // Getters
    public string GetTeamId() => teamId;
    public virtual UnitState GetCurrentState() => currentState;
    public virtual float GetAttackRange() => attackRange;
    public virtual float GetAttackDamage() => currentAttackDamage;
    public virtual float GetAttackSpeed() => currentAttackSpeed;
    public virtual float GetMoveSpeed() => currentMoveSpeed;
    public virtual UnitType GetUnitType() => unitType;
    public float GetDeathAnimationDuration() => deathAnimationDuration;
    public bool IsAbilityActive() => isAbilityActive;
}