using UnityEngine;
using System.Collections;
using Photon.Pun;

public class ArrowProjectile : PooledObjectBase
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
    private Range sourceUnit;
    private BaseUnit targetUnit;
    private float currentFlightProgress = 0f;

    protected override void Awake()
    {
        base.Awake();

        if (arrowSprite == null)
            arrowSprite = GetComponent<SpriteRenderer>();
            
        if (arrowTrail == null)
            arrowTrail = GetComponent<TrailRenderer>();
            
        if (arrowParticles == null)
            arrowParticles = GetComponent<ParticleSystem>();
            
        originalScale = transform.localScale;
    }

    public override void OnObjectSpawn()
    {
        // Reset all components to initial state
        transform.localScale = originalScale;
        currentFlightProgress = 0f;
        isActive = false;

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
    }

    public void Initialize(Range source, BaseUnit target)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCInitialize", RpcTarget.All, source.photonView.ViewID, target.photonView.ViewID);
    }

    [PunRPC]
    private void RPCInitialize(int sourceViewID, int targetViewID)
    {
        PhotonView sourceView = PhotonView.Find(sourceViewID);
        PhotonView targetView = PhotonView.Find(targetViewID);

        if (sourceView != null && targetView != null)
        {
            sourceUnit = sourceView.GetComponent<Range>();
            targetUnit = targetView.GetComponent<BaseUnit>();

            if (sourceUnit != null && sourceUnit.IsExplosiveArrow())
            {
                SetupExplosiveArrow();
            }
            else
            {
                SetupNormalArrow();
            }
        }
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
        isActive = true;
        if (arrowTrail != null)
            arrowTrail.emitting = true;
        if (arrowParticles != null)
            arrowParticles.Play();
    }

    public void UpdateArrowInFlight(float flightProgress)
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCUpdateArrowInFlight", RpcTarget.All, flightProgress);
    }

    [PunRPC]
    private void RPCUpdateArrowInFlight(float flightProgress)
    {
        if (!isActive) return;

        currentFlightProgress = flightProgress;
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
        float scaleMultiplier = Mathf.Lerp(1f, scaleDuringFlight, flightProgress);
        transform.localScale = originalScale * scaleMultiplier;

        if (arrowParticles != null)
        {
            var emission = arrowParticles.emission;
            emission.rateOverTime = Mathf.Lerp(20f, 10f, flightProgress);
        }
    }

    public void OnHit()
    {
        if (!photonView.IsMine) return;
        photonView.RPC("RPCOnHit", RpcTarget.All);
    }

    [PunRPC]
    private void RPCOnHit()
    {
        if (!isActive) return;
        
        isActive = false;

        if (arrowTrail != null)
            arrowTrail.emitting = false;
            
        if (arrowParticles != null)
            arrowParticles.Stop();

        if (sourceUnit != null && sourceUnit.IsExplosiveArrow())
        {
            sourceUnit.CreateExplosion(transform.position, targetUnit);
        }

        StartCoroutine(ReturnToPoolAfterDelay());
    }

    private IEnumerator ReturnToPoolAfterDelay()
    {
        yield return new WaitForSeconds(hitEffectDuration);
        
        if (photonView.IsMine)
        {
            ObjectPool.Instance.ReturnToPool("ArrowProjectile", gameObject);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(isActive);
            stream.SendNext(currentFlightProgress);
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(transform.localScale);
        }
        else
        {
            // Receive data
            isActive = (bool)stream.ReceiveNext();
            currentFlightProgress = (float)stream.ReceiveNext();
            transform.position = (Vector3)stream.ReceiveNext();
            transform.rotation = (Quaternion)stream.ReceiveNext();
            transform.localScale = (Vector3)stream.ReceiveNext();
        }
    }
}