using UnityEngine;

public class MagicProjectile : MonoBehaviour
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

    private void Awake()
    {
        if (spellSprite == null)
            spellSprite = GetComponent<SpriteRenderer>();
            
        if (spellTrail == null)
            spellTrail = GetComponent<TrailRenderer>();
            
        if (particleEffect == null)
            particleEffect = GetComponent<ParticleSystem>();

        SetupVisuals();
        initialSize = transform.localScale.x;
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
        // Rotate the spell
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);

        // Pulsing effect
        time += Time.deltaTime;
        float pulse = 1f + (Mathf.Sin(time * pulseSpeed) * pulseAmount);
        transform.localScale = Vector3.one * initialSize * pulse;
    }

    public void OnSpellHit()
    {
        // Disable trail and sprite
        if (spellTrail != null)
            spellTrail.emitting = false;
        if (spellSprite != null)
            spellSprite.enabled = false;

        // Create hit effect
        CreateHitEffect();
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
}