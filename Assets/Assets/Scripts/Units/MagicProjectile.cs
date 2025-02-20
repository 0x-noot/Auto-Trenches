using UnityEngine;
using System.Collections;
using Photon.Pun;

public class MagicProjectile : PooledObjectBase
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
    private Vector3 syncedPosition;
    private Quaternion syncedRotation;
    private Vector3 syncedScale;

    protected override void Awake()
    {
        base.Awake();

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

    public override void OnObjectSpawn()
    {
        // Reset components
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
        }
    }

    private void Update()
    {
        if (!isActive) return;

        if (photonView.IsMine)
        {
            // Update position and rotation on owner
            transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
            time += Time.deltaTime;
            float pulse = 1f + (Mathf.Sin(time * pulseSpeed) * pulseAmount);
            transform.localScale = Vector3.one * initialSize * pulse;
            
            // Store synced values
            syncedPosition = transform.position;
            syncedRotation = transform.rotation;
            syncedScale = transform.localScale;
        }
        else
        {
            // Smooth interpolation for non-owners
            transform.position = Vector3.Lerp(transform.position, syncedPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, syncedRotation, Time.deltaTime * 10f);
            transform.localScale = Vector3.Lerp(transform.localScale, syncedScale, Time.deltaTime * 10f);
        }
    }

    public void OnSpellHit()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCOnSpellHit", RpcTarget.All);
    }

    [PunRPC]
    private void RPCOnSpellHit()
    {
        if (!isActive) return;

        // Disable trail and sprite
        if (spellTrail != null)
            spellTrail.emitting = false;
        if (spellSprite != null)
            spellSprite.enabled = false;

        // Create hit effect
        CreateHitEffect();

        // Return to pool after delay
        StartCoroutine(ReturnToPoolAfterDelay());
    }

    private void CreateHitEffect()
    {
        // Spawn a particle burst
        if (particleEffect != null)
        {
            particleEffect.Stop();
            var burstParams = new ParticleSystem.Burst(0f, 20);
            var emission = particleEffect.emission;
            emission.SetBurst(0, burstParams);
            particleEffect.Play();
        }
    }

    private IEnumerator ReturnToPoolAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        
        if (photonView.IsMine)
        {
            ObjectPool.Instance.ReturnToPool("SpellProjectile", gameObject);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(isActive);
            stream.SendNext(time);
            stream.SendNext(syncedPosition);
            stream.SendNext(syncedRotation);
            stream.SendNext(syncedScale);
        }
        else
        {
            // Receive data
            isActive = (bool)stream.ReceiveNext();
            time = (float)stream.ReceiveNext();
            syncedPosition = (Vector3)stream.ReceiveNext();
            syncedRotation = (Quaternion)stream.ReceiveNext();
            syncedScale = (Vector3)stream.ReceiveNext();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAllCoroutines();
        time = 0f;
    }
}