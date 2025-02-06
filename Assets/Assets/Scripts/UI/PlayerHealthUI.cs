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
        float value = currentHP / maxHP;
        Debug.Log($"Slider Update - Value: {value}, Current HP: {currentHP}, Max HP: {maxHP}");
        
        // Ensure slider is active and enabled
        if (hpSlider.gameObject.activeInHierarchy && hpSlider.enabled)
        {
            // Force updates
            hpSlider.value = value;
            hpSlider.SetValueWithoutNotify(value);
            
            // Trigger canvas updates
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hpSlider.GetComponent<RectTransform>());
        }
        
        hpText.text = $"{Mathf.CeilToInt(currentHP)}";
    }
    
    public void SetPlayerColor(bool isPlayerA)
    {
        fillImage.color = isPlayerA ? playerAColor : playerBColor;
    }
}