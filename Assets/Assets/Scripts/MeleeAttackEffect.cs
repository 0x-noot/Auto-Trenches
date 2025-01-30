using UnityEngine;

public class MeleeAttackEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    [SerializeField] private float effectDuration = 0.5f;
    [SerializeField] private AnimationCurve scaleCurve;
    [SerializeField] private float maxScale = 1.5f;
    
    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer slashSprite;
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = new Color(1, 1, 1, 0);
    
    private float elapsedTime = 0f;
    private Vector3 initialScale;

    private void Start()
    {
        Debug.Log($"[Effect] Starting effect animation");
        
        if (slashSprite == null)
        {
            slashSprite = GetComponent<SpriteRenderer>();
            if (slashSprite == null)
            {
                Debug.LogError("[Effect] No SpriteRenderer found!");
                Destroy(gameObject);
                return;
            }
        }

        if (scaleCurve == null)
        {
            Debug.LogError("[Effect] No scale curve assigned!");
            Destroy(gameObject);
            return;
        }
        
        initialScale = transform.localScale;
        slashSprite.color = startColor;
        
        Debug.Log($"[Effect] Initial setup complete. Scale: {initialScale}, Color: {startColor}");
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float normalizedTime = elapsedTime / effectDuration;
        
        // Update scale
        float currentScale = scaleCurve.Evaluate(normalizedTime) * maxScale;
        transform.localScale = initialScale * currentScale;
        
        // Update color/transparency
        slashSprite.color = Color.Lerp(startColor, endColor, normalizedTime);
        
        // Destroy when done
        if (elapsedTime >= effectDuration)
        {
            Debug.Log("[Effect] Effect complete, destroying");
            Destroy(gameObject);
        }
    }

    public void SetupEffect(Vector3 attackerPosition, Vector3 targetPosition)
    {
        Debug.Log($"[Effect] Setting up effect. Attacker: {attackerPosition}, Target: {targetPosition}");
        
        // Calculate direction to face
        Vector2 direction = (targetPosition - attackerPosition).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Set rotation (assuming sprite faces right by default)
        transform.rotation = Quaternion.Euler(0, 0, angle);
        
        // Position slightly in front of the attacker
        transform.position = Vector3.Lerp(attackerPosition, targetPosition, 0.3f);
        
        Debug.Log($"[Effect] Effect positioned at {transform.position} with rotation {angle} degrees");
    }
}