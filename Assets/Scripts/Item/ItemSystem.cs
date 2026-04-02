using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class ItemSystem : MonoBehaviour
    {
        [Header("References")]
        public BoardManager boardManager;
        public TilemapController tilemapController;
        public TurnManager turnManager;

        [Header("Spawn Settings")]
        [Range(0f, 1f)]
        public float itemSpawnChance = 0.3f;
        public int itemDisplayTurns = 5;
        public float itemSpawnRateMultiplier = 1f;

        [Header("Runtime")]
        public List<ItemData> collectedItems = new List<ItemData>();

        [System.Serializable]
        public class BoardItem
        {
            public ItemData data;
            public int remainingTurns;

            public BoardItem(ItemData data, int turns)
            {
                this.data = data;
                this.remainingTurns = turns;
            }
        }

        Dictionary<Vector2Int, BoardItem> boardItems = new Dictionary<Vector2Int, BoardItem>();

        public void InitializeForStage()
        {
            ClearBoardItems();
        }

        /// Called every turn (on block placement) to decrement item timers and remove expired items.
        public void TickItemTimers()
        {
            var expired = new List<Vector2Int>();

            foreach (var kvp in boardItems)
            {
                kvp.Value.remainingTurns--;
                if (kvp.Value.remainingTurns <= 0)
                {
                    expired.Add(kvp.Key);
                }
            }

            foreach (var pos in expired)
            {
                var item = boardItems[pos];
                Debug.Log($"[Item] '{item.data.itemName}' at ({pos.x},{pos.y}) expired after display time ran out.");
                boardItems.Remove(pos);
                boardManager.grid[pos.x, pos.y].ClearItem();
                tilemapController?.ClearItemMarker(pos.x, pos.y);
            }
        }

        /// Called after a successful line clear. Rolls for random item spawn.
        public void TrySpawnRandomItem(int linesCleared)
        {
            float chance = itemSpawnChance * itemSpawnRateMultiplier;
            // Each line cleared gives an independent roll
            for (int i = 0; i < linesCleared; i++)
            {
                if (Random.value > chance) continue;

                Vector2Int pos = GetRandomEmptyCell();
                if (pos.x < 0) break;

                int stageNum = GameManager.Instance.stageManager.currentStageNumber;
                int maxRarity = Mathf.Min(stageNum + 1, 5);
                ItemData item = ItemDatabase.GetRandomItem(maxRarity);
                if (item == null) continue;

                var boardItem = new BoardItem(item, itemDisplayTurns);
                boardItems[pos] = boardItem;
                boardManager.grid[pos.x, pos.y].PlaceItem(item);
                tilemapController?.SetItemMarker(pos.x, pos.y);

                Debug.Log($"[Item] '{item.itemName}' (Rarity:{item.rarity}) spawned at ({pos.x},{pos.y}). Visible for {itemDisplayTurns} turns.");
            }
        }

        Vector2Int GetRandomEmptyCell()
        {
            int width = boardManager.boardWidth;
            int height = boardManager.boardHeight;
            List<Vector2Int> emptyCells = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = boardManager.grid[x, y];
                    if (cell.IsEmpty && !boardItems.ContainsKey(new Vector2Int(x, y)))
                        emptyCells.Add(new Vector2Int(x, y));
                }
            }

            if (emptyCells.Count == 0) return new Vector2Int(-1, -1);
            return emptyCells[Random.Range(0, emptyCells.Count)];
        }

        /// Collect items from cells that were cleared by line clear.
        public List<ItemData> CollectItemsFromClearedCells(List<Vector2Int> clearedCells)
        {
            var collected = new List<ItemData>();

            foreach (var cellPos in clearedCells)
            {
                if (boardItems.TryGetValue(cellPos, out BoardItem boardItem))
                {
                    collected.Add(boardItem.data);
                    collectedItems.Add(boardItem.data);
                    boardItems.Remove(cellPos);
                    tilemapController?.ClearItemMarker(cellPos.x, cellPos.y);

                    Debug.Log($"[Item] COLLECTED '{boardItem.data.itemName}' (Type:{boardItem.data.type}, Rarity:{boardItem.data.rarity}) from ({cellPos.x},{cellPos.y})!");

                    if (boardItem.data.type == ItemType.TurnBonus)
                    {
                        turnManager.AddTurns(boardItem.data.turnBonusAmount);
                        Debug.Log($"[Item] Turn bonus: +{boardItem.data.turnBonusAmount} turns!");
                    }
                }
            }

            return collected;
        }

        public List<ItemData> GetCollectedSpaceshipParts()
        {
            var parts = new List<ItemData>();
            foreach (var item in collectedItems)
            {
                if (item.type == ItemType.SpaceshipPart)
                    parts.Add(item);
            }
            return parts;
        }

        public List<ItemData> GetAllCollectedItems()
        {
            return new List<ItemData>(collectedItems);
        }

        void ClearBoardItems()
        {
            boardItems.Clear();
            tilemapController?.ClearAllItemMarkers();
        }

        public void ClearAll()
        {
            ClearBoardItems();
            collectedItems.Clear();
        }

        // Legacy compatibility - no longer does bulk placement
        public void PlaceItemsForStage(int stageNumber)
        {
            ClearBoardItems();
        }

        // Legacy field kept for perk system compatibility
        public int itemsPerStage = 3;
    }
}
