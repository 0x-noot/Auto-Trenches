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
    
    [Header("Flight Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float scaleDuringFlight = 1.2f;
    [SerializeField] private float hitEffectDuration = 0.5f;
    
    private Vector3 originalScale;
    private bool isFlying = false;

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
            
            // Start the particle system
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

        // Rotate arrow during flight
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);

        // Scale effect during flight
        float scaleMultiplier = Mathf.Lerp(1f, scaleDuringFlight, flightProgress);
        transform.localScale = originalScale * scaleMultiplier;

        // Update particle systems
        if (arrowParticles != null)
        {
            var emission = arrowParticles.emission;
            emission.rateOverTime = Mathf.Lerp(20f, 10f, flightProgress);
        }
    }

    public void OnHit()
    {
        isFlying = false;

        // Stop trail effect
        if (arrowTrail != null)
        {
            arrowTrail.emitting = false;
        }

        // Stop flight particles
        if (arrowParticles != null)
        {
            arrowParticles.Stop();
        }

        // Start fade out
        StartFadeOut();
    }

    private void StartFadeOut()
    {
        if (arrowSprite != null)
        {
            // Fade out the sprite
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

        // Destroy the arrow after fade out
        Destroy(gameObject);
    }
}