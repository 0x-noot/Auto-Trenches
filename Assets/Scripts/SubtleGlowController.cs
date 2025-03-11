using UnityEngine;
using UnityEngine.UI;

public class SubtleGlowController : MonoBehaviour
{
    [SerializeField] private Color glowColor = new Color(1.0f, 0.9f, 0.5f, 0.4f); // Subtle gold
    [SerializeField] private float glowPower = 2.5f; // More concentrated glow
    [SerializeField] private float glowIntensity = 0.6f; // Much lower intensity
    [SerializeField] private float glowSpread = 0.0015f; // Less spread
    
    [SerializeField] private Material glowMaterial;
    
    void Start()
    {
        Image image = GetComponent<Image>();
        
        if (glowMaterial != null && image != null)
        {
            // Create an instance of the material
            Material instanceMaterial = Instantiate(glowMaterial);
            
            // Set much more subtle values
            instanceMaterial.SetColor("_GlowColor", glowColor);
            instanceMaterial.SetFloat("_GlowPower", glowPower);
            instanceMaterial.SetFloat("_GlowIntensity", glowIntensity);
            instanceMaterial.SetFloat("_GlowSpread", glowSpread);
            
            // Apply to image
            image.material = instanceMaterial;
        }
    }
}