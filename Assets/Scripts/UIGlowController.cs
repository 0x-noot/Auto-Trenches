using UnityEngine;
using UnityEngine.UI;

public class UIGlowController : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(1.0f, 0.8f, 0.2f, 0.9f); // Golden glow
    [SerializeField] private float minIntensity = 0.7f;
    [SerializeField] private float maxIntensity = 2.0f; // Increased intensity
    [SerializeField] private float glowPower = 1.5f; // Reduced for wider glow
    [SerializeField] private float pulseSpeed = 0.8f;
    
    [SerializeField] private Material glowMaterial;
    
    void Start()
    {
        Image image = GetComponent<Image>();
        
        if (glowMaterial != null && image != null)
        {
            // Create an instance of the material to avoid modifying the original
            Material instanceMaterial = Instantiate(glowMaterial);
            
            // Set initial values
            instanceMaterial.SetColor("_GlowColor", glowColor);
            instanceMaterial.SetFloat("_GlowPower", glowPower);
            instanceMaterial.SetFloat("_GlowIntensity", minIntensity);
            
            // Apply to image
            image.material = instanceMaterial;
        }
    }
    
    void Update()
    {
        Image image = GetComponent<Image>();
        if (image != null && image.material != null)
        {
            // Animate the glow intensity
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f);
            image.material.SetFloat("_GlowIntensity", intensity);
        }
    }
}