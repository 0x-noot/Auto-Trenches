using UnityEngine;
using System.Collections;
using Photon.Pun;

public class MagicProjectile : MonoBehaviourPunCallbacks, IPooledObject, IPunObservable
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
    [SerializeField] private float returnDelay = 0.5f;
    
    private float initialSize;
    private float time;
    private bool isActive = false;
    private bool isDestroyed = false;
    private Vector3 syncedPosition;
    private Quaternion syncedRotation;
    private Vector3 syncedScale;
    private bool isMoving = false;

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

    public void OnObjectSpawn()
    {
        // Reset components
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

    public void MoveToTarget(Vector3 targetPosition, float speed)
    {
        if (isMoving) return;
        isMoving = true;
        StartCoroutine(MoveCoroutine(targetPosition, speed));
    }

    private IEnumerator MoveCoroutine(Vector3 targetPosition, float speed)
    {
        Vector3 startPos = transform.position;
        float distance = Vector3.Distance(startPos, targetPosition);
        float journeyLength = distance;
        float startTime = Time.time;
        float maxTravelTime = 3f; // Max 3 seconds of travel
        
        while (Vector3.Distance(transform.position, targetPosition) > 0.2f && !isDestroyed)
        {
            float distCovered = (Time.time - startTime) * speed;
            float fractionOfJourney = distCovered / journeyLength;
            transform.position = Vector3.Lerp(startPos, targetPosition, fractionOfJourney);
            
            // Continue rotating and pulsing during movement
            transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
            time += Time.deltaTime;
            float pulse = 1f + (Mathf.Sin(time * pulseSpeed) * pulseAmount);
            transform.localScale = Vector3.one * initialSize * pulse;
            
            // Store synced values
            syncedPosition = transform.position;
            syncedRotation = transform.rotation;
            syncedScale = transform.localScale;
            
            // Safety mechanism - if we've been traveling too long, force destroy
            if (Time.time - startTime > maxTravelTime)
            {
                Debug.Log("Spell exceeded max travel time - destroying");
                Destroy(gameObject);
                yield break;
            }
            
            yield return null;
        }
        
        // When we reach the target, hit and destroy immediately
        if (photonView.IsMine)
        {
            OnSpellHit();
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
        }
    }

    private void Update()
    {
        if (!isActive || isDestroyed) return;

        if (!photonView.IsMine)
        {
            // Smooth interpolation for non-owners
            transform.position = Vector3.Lerp(transform.position, syncedPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, syncedRotation, Time.deltaTime * 10f);
            transform.localScale = Vector3.Lerp(transform.localScale, syncedScale, Time.deltaTime * 10f);
        }
    }

    public void OnSpellHit()
    {
        if (!photonView.IsMine || isDestroyed) return;
        
        isDestroyed = true;
        
        // Disable effects
        if (spellTrail != null)
            spellTrail.emitting = false;
        if (spellSprite != null)
            spellSprite.enabled = false;
        
        // Create hit effect if needed
        CreateHitEffect();
        
        // Notify other clients
        photonView.RPC("RPCOnSpellHit", RpcTarget.All);
        
        // DESTROY IMMEDIATELY - No delay
        Destroy(gameObject);
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    [PunRPC]
    private void RPCOnSpellHit()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Disable trail and sprite
        if (spellTrail != null)
            spellTrail.emitting = false;
        if (spellSprite != null)
            spellSprite.enabled = false;

        // Create hit effect
        CreateHitEffect();
        
        // Destroy after delay
        Destroy(gameObject, returnDelay);
    }

    private void CreateHitEffect()
    {
        // Spawn a particle burst
        if (particleEffect != null && !isDestroyed)
        {
            particleEffect.Stop();
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
            // Send data
            stream.SendNext(isActive);
            stream.SendNext(isDestroyed);
            stream.SendNext(time);
            stream.SendNext(syncedPosition);
            stream.SendNext(syncedRotation);
            stream.SendNext(syncedScale);
        }
        else
        {
            // Receive data
            isActive = (bool)stream.ReceiveNext();
            isDestroyed = (bool)stream.ReceiveNext();
            time = (float)stream.ReceiveNext();
            syncedPosition = (Vector3)stream.ReceiveNext();
            syncedRotation = (Quaternion)stream.ReceiveNext();
            syncedScale = (Vector3)stream.ReceiveNext();
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isActive = false;
        isDestroyed = false;
        time = 0f;
    }
}