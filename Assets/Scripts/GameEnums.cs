public enum GameState
{
    Setup,              // Initial setup, loading resources
    PlayerAPlacement,   // Player A placing their units
    PlayerBPlacement,   // Player B placing their units
    BattleStart,       // Pre-battle preparations
    BattleActive,      // Battle in progress
    BattleEnd,         // Battle has ended, showing results
    GameOver           // Game complete, showing final results
}