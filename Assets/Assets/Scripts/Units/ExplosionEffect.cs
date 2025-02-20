using UnityEngine;
using System.Collections;
using Photon.Pun;

public class ExplosionEffect : PooledObjectBase
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
    private float explosionProgress = 0f;
    private Coroutine explosionCoroutine;

    protected override void Awake()
    {
        base.Awake();

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

    public override void OnObjectSpawn()
    {
        // Reset scale and progress
        transform.localScale = Vector3.one;
        explosionProgress = 0f;
        
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
        
        if (photonView.IsMine)
        {
            photonView.RPC("RPCStartExplosion", RpcTarget.All);
        }
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

    [PunRPC]
    private void RPCStartExplosion()
    {
        isActive = true;
        explosionProgress = 0f;
        
        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }
        if (sparks != null)
        {
            sparks.Play();
        }
        
        if (explosionCoroutine != null)
        {
            StopCoroutine(explosionCoroutine);
        }
        explosionCoroutine = StartCoroutine(ExplosionSequence());
    }

    private IEnumerator ExplosionSequence()
    {
        float elapsedTime = 0f;
        Vector3 initialScale = transform.localScale;
        
        while (elapsedTime < explosionDuration)
        {
            elapsedTime += Time.deltaTime;
            explosionProgress = elapsedTime / explosionDuration;
            
            // Scale the explosion using the animation curve
            float currentScale = explosionScaleCurve.Evaluate(explosionProgress) * maxScale;
            transform.localScale = initialScale * currentScale;
            
            yield return null;
        }

        isActive = false;
        explosionProgress = 1f;
        
        // Return to pool
        if (photonView.IsMine)
        {
            ObjectPool.Instance.ReturnToPool("ExplosionEffect", gameObject);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isActive);
            stream.SendNext(explosionProgress);
        }
        else
        {
            isActive = (bool)stream.ReceiveNext();
            explosionProgress = (float)stream.ReceiveNext();

            // Update scale based on received progress
            if (isActive)
            {
                float currentScale = explosionScaleCurve.Evaluate(explosionProgress) * maxScale;
                transform.localScale = Vector3.one * currentScale;
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (explosionCoroutine != null)
        {
            StopCoroutine(explosionCoroutine);
            explosionCoroutine = null;
        }
    }
}