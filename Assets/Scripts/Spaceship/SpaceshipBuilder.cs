using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class SpaceshipBuilder : MonoBehaviour
    {
        [Header("Spaceship Config")]
        public int gridWidth = 5;
        public int gridHeight = 7;

        [Header("Runtime")]
        public List<ItemData> availableParts = new List<ItemData>();
        public Dictionary<Vector2Int, ItemData> placedParts = new Dictionary<Vector2Int, ItemData>();
        public SpaceshipRating currentRating;

        [System.Serializable]
        public class SpaceshipRating
        {
            public string shipName;
            public int totalParts;
            public float powerLevel;
            public string description;
        }

        public void Initialize(List<ItemData> collectedParts)
        {
            availableParts = collectedParts ?? new List<ItemData>();
            placedParts.Clear();
            AutoPlaceParts();
            currentRating = CalculateRating();
        }

        void AutoPlaceParts()
        {
            int centerX = gridWidth / 2;

            // Body (always present for normal ship)
            Vector2Int[] bodyPositions = {
                new Vector2Int(centerX, 0),
                new Vector2Int(centerX, 1),
                new Vector2Int(centerX, 2),
                new Vector2Int(centerX, 3),
                new Vector2Int(centerX, 4),
            };

            int partIndex = 0;
            foreach (var pos in bodyPositions)
            {
                if (partIndex < availableParts.Count)
                {
                    placedParts[pos] = availableParts[partIndex];
                    partIndex++;
                }
            }

            // Wings
            Vector2Int[] wingPositions = {
                new Vector2Int(centerX - 1, 1),
                new Vector2Int(centerX + 1, 1),
                new Vector2Int(centerX - 2, 0),
                new Vector2Int(centerX + 2, 0),
            };

            foreach (var pos in wingPositions)
            {
                if (partIndex < availableParts.Count)
                {
                    placedParts[pos] = availableParts[partIndex];
                    partIndex++;
                }
            }
        }

        public SpaceshipRating CalculateRating()
        {
            var rating = new SpaceshipRating();
            rating.totalParts = availableParts.Count;

            if (availableParts.Count == 0)
            {
                rating.shipName = "ノーマルシャトル";
                rating.powerLevel = 1f;
                rating.description = "基本的な脱出用シャトル。\n最低限の装備で地球を脱出！";
            }
            else if (availableParts.Count <= 3)
            {
                rating.shipName = "カスタムシャトル";
                rating.powerLevel = 2f;
                rating.description = $"パーツ{availableParts.Count}個装備！\nなかなかの宇宙船に仕上がった！";
            }
            else if (availableParts.Count <= 6)
            {
                rating.shipName = "スーパーシップ";
                rating.powerLevel = 3f;
                rating.description = $"パーツ{availableParts.Count}個装備！\n高性能な宇宙船が完成！";
            }
            else
            {
                rating.shipName = "アルティメットスターシップ";
                rating.powerLevel = 5f;
                rating.description = $"パーツ{availableParts.Count}個装備！\n伝説の宇宙船が誕生した！";
            }

            bool hasWarp = false;
            bool hasShield = false;
            bool hasAI = false;

            foreach (var part in availableParts)
            {
                switch (part.spaceshipPartId)
                {
                    case "warp": hasWarp = true; break;
                    case "shield": hasShield = true; break;
                    case "ai_pilot": hasAI = true; break;
                }
                rating.powerLevel += part.rarity * 0.5f;
            }

            if (hasWarp && hasShield && hasAI)
            {
                rating.shipName = "ギャラクシーデストロイヤー";
                rating.description = "全特殊パーツ搭載！\n銀河最強の宇宙船が完成した！";
            }

            return rating;
        }

        public string GetEndingText()
        {
            if (currentRating == null)
                currentRating = CalculateRating();

            return $"【{currentRating.shipName}】で地球を脱出！\n\n" +
                   $"{currentRating.description}\n\n" +
                   $"パワーレベル: {currentRating.powerLevel:F1}\n" +
                   $"装備パーツ: {currentRating.totalParts}個";
        }
    }
}
