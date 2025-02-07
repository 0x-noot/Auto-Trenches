using UnityEngine;
using System.Collections;

public class ArrowProjectile : MonoBehaviour, IPooledObject
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
        isFlying = false;

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
        ObjectPool.Instance.ReturnToPool("Arrow", gameObject);
    }
}