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

    /// <summary>
    /// ショップアイテムのレアリティ (CSV のステージ構成資料に準拠)。
    /// 出現プール (どのレアリティが出るか) や見た目 (枠色等) の決定に使う。
    /// 上に行くほど高レア。
    /// </summary>
    public enum Rarity
    {
        N,
        R,
        SR,
        SSR,
        UR,
    }

    /// <summary>
    /// CSV のアイテム表「アイテム」列に対応する効果カテゴリ。
    /// 同じカテゴリの中で複数 Tier (=効果値の段階) を持つ。
    /// 例: BulletSize は Tier 0..3 で 1.01 / 1.02 / 1.05 / 1.1 倍。
    /// 値は ShopItemEffectTable で個別に持つので、ここではあくまで分類のみ。
    /// </summary>
    public enum ShopItemCategory
    {
        /// <summary>弾でかくなる (倍率)</summary>
        BulletSize,
        /// <summary>弾速 (倍率)</summary>
        BulletSpeed,
        /// <summary>弾数 (+数)</summary>
        BulletCount,
        /// <summary>貫通 (+数)</summary>
        Penetration,
        /// <summary>スピード上昇 (プレイヤー / 倍率)</summary>
        PlayerSpeed,
        /// <summary>ブロックリセット (盤面全消し)</summary>
        BlockReset,
        /// <summary>ブロック救済 (盤面詰み時の救済ブロック)</summary>
        BlockRescue,
        /// <summary>お金増加 (+額)</summary>
        MoneyBonus,
        /// <summary>運UP (良ブロック出現率 +%)</summary>
        LuckUp,
        /// <summary>救済 (追いつかれたときに一度助かる)</summary>
        SaveOnce,
    }
}
