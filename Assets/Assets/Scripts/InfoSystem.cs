using UnityEngine;

public class InfoSystem : MonoBehaviour
{
    [System.Serializable]
    public class UnitInfo
    {
        public UnitType unitType;
        public string description;
        public float health;
        public float damage;
        public float attackSpeed;
        public string specialAbility;
    }

    [SerializeField] private UnitInfo[] unitDatabase;
    [SerializeField] private TMPro.TextMeshProUGUI unitInfoText;
    [SerializeField] private TMPro.TextMeshProUGUI gameplayInfoText;

    // These methods can be called directly from UI buttons
    public void ShowFighterInfo() { ShowUnitInfo(UnitType.Fighter); }
    public void ShowTankInfo() { ShowUnitInfo(UnitType.Tank); }
    public void ShowMageInfo() { ShowUnitInfo(UnitType.Mage); }
    public void ShowRangeInfo() { ShowUnitInfo(UnitType.Range); }

    // This becomes a private method since we'll call it through the public methods above
    private void ShowUnitInfo(UnitType unitType)
    {
        UnitInfo info = System.Array.Find(unitDatabase, unit => unit.unitType == unitType);
        if (info != null)
        {
            unitInfoText.text = $"Unit Type: {info.unitType}\n" +
                              $"Health: {info.health}\n" +
                              $"Damage: {info.damage}\n" +
                              $"Attack Speed: {info.attackSpeed}\n" +
                              $"Special Ability: {info.specialAbility}\n\n" +
                              $"Description: {info.description}";
        }
    }

    public void ShowGameplayInfo()
    {
        gameplayInfoText.text = "How to Play:\n\n" +
                              "1. Place Your Units\n" +
                              "- Drag and place up to 3 units on the battlefield\n" +
                              "- Each unit has unique strengths and abilities\n\n" +
                              "2. Battle Phase\n" +
                              "- Units automatically fight the enemy team\n" +
                              "- Units will move, target, and attack on their own\n\n" +
                              "3. Victory Conditions\n" +
                              "- Defeat all enemy units to win\n" +
                              "- Protect your units from being defeated";
    }
}