using UnityEngine;
using System.Collections;
using Photon.Pun;

public class ArrowProjectile : MonoBehaviourPunCallbacks, IPooledObject, IPunObservable
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
    [SerializeField] private float hitEffectDuration = 0.5f;
    
    private Vector3 originalScale;
    private bool isFlying = false;
    private bool isDestroyed = false;
    private Range sourceUnit;
    private BaseUnit targetUnit;
    private float currentFlightProgress = 0f;
    private Vector3 syncedPosition;
    private Quaternion syncedRotation;
    private bool isMoving = false;

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

    public void OnObjectSpawn()
    {
        // Reset all components to initial state
        transform.localScale = originalScale;
        isFlying = false;
        isDestroyed = false;
        currentFlightProgress = 0f;

        if (arrowSprite != null)
        {
            arrowSprite.enabled = true;
            arrowSprite.color = Color.white;
        }

        if (arrowTrail != null)
        {
            arrowTrail.Clear();
            arrowTrail.emitting = true;
        }

        if (arrowParticles != null)
        {
            arrowParticles.Stop();
            arrowParticles.Clear();
        }

        sourceUnit = null;
        targetUnit = null;
        
        // Ensure we're actually active (important for pooling)
        gameObject.SetActive(true);
    }

    public void Initialize(Range source, BaseUnit target)
    {
        if (!photonView.IsMine) return;
        
        sourceUnit = source;
        targetUnit = target;
        
        // Send important info to other clients
        photonView.RPC("RPCInitialize", RpcTarget.Others, 
            (source != null) ? source.photonView.ViewID : -1, 
            (target != null) ? target.photonView.ViewID : -1);
            
        // Set up locally immediately
        if (source != null && source.IsExplosiveArrow())
            SetupExplosiveArrow();
        else
            SetupNormalArrow();
    }

    [PunRPC]
    private void RPCInitialize(int sourceViewID, int targetViewID)
    {
        // Find objects from view IDs
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

        // Setup visuals
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
        
        // Set local state
        isFlying = true;
        
        // Enable trail and particles
        if (arrowTrail != null)
            arrowTrail.emitting = true;
        if (arrowParticles != null)
            arrowParticles.Play();
            
        // Notify other clients
        photonView.RPC("RPCStartFlight", RpcTarget.Others);
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
        
        // Update local state
        currentFlightProgress = flightProgress;
        
        // Update visual properties
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
        float scaleMultiplier = Mathf.Lerp(1f, scaleDuringFlight, flightProgress);
        transform.localScale = originalScale * scaleMultiplier;
        
        // Update particles if needed
        if (arrowParticles != null)
        {
            var emission = arrowParticles.emission;
            emission.rateOverTime = Mathf.Lerp(20f, 10f, flightProgress);
        }
        
        // Save for network sync
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;
    }

    public void OnHit()
    {
        if (!photonView.IsMine) return;
        
        // Set local state
        isFlying = false;
        isDestroyed = true;
        
        // Disable effects
        if (arrowTrail != null)
            arrowTrail.emitting = false;
        if (arrowParticles != null)
            arrowParticles.Stop();
            
        // Handle explosion if needed
        if (sourceUnit != null && sourceUnit.IsExplosiveArrow() && targetUnit != null)
        {
            sourceUnit.CreateExplosion(transform.position, targetUnit);
        }
        
        // Notify other clients
        photonView.RPC("RPCOnHit", RpcTarget.All);
        
        // DESTROY IMMEDIATELY - No delay
        Destroy(gameObject);
    }
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(hitEffectDuration);
        
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    [PunRPC]
    private void RPCOnHit()
    {
        isFlying = false;
        isDestroyed = true;
        
        if (arrowTrail != null)
            arrowTrail.emitting = false;
        if (arrowParticles != null)
            arrowParticles.Stop();
    }

    private IEnumerator ReturnToPoolAfterDelay()
    {
        yield return new WaitForSeconds(hitEffectDuration);
        
        if (photonView.IsMine && gameObject.activeInHierarchy)
        {
            // Make sure we use the correct pool tag
            ObjectPool.Instance.ReturnToPool("ArrowProjectile", gameObject);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Basic state data
            stream.SendNext(isFlying);
            stream.SendNext(isDestroyed);
            stream.SendNext(currentFlightProgress);
            
            // Position data only if flying
            if (isFlying && !isDestroyed)
            {
                stream.SendNext(syncedPosition);
                stream.SendNext(syncedRotation);
            }
        }
        else
        {
            // Receive data
            isFlying = (bool)stream.ReceiveNext();
            isDestroyed = (bool)stream.ReceiveNext();
            currentFlightProgress = (float)stream.ReceiveNext();
            
            // Only update position if flying
            if (isFlying && !isDestroyed)
            {
                syncedPosition = (Vector3)stream.ReceiveNext();
                syncedRotation = (Quaternion)stream.ReceiveNext();
                
                // Smooth lerping for better visual
                transform.position = Vector3.Lerp(transform.position, syncedPosition, Time.deltaTime * 10f);
                transform.rotation = Quaternion.Lerp(transform.rotation, syncedRotation, Time.deltaTime * 10f);
            }
            
            // Update visual state based on received state
            if (arrowTrail != null)
                arrowTrail.emitting = isFlying && !isDestroyed;
                
            if (arrowParticles != null)
            {
                if (isFlying && !isDestroyed && !arrowParticles.isPlaying)
                    arrowParticles.Play();
                else if ((!isFlying || isDestroyed) && arrowParticles.isPlaying)
                    arrowParticles.Stop();
            }
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
        Vector3 direction = (targetPosition - startPos).normalized;
        float distance = Vector3.Distance(startPos, targetPosition);
        float journeyLength = distance;
        float startTime = Time.time;
        float maxTravelTime = 3f; // Max 3 seconds of travel
        
        while (Vector3.Distance(transform.position, targetPosition) > 0.2f && !isDestroyed)
        {
            float distCovered = (Time.time - startTime) * speed;
            float fractionOfJourney = distCovered / journeyLength;
            transform.position = Vector3.Lerp(startPos, targetPosition, fractionOfJourney);
            
            // Rotate arrow to face movement direction
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            // Safety mechanism - if we've been traveling too long, force destroy
            if (Time.time - startTime > maxTravelTime)
            {
                Debug.Log("Arrow exceeded max travel time - destroying");
                Destroy(gameObject);
                yield break;
            }
            
            yield return null;
        }
        
        // When we reach the target, trigger hit and destroy immediately
        if (photonView.IsMine)
        {
            OnHit();
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isFlying = false;
        isDestroyed = false;
        currentFlightProgress = 0f;
    }
}