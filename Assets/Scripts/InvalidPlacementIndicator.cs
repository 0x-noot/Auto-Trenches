using UnityEngine;
using System.Collections;

public class InvalidPlacementIndicator : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 0.1f;
    [SerializeField] private float holdDuration = 0.3f; 
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private Color indicatorColor = new Color(1f, 0.3f, 0.3f, 0.7f); // Red with transparency
    
    private SpriteRenderer spriteRenderer;
    
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // Set up the sprite renderer
        spriteRenderer.sprite = CreateCircleSprite();
        spriteRenderer.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, 0f); // Start fully transparent
        spriteRenderer.sortingOrder = 100; // Make sure it appears above other elements
    }
    
    private void Start()
    {
        StartCoroutine(AnimateIndicator());
    }
    
    private IEnumerator AnimateIndicator()
    {
        // Fade in
        float elapsedTime = 0f;
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, indicatorColor.a, elapsedTime / fadeInDuration);
            spriteRenderer.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, alpha);
            yield return null;
        }
        
        // Hold
        spriteRenderer.color = indicatorColor;
        yield return new WaitForSeconds(holdDuration);
        
        // Fade out
        elapsedTime = 0f;
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(indicatorColor.a, 0f, elapsedTime / fadeOutDuration);
            spriteRenderer.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, alpha);
            yield return null;
        }
        
        // Destroy after animation
        Destroy(gameObject);
    }
    
    // Create a simple circle sprite programmatically
    private Sprite CreateCircleSprite()
    {
        int resolution = 128;
        Texture2D texture = new Texture2D(resolution, resolution);
        
        Color transparent = new Color(0f, 0f, 0f, 0f);
        
        // Initialize all pixels as transparent
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                texture.SetPixel(x, y, transparent);
            }
        }
        
        // Draw a filled circle
        int center = resolution / 2;
        int radius = center - 4; // Slightly smaller than half the resolution
        
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                if (distance <= radius)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        // Draw a "X" shape for "not allowed" indication
        int lineWidth = 6;
        for (int i = 0; i < resolution; i++)
        {
            // Draw from top-left to bottom-right
            for (int w = -lineWidth/2; w <= lineWidth/2; w++)
            {
                int x = i + w;
                int y = i + w;
                if (x >= 0 && x < resolution && y >= 0 && y < resolution)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
            
            // Draw from top-right to bottom-left
            for (int w = -lineWidth/2; w <= lineWidth/2; w++)
            {
                int x = i + w;
                int y = resolution - i - 1 + w;
                if (x >= 0 && x < resolution && y >= 0 && y < resolution)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }
}