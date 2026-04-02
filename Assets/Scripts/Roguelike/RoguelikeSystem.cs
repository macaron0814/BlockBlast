using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class RoguelikeSystem : MonoBehaviour
    {
        [Header("Perk Pool")]
        public List<PerkType> availablePerks = new List<PerkType>
        {
            PerkType.TurnRecoveryUp,
            PerkType.ItemSpawnRateUp,
            PerkType.SpecialBlockSpawn,
            PerkType.ComboMultiplierUp,
            PerkType.StartingTurnsUp,
            PerkType.BoardClearBomb
        };

        [Header("Applied Perks")]
        public List<PerkType> appliedPerks = new List<PerkType>();

        public List<PerkType> GetRandomPerks(int count)
        {
            var pool = new List<PerkType>(availablePerks);
            var result = new List<PerkType>();

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        public void ApplyPerk(PerkType perk)
        {
            appliedPerks.Add(perk);
            var gm = GameManager.Instance;

            switch (perk)
            {
                case PerkType.TurnRecoveryUp:
                    gm.turnManager.turnRecoveryBonus += 1;
                    break;

                case PerkType.ItemSpawnRateUp:
                    gm.itemSystem.itemSpawnRateMultiplier += 0.5f;
                    break;

                case PerkType.SpecialBlockSpawn:
                    gm.blockSpawner.SetDifficulty(
                        Mathf.Min(1f, gm.blockSpawner.allShapes.Count));
                    break;

                case PerkType.ComboMultiplierUp:
                    gm.comboSystem.sameColorBonusMultiplier += 0.5f;
                    break;

                case PerkType.StartingTurnsUp:
                    gm.turnManager.startingTurnsBonus += 3;
                    break;

                case PerkType.BoardClearBomb:
                    ClearRandomArea(gm.boardManager);
                    break;
            }
        }

        void ClearRandomArea(BoardManager board)
        {
            int centerX = Random.Range(1, board.boardWidth - 1);
            int centerY = Random.Range(1, board.boardHeight - 1);

            var clearedCells = new List<Vector2Int>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (board.IsValidPosition(x, y))
                    {
                        clearedCells.Add(new Vector2Int(x, y));
                        board.ClearCell(x, y);
                    }
                }
            }

            var gm = GameManager.Instance;
            if (gm != null && gm.itemSystem != null)
                gm.itemSystem.CollectItemsFromClearedCells(clearedCells);
        }

        public string GetPerkDescription(PerkType perk)
        {
            return perk switch
            {
                PerkType.TurnRecoveryUp => "ターン回復量+1\nライン消去時の回復ターンが増加",
                PerkType.ItemSpawnRateUp => "アイテム出現率UP\nステージ内のアイテムが増加",
                PerkType.SpecialBlockSpawn => "特殊ブロック解放\nより多くのブロック形状が出現",
                PerkType.ComboMultiplierUp => "コンボ倍率UP\n同色消しボーナスが強化",
                PerkType.StartingTurnsUp => "初期ターン+3\n次ステージの開始ターンが増加",
                PerkType.BoardClearBomb => "ボムクリア\nボードの一部をランダムに消去",
                _ => "不明なパーク"
            };
        }

        public string GetPerkName(PerkType perk)
        {
            return perk switch
            {
                PerkType.TurnRecoveryUp => "回復強化",
                PerkType.ItemSpawnRateUp => "アイテム増加",
                PerkType.SpecialBlockSpawn => "形状解放",
                PerkType.ComboMultiplierUp => "コンボ強化",
                PerkType.StartingTurnsUp => "ターン増加",
                PerkType.BoardClearBomb => "ボムクリア",
                _ => "???"
            };
        }

        public void Reset()
        {
            appliedPerks.Clear();
        }
    }
}
