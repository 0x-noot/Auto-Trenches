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
        if (slashSprite == null)
        {
            slashSprite = GetComponent<SpriteRenderer>();
            if (slashSprite == null)
            {
                Debug.LogError("No SpriteRenderer found!");
                Destroy(gameObject);
                return;
            }
        }

        if (scaleCurve == null)
        {
            Debug.LogError("No scale curve assigned!");
            Destroy(gameObject);
            return;
        }
        
        initialScale = transform.localScale;
        slashSprite.color = startColor;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float normalizedTime = elapsedTime / effectDuration;
        
        float currentScale = scaleCurve.Evaluate(normalizedTime) * maxScale;
        transform.localScale = initialScale * currentScale;
        
        slashSprite.color = Color.Lerp(startColor, endColor, normalizedTime);
        
        if (elapsedTime >= effectDuration)
        {
            Destroy(gameObject);
        }
    }

    public void SetupEffect(Vector3 attackerPosition, Vector3 targetPosition)
    {
        Vector2 direction = (targetPosition - attackerPosition).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        transform.rotation = Quaternion.Euler(0, 0, angle);
        transform.position = Vector3.Lerp(attackerPosition, targetPosition, 0.3f);
    }
}