public enum GameState
{
    Setup,          // Initial setup, loading resources
    UnitPlacement,  // Player placing their units
    BattleStart,    // Pre-battle preparations
    BattleActive,   // Battle in progress
    BattleEnd,      // Battle has ended, showing results
    GameOver        // Game complete, showing final results
}
