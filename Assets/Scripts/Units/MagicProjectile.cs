using UnityEngine;
using System.Collections;
using Photon.Pun;

public class MagicProjectile : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer spellSprite;
    [SerializeField] private TrailRenderer spellTrail;
    [SerializeField] private ParticleSystem particleEffect;
    
    [Header("Spell Settings")]
    [SerializeField] private Color spellColor = new Color(0.5f, 0f, 1f, 1f);
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.2f;
    
    private float initialSize;
    private float time;
    private bool isActive = false;
    private bool isDestroyed = false;
    private bool isMoving = false;

    // Network sync variables
    private Vector3 syncedPosition;
    private Quaternion syncedRotation;
    private Vector3 syncedScale;
    private float interpolationSpeed = 15f;
    private float syncInterval = 0.1f;
    private float lastSyncTime = 0f;
    
    // Improved sync variables
    private Vector3 velocityRef = Vector3.zero;
    private Vector3 scaleVelocityRef = Vector3.zero;
    private Vector3 lastSyncedPosition;
    private float timeSinceLastPositionUpdate = 0f;
    private bool hasReceivedFirstUpdate = false;

    private void Awake()
    {
        if (spellSprite == null)
            spellSprite = GetComponent<SpriteRenderer>();
            
        if (spellTrail == null)
            spellTrail = GetComponent<TrailRenderer>();
            
        if (particleEffect == null)
            particleEffect = GetComponent<ParticleSystem>();

        initialSize = transform.localScale.x;
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;
        syncedScale = transform.localScale;
        lastSyncedPosition = transform.position;
    }

    private void Update()
    {
        if (!photonView.IsMine && isActive && !isDestroyed)
        {
            // Track time since last position update
            timeSinceLastPositionUpdate += Time.deltaTime;
            
            // If this is our first update, snap to the position
            if (!hasReceivedFirstUpdate)
            {
                if (syncedPosition != Vector3.zero)
                {
                    transform.position = syncedPosition;
                    lastSyncedPosition = syncedPosition;
                    hasReceivedFirstUpdate = true;
                }
                return;
            }
            
            // Calculate estimated velocity based on position changes
            Vector3 estimatedVelocity = (syncedPosition - lastSyncedPosition) / syncInterval;
            
            // If the estimated velocity is very high, something might be wrong - limit it
            if (estimatedVelocity.magnitude > 30f)
            {
                estimatedVelocity = estimatedVelocity.normalized * 30f;
            }
            
            // Calculate a more realistic target position based on time passed
            Vector3 projectedPosition = lastSyncedPosition + (estimatedVelocity * timeSinceLastPositionUpdate);
            
            // Gradually blend between current position and projected position
            transform.position = Vector3.SmoothDamp(
                transform.position,
                projectedPosition,
                ref velocityRef,
                0.08f, // Faster smoothing time
                40f    // Higher max speed
            );
            
            // Rotation with Slerp
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                syncedRotation,
                Time.deltaTime * 15f // Higher rotation speed
            );
            
        // Keep a consistent base scale
        Vector3 baseScale = Vector3.one * initialSize;

        // Apply pulse effect to the base scale
        if (pulseAmount > 0)
        {
            float pulse = 1f + (Mathf.Sin(time * pulseSpeed) * pulseAmount);
            transform.localScale = baseScale * pulse;
        }
        else
        {
            // If no pulse is desired, just set the scale directly
            transform.localScale = baseScale;
        }
            
            // Update time
            time += Time.deltaTime;
        }
    }

    // This method is called automatically when the object is instantiated
    private void Start()
    {
        isDestroyed = false;
        isActive = true;
        time = 0f;
        transform.localScale = Vector3.one * initialSize;
        
        if (spellSprite != null)
        {
            spellSprite.enabled = true;
            spellSprite.color = spellColor;
        }

        if (spellTrail != null)
        {
            spellTrail.Clear();
            spellTrail.emitting = true;
        }

        if (particleEffect != null)
        {
            particleEffect.Stop();
            particleEffect.Clear();
        }

        SetupVisuals();
    }

    private void SetupVisuals()
    {
        // Set sprite color
        if (spellSprite != null)
        {
            spellSprite.color = spellColor;
        }

        // Setup trail
        if (spellTrail != null)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(spellColor, 0.0f), 
                    new GradientColorKey(new Color(spellColor.r, spellColor.g, spellColor.b, 0f), 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.7f, 0.0f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            spellTrail.colorGradient = gradient;
        }

        // Setup particles
        if (particleEffect != null)
        {
            var main = particleEffect.main;
            main.startColor = spellColor;
            particleEffect.Play();
        }
    }

    public void MoveToTarget(Vector3 targetPosition, float speed)
    {
        if (isMoving) return;
        isMoving = true;
        StartCoroutine(MoveCoroutine(targetPosition, speed));
    }

    private IEnumerator MoveCoroutine(Vector3 targetPosition, float speed)
    {
        Vector3 startPos = transform.position;
        Vector3 finalTargetPosition = targetPosition;
        float distance = Vector3.Distance(startPos, finalTargetPosition);
        float journeyLength = distance;
        float startTime = Time.time;
        
        // Use fixed timestep for more consistent movement
        float timeStep = 0.02f; // 50 updates per second
        float elapsedTime = 0f;
        
        while (elapsedTime < journeyLength/speed && !isDestroyed)
        {
            elapsedTime += timeStep;
            float fractionOfJourney = elapsedTime / (journeyLength/speed);
            fractionOfJourney = Mathf.Clamp01(fractionOfJourney);
            
            // Use smooth step for more natural acceleration/deceleration
            float t = Mathf.SmoothStep(0, 1, fractionOfJourney);
            transform.position = Vector3.Lerp(startPos, finalTargetPosition, t);
            
            // Make direction face movement
            if (finalTargetPosition != startPos)
            {
                Vector3 direction = (finalTargetPosition - startPos).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            
            // Update synced values at regular intervals
            if (Time.time - lastSyncTime >= syncInterval)
            {
                syncedPosition = transform.position;
                syncedRotation = transform.rotation;
                lastSyncTime = Time.time;
            }
            
            yield return new WaitForSeconds(timeStep);
        }
        
        // Ensure arrival at exact destination
        transform.position = finalTargetPosition;
        
        if (photonView.IsMine)
        {
            OnSpellHit();
        }
    }

    public void OnSpellHit()
    {
        if (!photonView.IsMine || isDestroyed) return;
        
        isDestroyed = true;
        
        // Disable visuals immediately
        if (spellSprite != null)
            spellSprite.enabled = false;
        if (spellTrail != null)
        {
            spellTrail.emitting = false;
            spellTrail.Clear();
        }
        if (particleEffect != null)
        {
            particleEffect.Stop();
            particleEffect.Clear();
        }
        
        // Create hit effect
        CreateHitEffect();
        
        // Notify other clients
        photonView.RPC("RPCOnSpellHit", RpcTarget.All);
        
        // Immediate destruction
        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    private void RPCOnSpellHit()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Disable visuals immediately
        if (spellSprite != null)
            spellSprite.enabled = false;
        if (spellTrail != null)
        {
            spellTrail.emitting = false;
            spellTrail.Clear();
        }
        if (particleEffect != null)
        {
            particleEffect.Stop();
            particleEffect.Clear();
        }

        CreateHitEffect();
    }

    private void CreateHitEffect()
    {
        if (particleEffect != null && !isDestroyed)
        {
            // Create a burst effect
            var burstParams = new ParticleSystem.Burst(0f, 20);
            var emission = particleEffect.emission;
            emission.SetBurst(0, burstParams);
            particleEffect.Play();
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Writing data
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(isActive);
            stream.SendNext(isDestroyed);
            stream.SendNext(time);
        }
        else
        {
            // Store the last position before updating
            lastSyncedPosition = syncedPosition;
            
            // Receive data
            syncedPosition = (Vector3)stream.ReceiveNext();
            syncedRotation = (Quaternion)stream.ReceiveNext();
            isActive = (bool)stream.ReceiveNext();
            isDestroyed = (bool)stream.ReceiveNext();
            time = (float)stream.ReceiveNext();
            
            // Reset time counter since last update
            timeSinceLastPositionUpdate = 0f;
            hasReceivedFirstUpdate = true;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isActive = false;
        isDestroyed = false;
        isMoving = false;
        time = 0f;
    }
}