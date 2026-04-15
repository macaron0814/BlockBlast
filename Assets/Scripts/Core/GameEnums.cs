namespace BlockBlastGame
{
    public enum GameState
    {
        Title,
        Playing,
        LineClearing,
        Paused,
        GameOver,
        StageTransition,
        SpaceshipBuild,
        Ending
    }

    public enum CellState
    {
        Empty,
        Filled,
        Item
    }

    public enum BlockColorType
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange,
        Cyan
    }

    public enum ItemType
    {
        SpaceshipPart,
        PowerUp,
        TurnBonus
    }

    public enum GameOverType
    {
        PuzzleStuck,
        EnemyCapture
    }

    public enum PerkType
    {
        TurnRecoveryUp,
        ItemSpawnRateUp,
        SpecialBlockSpawn,
        ComboMultiplierUp,
        StartingTurnsUp,
        BoardClearBomb
    }
}
