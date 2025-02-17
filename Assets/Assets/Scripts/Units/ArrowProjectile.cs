using UnityEngine;
using System.Collections;
using Photon.Pun;

public class ArrowProjectile : MonoBehaviourPunCallbacks, IPunObservable, IPooledObject
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
    private Range sourceUnit;
    private BaseUnit targetUnit;
    private int sourceViewID = -1;
    private int targetViewID = -1;

    private void Awake()
    {
        if (arrowSprite == null)
            arrowSprite = GetComponent<SpriteRenderer>();
            
        if (arrowTrail == null)
            arrowTrail = GetComponent<TrailRenderer>();
            
        if (arrowParticles == null)
            arrowParticles = GetComponent<ParticleSystem>();
            
        originalScale = transform.localScale;
    }

    public void Initialize(Range source, BaseUnit target)
    {
        sourceUnit = source;
        targetUnit = target;
        
        if (source != null)
        {
            PhotonView sourceView = source.GetComponent<PhotonView>();
            if (sourceView != null)
                sourceViewID = sourceView.ViewID;
        }
        
        if (target != null)
        {
            PhotonView targetView = target.GetComponent<PhotonView>();
            if (targetView != null)
                targetViewID = targetView.ViewID;
        }

        if (sourceUnit != null && sourceUnit.IsExplosiveArrow())
        {
            SetupExplosiveArrow();
        }
        else
        {
            SetupNormalArrow();
        }
    }

    public void OnObjectSpawn()
    {
        // Reset all components to initial state
        transform.localScale = originalScale;
        isFlying = false;

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
        sourceViewID = -1;
        targetViewID = -1;
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
        isFlying = true;
        if (arrowTrail != null)
            arrowTrail.emitting = true;
        if (arrowParticles != null)
            arrowParticles.Play();
    }

    public void UpdateArrowInFlight(float flightProgress)
    {
        if (!isFlying) return;

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
        if (!isFlying) return;
        
        isFlying = false;

        if (arrowTrail != null)
            arrowTrail.emitting = false;
            
        if (arrowParticles != null)
            arrowParticles.Stop();

        // Try to recover the source unit if we lost it
        if (sourceUnit == null && sourceViewID != -1)
        {
            PhotonView sourceView = PhotonView.Find(sourceViewID);
            if (sourceView != null)
                sourceUnit = sourceView.GetComponent<Range>();
        }

        // Try to recover the target unit if we lost it
        if (targetUnit == null && targetViewID != -1)
        {
            PhotonView targetView = PhotonView.Find(targetViewID);
            if (targetView != null)
                targetUnit = targetView.GetComponent<BaseUnit>();
        }

        if (sourceUnit != null && sourceUnit.IsExplosiveArrow())
        {
            sourceUnit.CreateExplosion(transform.position, targetUnit);
        }

        StartCoroutine(ReturnToPoolAfterDelay());
    }

    private IEnumerator ReturnToPoolAfterDelay()
    {
        yield return new WaitForSeconds(hitEffectDuration);
        ObjectPool.Instance.ReturnToPool("Arrow", gameObject);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send flight data
            stream.SendNext(isFlying);
            stream.SendNext(sourceViewID);
            stream.SendNext(targetViewID);
        }
        else
        {
            // Receive flight data
            isFlying = (bool)stream.ReceiveNext();
            sourceViewID = (int)stream.ReceiveNext();
            targetViewID = (int)stream.ReceiveNext();

            // Try to recover references if needed
            if (sourceUnit == null && sourceViewID != -1)
            {
                PhotonView sourceView = PhotonView.Find(sourceViewID);
                if (sourceView != null)
                    sourceUnit = sourceView.GetComponent<Range>();
            }

            if (targetUnit == null && targetViewID != -1)
            {
                PhotonView targetView = PhotonView.Find(targetViewID);
                if (targetView != null)
                    targetUnit = targetView.GetComponent<BaseUnit>();
            }
        }
    }
}