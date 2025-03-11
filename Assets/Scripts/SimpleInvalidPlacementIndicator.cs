using UnityEngine;
using System.Collections;

public class SimpleInvalidPlacementIndicator : MonoBehaviour
{
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float maxScale = 1.5f;
    [SerializeField] private Color indicatorColor = new Color(1f, 0.3f, 0.3f, 0.7f);
    
    private SpriteRenderer spriteRenderer;
    
    private void Awake()
    {
        // Create sprite renderer if not present
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // Create a simple circle with X sprite
        spriteRenderer.sprite = CreateXSprite();
        spriteRenderer.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, 0f);
        spriteRenderer.sortingOrder = 100; // Make sure it appears on top
    }
    
    private void Start()
    {
        StartCoroutine(AnimateAndDestroy());
    }
    
    private IEnumerator AnimateAndDestroy()
    {
        // Grow and fade in
        float fadeInTime = duration * 0.3f;
        for (float t = 0; t < fadeInTime; t += Time.deltaTime)
        {
            float normalizedT = t / fadeInTime;
            float scale = Mathf.Lerp(0.1f, 1.0f, normalizedT);
            transform.localScale = new Vector3(scale, scale, 1f);
            
            float alpha = Mathf.Lerp(0f, indicatorColor.a, normalizedT);
            spriteRenderer.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, alpha);
            
            yield return null;
        }
        
        // Hold
        float holdTime = duration * 0.4f;
        yield return new WaitForSeconds(holdTime);
        
        // Shrink and fade out
        float fadeOutTime = duration * 0.3f;
        for (float t = 0; t < fadeOutTime; t += Time.deltaTime)
        {
            float normalizedT = t / fadeOutTime;
            float scale = Mathf.Lerp(1.0f, 0.5f, normalizedT);
            transform.localScale = new Vector3(scale, scale, 1f);
            
            float alpha = Mathf.Lerp(indicatorColor.a, 0f, normalizedT);
            spriteRenderer.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, alpha);
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
    
    // Create a simple circle with X sprite
    private Sprite CreateXSprite()
    {
        int resolution = 64;
        Texture2D texture = new Texture2D(resolution, resolution);
        
        Color transparent = new Color(0f, 0f, 0f, 0f);
        
        // Fill with transparent
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                texture.SetPixel(x, y, transparent);
            }
        }
        
        // Draw circle
        int center = resolution / 2;
        int radius = resolution / 2 - 4;
        
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                if (distance <= radius && distance > radius - 4)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }
        
        // Draw X
        int lineWidth = 4;
        for (int i = 0; i < resolution; i++)
        {
            for (int w = -lineWidth/2; w <= lineWidth/2; w++)
            {
                int x1 = Mathf.Clamp(i + w, 0, resolution - 1);
                int y1 = Mathf.Clamp(i + w, 0, resolution - 1);
                
                int x2 = Mathf.Clamp(i + w, 0, resolution - 1);
                int y2 = Mathf.Clamp(resolution - i - 1 + w, 0, resolution - 1);
                
                // Only draw within the circle
                float distance1 = Mathf.Sqrt((x1 - center) * (x1 - center) + (y1 - center) * (y1 - center));
                float distance2 = Mathf.Sqrt((x2 - center) * (x2 - center) + (y2 - center) * (y2 - center));
                
                if (distance1 <= radius)
                {
                    texture.SetPixel(x1, y1, Color.white);
                }
                
                if (distance2 <= radius)
                {
                    texture.SetPixel(x2, y2, Color.white);
                }
            }
        }
        
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
    }
    
    // Static method to create an indicator at a position
    public static void Create(Vector3 position)
    {
        GameObject indicator = new GameObject("InvalidPlacementIndicator");
        indicator.transform.position = position;
        indicator.AddComponent<SimpleInvalidPlacementIndicator>();
    }
}