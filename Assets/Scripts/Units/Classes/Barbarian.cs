using UnityEngine;
using System.Collections;
using Photon.Pun;

public class Barbarian : BaseUnit
{
    [Header("Barbarian-Specific Settings")]
    [SerializeField] private float baseCriticalStrikeChance = 0.15f;
    [SerializeField] private float currentCriticalStrikeChance;

    [Header("Primal Strike Ability Settings")]
    [SerializeField] private float stunDuration = 2.0f;
    [SerializeField] private float damageBonus = 0.5f;
    [SerializeField] private GameObject stunEffectPrefab;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem strikeParticles;
    [SerializeField] private Color primalStrikeColor = new Color(1f, 0.4f, 0.0f, 1f);
    [SerializeField] private int particlesSortingOrder = 10;
    
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private BaseUnit currentStunnedTarget;
    private bool abilityStarted = false;
    
    private BarbarianAnimator barbarianAnimator;
    private Vector3 lastPosition;

    protected override void Awake()
    {
        unitType = UnitType.Barbarian;
        orderType = OrderType.Wild;
        baseHealth = 850f;
        baseDamage = 110f;
        baseAttackSpeed = 0.9f;
        baseMoveSpeed = 3.4f;
        attackRange = 3.5f;
        abilityChance = 0.08f;
        
        base.Awake();
        
        maxHealth = baseHealth;
        attackDamage = baseDamage;
        attackSpeed = baseAttackSpeed;
        moveSpeed = baseMoveSpeed;
        currentCriticalStrikeChance = baseCriticalStrikeChance;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        if (strikeParticles == null)
        {
            strikeParticles = GetComponent<ParticleSystem>();
        }
        
        if (strikeParticles != null)
        {
            var renderer = strikeParticles.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = particlesSortingOrder;
            }
        }
        
        barbarianAnimator = GetComponent<BarbarianAnimator>();
        if (barbarianAnimator == null)
        {
            barbarianAnimator = gameObject.AddComponent<BarbarianAnimator>();
        }
        
        lastPosition = transform.position;
    }

    protected override void Update()
    {
        base.Update();
        
        if (photonView.IsMine)
        {
            UpdateAnimations();
            lastPosition = transform.position;
        }
    }
    
    private void UpdateAnimations()
    {
        switch (currentState)
        {
            case UnitState.Moving:
                barbarianAnimator.SetMoving(true);
                barbarianAnimator.SetAttacking(false);
                
                Vector3 moveDir = transform.position - lastPosition;
                if (moveDir.magnitude > 0.01f)
                {
                    barbarianAnimator.SetDirectionFromVector(new Vector2(moveDir.x, moveDir.y));
                }
                break;
                
            case UnitState.Attacking:
                barbarianAnimator.SetMoving(false);
                barbarianAnimator.SetAttacking(true);
                
                EnemyTargeting targeting = GetComponent<EnemyTargeting>();
                if (targeting != null)
                {
                    Transform targetTransform = targeting.GetCurrentTarget();
                    if (targetTransform != null)
                    {
                        Vector3 targetDir = targetTransform.position - transform.position;
                        barbarianAnimator.SetDirectionFromVector(new Vector2(targetDir.x, targetDir.y));
                    }
                }
                break;
                
            case UnitState.Idle:
                barbarianAnimator.SetMoving(false);
                barbarianAnimator.SetAttacking(false);
                break;
                
            case UnitState.Dead:
                barbarianAnimator.SetMoving(false);
                barbarianAnimator.SetAttacking(false);
                barbarianAnimator.SetDirection(1);
                break;
        }
    }

    protected override void HandleGameStateChanged(GameState newState)
    {
        base.HandleGameStateChanged(newState);

        if (newState == GameState.BattleEnd || newState == GameState.PlayerAPlacement || 
            newState == GameState.GameOver)
        {
            if (isAbilityActive)
            {
                StopAllCoroutines();
                CleanupAllEffects();
                isAbilityActive = false;
            }
        }
    }

    private void CleanupAllEffects()
    {
        if (photonView.IsMine)
        {
            try {
                GameObject[] stunEffects = GameObject.FindGameObjectsWithTag("StunEffect");
                
                foreach (GameObject effect in stunEffects)
                {
                    PhotonView view = effect.GetComponent<PhotonView>();
                    if (view != null && view.IsMine)
                    {
                        PhotonNetwork.Destroy(view);
                    }
                }
            }
            catch (UnityException ex) { }
        }
        
        if (currentStunnedTarget != null && photonView.IsMine)
        {
            ReleaseStunnedTarget();
        }
        
        abilityStarted = false;
    }

    public override void UpdateState(UnitState newState)
    {
        if (currentState == UnitState.Attacking && newState != UnitState.Attacking && isAbilityActive)
        {
            if (photonView.IsMine)
            {
                ReleaseStunnedTarget();
            }
        }
        
        base.UpdateState(newState);
    }

    public override float GetAttackDamage()
    {
        float damage = attackDamage;
        
        if (Random.value < currentCriticalStrikeChance)
        {
            damage *= 1.5f;
        }
        
        if (isAbilityActive && currentStunnedTarget != null)
        {
            damage *= (1 + damageBonus);
        }
        
        return damage;
    }

    protected override void TryActivateAbility()
    {
        if (!photonView.IsMine) return;
        
        if (!isAbilityActive && UnityEngine.Random.value < abilityChance)
        {
            photonView.RPC("RPCActivateAbility", RpcTarget.All);
        }
    }

    [PunRPC]
    protected override void RPCActivateAbility()
    {
        base.RPCActivateAbility();
        
        abilityStarted = true;
        
        if (photonView.IsMine)
        {
            EnemyTargeting targeting = GetComponent<EnemyTargeting>();
            if (targeting != null)
            {
                Transform targetTransform = targeting.GetCurrentTarget();
                if (targetTransform != null)
                {
                    BaseUnit targetUnit = targetTransform.GetComponent<BaseUnit>();
                    if (targetUnit != null && targetUnit.GetCurrentState() != UnitState.Dead)
                    {
                        photonView.RPC("RPCStunTarget", RpcTarget.All, targetUnit.photonView.ViewID);
                    }
                }
            }
        }
    }

    protected override void PerformAbilityActivation()
    {
        if (!abilityStarted)
        {
            abilityStarted = true;
            
            barbarianAnimator.SetAttacking(true);
            
            if (photonView.IsMine)
            {
                EnemyTargeting targeting = GetComponent<EnemyTargeting>();
                if (targeting != null)
                {
                    Transform targetTransform = targeting.GetCurrentTarget();
                    if (targetTransform != null)
                    {
                        BaseUnit targetUnit = targetTransform.GetComponent<BaseUnit>();
                        if (targetUnit != null && targetUnit.GetCurrentState() != UnitState.Dead)
                        {
                            photonView.RPC("RPCStunTarget", RpcTarget.All, targetUnit.photonView.ViewID);
                        }
                    }
                }
            }
        }
    }

    [PunRPC]
    private void RPCStunTarget(int targetViewID)
    {
        PhotonView targetView = PhotonView.Find(targetViewID);
        if (targetView == null) return;
        
        BaseUnit target = targetView.GetComponent<BaseUnit>();
        if (target == null || target.GetCurrentState() == UnitState.Dead) return;
        
        currentStunnedTarget = target;
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = primalStrikeColor;
        }
        
        if (strikeParticles != null)
        {
            var renderer = strikeParticles.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = particlesSortingOrder;
            }
            
            strikeParticles.Play();
        }
        
        StunUnit(target);
        
        if (stunEffectPrefab != null && photonView.IsMine)
        {
            GameObject stunEffect = PhotonNetwork.Instantiate(
                stunEffectPrefab.name,
                target.transform.position + Vector3.up * 0.5f,
                Quaternion.identity
            );
            
            var stunRenderers = stunEffect.GetComponentsInChildren<Renderer>();
            foreach (var renderer in stunRenderers)
            {
                renderer.sortingOrder = particlesSortingOrder;
            }
            
            stunEffect.transform.SetParent(target.transform);
            
            StartCoroutine(DestroyAfterDelay(stunEffect, stunDuration + 0.2f));
        }
        
        if (photonView.IsMine)
        {
            StartCoroutine(ReleaseTargetAfterDelay(stunDuration));
        }
    }

    private void StunUnit(BaseUnit unit)
    {
        int viewID = unit.photonView.ViewID;
        
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            movement.photonView.RPC("RPCStopMovement", RpcTarget.All);
            movement.enabled = false;
        }
        
        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.photonView.RPC("RPCStopTargeting", RpcTarget.All);
            targeting.enabled = false;
        }
        
        CombatSystem combat = unit.GetComponent<CombatSystem>();
        if (combat != null)
        {
            combat.enabled = false;
        }
        
        unit.photonView.RPC("RPCUpdateState", RpcTarget.All, (int)UnitState.Idle);
        
        SpriteRenderer renderer = unit.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = new Color(0.7f, 0.7f, 1.0f);
        }
    }

    private void UnstunUnit(BaseUnit unit)
    {
        if (unit == null) return;
        
        int viewID = unit.photonView.ViewID;
        
        MovementSystem movement = unit.GetComponent<MovementSystem>();
        if (movement != null)
        {
            movement.enabled = true;
        }
        
        EnemyTargeting targeting = unit.GetComponent<EnemyTargeting>();
        if (targeting != null)
        {
            targeting.enabled = true;
            targeting.photonView.RPC("RPCStartTargeting", RpcTarget.All);
        }
        
        CombatSystem combat = unit.GetComponent<CombatSystem>();
        if (combat != null)
        {
            combat.enabled = true;
        }
        
        SpriteRenderer renderer = unit.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = Color.white;
        }
        
        unit.photonView.RPC("RPCUpdateState", RpcTarget.All, (int)UnitState.Idle);
    }

    private IEnumerator ReleaseTargetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReleaseStunnedTarget();
    }

    private void ReleaseStunnedTarget()
    {
        if (!photonView.IsMine) return;
        
        if (currentStunnedTarget != null)
        {
            photonView.RPC("RPCReleaseTarget", RpcTarget.All);
        }
        
        DeactivateAbility();
    }

    [PunRPC]
    private void RPCReleaseTarget()
    {
        if (currentStunnedTarget != null)
        {
            UnstunUnit(currentStunnedTarget);
            currentStunnedTarget = null;
        }
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        
        if (strikeParticles != null)
        {
            strikeParticles.Stop();
        }
        
        abilityStarted = false;
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Destroy(obj);
        }
    }

    [PunRPC]
    protected override void RPCApplyUpgrades(float armorMultiplier, float damageMultiplier, float speedMultiplier, float attackSpeedMultiplier)
    {
        base.RPCApplyUpgrades(armorMultiplier, damageMultiplier, speedMultiplier, attackSpeedMultiplier);
    }

    protected override void DeactivateAbility()
    {
        if (!photonView.IsMine) return;
        
        isAbilityActive = false;
        abilityStarted = false;
        
        base.DeactivateAbility();
    }

    public float GetAbilityCooldownRemaining()
    {
        return Mathf.Max(0, nextAbilityTime - Time.time);
    }
    
    public override void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        base.OnPhotonSerializeView(stream, info);
        
        if (stream.IsWriting)
        {
            int currentDirection = 0;
            if (barbarianAnimator != null && barbarianAnimator.GetComponent<Animator>() != null)
            {
                currentDirection = barbarianAnimator.GetComponent<Animator>().GetInteger("direction");
            }
            stream.SendNext(currentDirection);
        }
        else
        {
            int receivedDirection = (int)stream.ReceiveNext();
            
            if (!photonView.IsMine && barbarianAnimator != null)
            {
                barbarianAnimator.SetDirection(receivedDirection);
            }
        }
    }
}