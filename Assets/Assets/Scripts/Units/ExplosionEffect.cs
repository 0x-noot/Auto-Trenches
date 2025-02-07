using UnityEngine;
using System.Collections;

public class ExplosionEffect : MonoBehaviour, IPooledObject
{
    [Header("Visual Settings")]
    [SerializeField] private ParticleSystem explosionParticles;
    [SerializeField] private float explosionDuration = 0.5f;
    [SerializeField] private AnimationCurve explosionScaleCurve;
    [SerializeField] private float maxScale = 2f;
    
    [Header("Effect Colors")]
    [SerializeField] private Color explosionColor = new Color(1f, 0.5f, 0f, 1f); // Orange
    [SerializeField] private Color sparkColor = new Color(1f, 0.8f, 0f, 1f); // Yellow
    
    private ParticleSystem sparks;

    private void Awake()
    {
        if (explosionParticles == null)
        {
            explosionParticles = GetComponent<ParticleSystem>();
        }
        
        // Create sparks system if not already a child
        if (transform.Find("Sparks") == null)
        {
            CreateSparksSystem();
        }
        else
        {
            sparks = transform.Find("Sparks").GetComponent<ParticleSystem>();
        }
    }

    public void OnObjectSpawn()
    {
        // Reset scale
        transform.localScale = Vector3.one;
        
        // Reset and setup particles
        if (explosionParticles != null)
        {
            explosionParticles.Stop();
            explosionParticles.Clear();
        }
        if (sparks != null)
        {
            sparks.Stop();
            sparks.Clear();
        }

        SetupParticles();
        StartExplosion();
    }

    private void CreateSparksSystem()
    {
        var sparksObj = new GameObject("Sparks");
        sparksObj.transform.SetParent(transform);
        sparksObj.transform.localPosition = Vector3.zero;
        
        sparks = sparksObj.AddComponent<ParticleSystem>();
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

    private void SetupParticles()
    {
        if (explosionParticles != null)
        {
            var main = explosionParticles.main;
            main.startColor = explosionColor;
        }
    }

    private void StartExplosion()
    {
        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }
        if (sparks != null)
        {
            sparks.Play();
        }
        
        StartCoroutine(ExplosionSequence());
    }

    private IEnumerator ExplosionSequence()
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
        
        // Return to pool instead of destroying
        ObjectPool.Instance.ReturnToPool("ExplosionEffect", gameObject);
    }
}