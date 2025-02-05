using UnityEngine;

public class ArrowProjectile : MonoBehaviour
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
        // Get or add required components
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

    private void SetupExplosiveArrow()
    {
        if (arrowTrail != null)
        {
            // Create a gradient for the explosive trail
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

        // Setup explosive particles
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
            
            // Create a gradient for the trail
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

        // Create explosion if this is an explosive arrow
        if (sourceUnit != null && sourceUnit.IsExplosiveArrow())
        {
            sourceUnit.CreateExplosion(transform.position, targetUnit);
        }

        StartFadeOut();
    }

    private void StartFadeOut()
    {
        if (arrowSprite != null)
        {
            Color startColor = arrowSprite.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
            StartCoroutine(FadeSprite(startColor, endColor, hitEffectDuration));
        }
    }

    private System.Collections.IEnumerator FadeSprite(Color startColor, Color endColor, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (arrowSprite != null)
            {
                arrowSprite.color = Color.Lerp(startColor, endColor, elapsed / duration);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}