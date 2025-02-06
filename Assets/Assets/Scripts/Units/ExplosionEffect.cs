using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private ParticleSystem explosionParticles;
    [SerializeField] private float explosionDuration = 0.5f;
    [SerializeField] private AnimationCurve explosionScaleCurve;
    [SerializeField] private float maxScale = 2f;
    
    [Header("Effect Colors")]
    [SerializeField] private Color explosionColor = new Color(1f, 0.5f, 0f, 1f); // Orange
    [SerializeField] private Color sparkColor = new Color(1f, 0.8f, 0f, 1f); // Yellow
    
    private void Start()
    {
        if (explosionParticles == null)
        {
            explosionParticles = GetComponent<ParticleSystem>();
        }
        
        SetupParticles();
        StartExplosion();
    }

    private void SetupParticles()
    {
        if (explosionParticles != null)
        {
            var main = explosionParticles.main;
            main.startColor = explosionColor;
            
            // Create a child particle system for sparks
            var sparks = new GameObject("Sparks").AddComponent<ParticleSystem>();
            sparks.transform.SetParent(transform);
            sparks.transform.localPosition = Vector3.zero;
            
            var sparkMain = sparks.main;
            sparkMain.startColor = sparkColor;
            sparkMain.startLifetime = 0.3f;
            sparkMain.startSpeed = 5f;
            sparkMain.startSize = 0.2f;
            
            var sparkEmission = sparks.emission;
            sparkEmission.rateOverTime = 0;
            sparkEmission.SetBurst(0, new ParticleSystem.Burst(0f, 20));
            
            var sparkShape = sparks.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 0.1f;
        }
    }

    private void StartExplosion()
    {
        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }
        
        StartCoroutine(ExplosionSequence());
    }

    private System.Collections.IEnumerator ExplosionSequence()
    {
        float elapsedTime = 0f;
        Vector3 initialScale = transform.localScale;
        
        while (elapsedTime < explosionDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / explosionDuration;
            
            // Scale the explosion using the animation curve
            float currentScale = explosionScaleCurve.Evaluate(normalizedTime) * maxScale;
            transform.localScale = initialScale * currentScale;
            
            yield return null;
        }
        
        // Cleanup after explosion
        Destroy(gameObject);
    }
}