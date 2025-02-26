using UnityEngine;
using System.Collections;
using Photon.Pun;

public class ArrowProjectile : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Visual Components")]
    [SerializeField] private SpriteRenderer arrowSprite;
    [SerializeField] private TrailRenderer arrowTrail;
    [SerializeField] private ParticleSystem arrowParticles;
    
    [Header("Trail Settings")]
    [SerializeField] private float trailTime = 0.2f;
    [SerializeField] private Color trailStartColor = Color.white;
    [SerializeField] private Color trailEndColor = new Color(1, 1, 1, 0);

    [Header("Explosive Arrow Settings")]
    [SerializeField] private Color explosiveTrailColor = Color.red;
    [SerializeField] private ParticleSystem explosiveParticles;
    
    [Header("Flight Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float scaleDuringFlight = 1.2f;
    
    private Vector3 velocityRef = Vector3.zero;

    private Vector3 originalScale;
    private bool isFlying = false;
    private bool isDestroyed = false;
    private Range sourceUnit;
    private BaseUnit targetUnit;
    private float currentFlightProgress = 0f;
    private bool isMoving = false;

    // Network sync variables
    private Vector3 syncedPosition;
    private Quaternion syncedRotation;
    private float interpolationSpeed = 15f;
    private float syncInterval = 0.1f;
    private float lastSyncTime = 0f;

    private void Awake()
    {
        if (arrowSprite == null)
            arrowSprite = GetComponent<SpriteRenderer>();
            
        if (arrowTrail == null)
            arrowTrail = GetComponent<TrailRenderer>();
            
        if (arrowParticles == null)
            arrowParticles = GetComponent<ParticleSystem>();
            
        originalScale = transform.localScale;
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;
    }

    private void Update()
    {
        if (!photonView.IsMine && isFlying && !isDestroyed)
        {
            // Use SmoothDamp for position - much smoother than Lerp
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                syncedPosition, 
                ref velocityRef, 
                0.1f,  // Smoothing time
                interpolationSpeed * 2f  // Max speed
            );
            
            // Use Slerp for rotation - better for rotations than Lerp
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                syncedRotation, 
                Time.deltaTime * interpolationSpeed * 2f
            );
            
            // Add leading adjustment - predict a bit ahead based on network delay
            if (Time.frameCount % 30 == 0)  // Only recalculate occasionally to save performance
            {
                float ping = PhotonNetwork.GetPing() / 1000f;  // Convert to seconds
                Vector3 projectedPosition = syncedPosition + (velocityRef * ping);
                
                // Apply a slight pull toward the projected position
                transform.position = Vector3.Lerp(transform.position, projectedPosition, 0.2f);
            }
        }
    }

    public void Initialize(Range source, BaseUnit target)
    {
        if (!photonView.IsMine) return;
        
        sourceUnit = source;
        targetUnit = target;
        
        // Only proceed if target is valid and alive
        if (target == null || target.GetCurrentState() == UnitState.Dead)
        {
            OnHit(); // Destroy projectile if target is invalid
            return;
        }
        
        photonView.RPC("RPCInitialize", RpcTarget.Others, 
            (source != null) ? source.photonView.ViewID : -1, 
            (target != null) ? target.photonView.ViewID : -1);
                
        if (source != null && source.IsExplosiveArrow())
            SetupExplosiveArrow();
        else
            SetupNormalArrow();
    }

    [PunRPC]
    private void RPCInitialize(int sourceViewID, int targetViewID)
    {
        if (sourceViewID != -1)
        {
            PhotonView sourceView = PhotonView.Find(sourceViewID);
            if (sourceView != null)
                sourceUnit = sourceView.GetComponent<Range>();
        }
        
        if (targetViewID != -1)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null)
                targetUnit = targetView.GetComponent<BaseUnit>();
        }

        if (sourceUnit != null && sourceUnit.IsExplosiveArrow())
            SetupExplosiveArrow();
        else
            SetupNormalArrow();
    }

    private void SetupExplosiveArrow()
    {
        if (arrowTrail != null)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(explosiveTrailColor, 0.0f), 
                    new GradientColorKey(Color.yellow, 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            arrowTrail.colorGradient = gradient;
        }

        if (explosiveParticles != null)
        {
            explosiveParticles.Play();
        }
    }

    private void SetupNormalArrow()
    {
        SetupTrail();
        SetupParticles();
    }

    private void SetupTrail()
    {
        if (arrowTrail != null)
        {
            arrowTrail.time = trailTime;
            
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(trailStartColor, 0.0f), 
                    new GradientColorKey(trailEndColor, 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            arrowTrail.colorGradient = gradient;
        }
    }

    private void SetupParticles()
    {
        if (arrowParticles != null)
        {
            var main = arrowParticles.main;
            main.startColor = trailStartColor;
            arrowParticles.Play();
        }
    }

    public void StartFlight()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCStartFlight", RpcTarget.All);
    }

    [PunRPC]
    private void RPCStartFlight()
    {
        isFlying = true;
        if (arrowTrail != null)
            arrowTrail.emitting = true;
        if (arrowParticles != null)
            arrowParticles.Play();
    }

    public void UpdateArrowInFlight(float flightProgress)
    {
        if (!photonView.IsMine) return;
        
        currentFlightProgress = flightProgress;
        
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
        float scaleMultiplier = Mathf.Lerp(1f, scaleDuringFlight, flightProgress);
        transform.localScale = originalScale * scaleMultiplier;
        
        if (arrowParticles != null)
        {
            var emission = arrowParticles.emission;
            emission.rateOverTime = Mathf.Lerp(20f, 10f, flightProgress);
        }

        // Update synced values if enough time has passed
        if (Time.time - lastSyncTime >= syncInterval)
        {
            syncedPosition = transform.position;
            syncedRotation = transform.rotation;
            lastSyncTime = Time.time;
        }
    }

    public void OnHit()
    {
        if (!photonView.IsMine) return;
        
        isFlying = false;
        isDestroyed = true;
        
        // Disable effects immediately
        if (arrowSprite != null)
            arrowSprite.enabled = false;
        if (arrowTrail != null)
        {
            arrowTrail.emitting = false;
            arrowTrail.Clear();
        }
        if (arrowParticles != null)
        {
            arrowParticles.Stop();
            arrowParticles.Clear();
        }
                
        // Handle explosion if needed
        if (sourceUnit != null && sourceUnit.IsExplosiveArrow() && targetUnit != null)
        {
             Debug.Log($"Calling CreateExplosion on sourceUnit: {sourceUnit.gameObject.name}");
            sourceUnit.CreateExplosion(transform.position, targetUnit);
        }
        else
        {
            Debug.Log($"Explosion conditions not met: sourceUnit={sourceUnit != null}, " +
                    $"isExplosive={sourceUnit?.IsExplosiveArrow()}, targetUnit={targetUnit != null}");
        }
        
        // Notify other clients
        photonView.RPC("RPCOnHit", RpcTarget.All);
        
        // Immediate destruction
        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    private void RPCOnHit()
    {
        isFlying = false;
        isDestroyed = true;
        
        if (arrowSprite != null)
            arrowSprite.enabled = false;
        if (arrowTrail != null)
        {
            arrowTrail.emitting = false;
            arrowTrail.Clear();
        }
        if (arrowParticles != null)
        {
            arrowParticles.Stop();
            arrowParticles.Clear();
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
        Vector3 finalTargetPosition = targetPosition; // Store initial target position
        Vector3 direction = (finalTargetPosition - startPos).normalized;
        float distance = Vector3.Distance(startPos, finalTargetPosition);
        float journeyLength = distance;
        float startTime = Time.time;
        float maxTravelTime = 3f;
        
        while (Vector3.Distance(transform.position, finalTargetPosition) > 0.2f && !isDestroyed)
        {
            // Even if target dies, keep moving to the stored position
            float distCovered = (Time.time - startTime) * speed;
            float fractionOfJourney = distCovered / journeyLength;
            transform.position = Vector3.Lerp(startPos, finalTargetPosition, fractionOfJourney);
            
            // Rotate to face movement direction
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            // Update synced values if enough time has passed
            if (Time.time - lastSyncTime >= syncInterval)
            {
                syncedPosition = transform.position;
                syncedRotation = transform.rotation;
                lastSyncTime = Time.time;
            }
            
            // Safety check with warning
            if (Time.time - startTime > maxTravelTime)
            {
                Debug.LogWarning($"Projectile exceeded max travel time. Distance to target: {Vector3.Distance(transform.position, finalTargetPosition)}");
                OnHit();
                yield break;
            }
            
            yield return null;
        }
        
        if (photonView.IsMine)
        {
            OnHit();
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(isFlying);
            stream.SendNext(isDestroyed);
            stream.SendNext(currentFlightProgress);
            
            // Add velocity calculation based on last position
            Vector3 velocity = (transform.position - syncedPosition) / syncInterval;
            stream.SendNext(velocity);
        }
        else
        {
            syncedPosition = (Vector3)stream.ReceiveNext();
            syncedRotation = (Quaternion)stream.ReceiveNext();
            isFlying = (bool)stream.ReceiveNext();
            isDestroyed = (bool)stream.ReceiveNext();
            currentFlightProgress = (float)stream.ReceiveNext();
            
            // Receive velocity
            Vector3 incomingVelocity = (Vector3)stream.ReceiveNext();
            velocityRef = incomingVelocity;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isFlying = false;
        isDestroyed = false;
        currentFlightProgress = 0f;
        isMoving = false;
    }
}