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
    }

    private void Update()
    {
        if (!photonView.IsMine && isActive && !isDestroyed)
        {
            // Smooth interpolation for non-owners
            transform.position = Vector3.Lerp(transform.position, syncedPosition, Time.deltaTime * interpolationSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, syncedRotation, Time.deltaTime * interpolationSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, syncedScale, Time.deltaTime * interpolationSpeed);
            
            // Update visual effects
            if (isActive && !isDestroyed)
            {
                time += Time.deltaTime;
                float pulse = 1f + (Mathf.Sin(time * pulseSpeed) * pulseAmount);
                transform.localScale = Vector3.one * initialSize * pulse;
            }
        }
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
                OnSpellHit();
                yield break;
            }
            
            yield return null;
        }
        
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
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(transform.localScale);
            stream.SendNext(isActive);
            stream.SendNext(isDestroyed);
            stream.SendNext(time);
        }
        else
        {
            syncedPosition = (Vector3)stream.ReceiveNext();
            syncedRotation = (Quaternion)stream.ReceiveNext();
            syncedScale = (Vector3)stream.ReceiveNext();
            isActive = (bool)stream.ReceiveNext();
            isDestroyed = (bool)stream.ReceiveNext();
            time = (float)stream.ReceiveNext();
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