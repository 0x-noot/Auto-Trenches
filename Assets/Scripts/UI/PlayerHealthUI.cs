using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image fillImage;
    
    [Header("Colors")]
    [SerializeField] private Color playerAColor = new Color(0.2f, 0.6f, 1f); // Blue
    [SerializeField] private Color playerBColor = new Color(1f, 0.2f, 0.2f); // Red
    
    public void SetHP(float currentHP, float maxHP)
    {
        try {
            // Guard against null references
            if (hpSlider == null || hpText == null) return;
            
            // Avoid division by zero
            if (maxHP <= 0) maxHP = 1;
            
            float value = Mathf.Clamp01(currentHP / maxHP);
            
            // Update slider value
            if (hpSlider.gameObject.activeInHierarchy && hpSlider.enabled)
            {
                hpSlider.value = value;
            }
            
            // Update text
            if (hpText.gameObject.activeInHierarchy)
            {
                hpText.text = $"{Mathf.CeilToInt(currentHP)}";
            }
        }
        catch (System.Exception ex) {
            Debug.LogError($"Error in SetHP: {ex.Message}");
        }
    }
    
    public void SetPlayerColor(bool isPlayerA)
    {
        try {
            if (fillImage != null)
            {
                fillImage.color = isPlayerA ? playerAColor : playerBColor;
            }
        }
        catch (System.Exception ex) {
            Debug.LogError($"Error in SetPlayerColor: {ex.Message}");
        }
    }
}