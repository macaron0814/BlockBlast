using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class BoardManager : MonoBehaviour
    {
        [Header("Board Settings")]
        [Tooltip("ボードの幅（列数）。5〜12 の間で変更できます。")]
        [Range(5, 12)]
        public int boardWidth = 8;
        [Tooltip("ボードの高さ（行数）。5〜12 の間で変更できます。")]
        [Range(5, 12)]
        public int boardHeight = 8;

        public Cell[,] grid;
        public TilemapController tilemapController;

        void Start()
        {
            if (grid == null)
                InitializeGrid();
        }

        public void InitializeGrid()
        {
            grid = new Cell[boardWidth, boardHeight];
            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    grid[x, y] = new Cell();
                }
            }
            tilemapController?.DrawBoard(boardWidth, boardHeight);
        }

        public void ClearBoard()
        {
            if (grid == null)
                InitializeGrid();

            for (int x = 0; x < boardWidth; x++)
            {
                for (int y = 0; y < boardHeight; y++)
                {
                    grid[x, y].Clear();
                }
            }
            tilemapController?.ClearAllBlocks();
        }

        public bool CanPlaceBlock(BlockData blockData, Vector2Int boardPosition)
        {
            bool[,] shape = blockData.GetShapeArray();
            int shapeW = shape.GetLength(0);
            int shapeH = shape.GetLength(1);

            for (int x = 0; x < shapeW; x++)
            {
                for (int y = 0; y < shapeH; y++)
                {
                    if (!shape[x, y]) continue;

                    int boardX = boardPosition.x + x;
                    int boardY = boardPosition.y + y;

                    if (boardX < 0 || boardX >= boardWidth || boardY < 0 || boardY >= boardHeight)
                        return false;

                    if (grid[boardX, boardY].IsFilled)
                        return false;
                }
            }
            return true;
        }

        public void PlaceBlock(BlockData blockData, Vector2Int boardPosition)
        {
            bool[,] shape = blockData.GetShapeArray();
            int shapeW = shape.GetLength(0);
            int shapeH = shape.GetLength(1);

            for (int x = 0; x < shapeW; x++)
            {
                for (int y = 0; y < shapeH; y++)
                {
                    if (!shape[x, y]) continue;

                    int boardX = boardPosition.x + x;
                    int boardY = boardPosition.y + y;

                    grid[boardX, boardY].Fill(blockData.colorType);
                    tilemapController?.SetBlockTile(boardX, boardY, blockData.colorType);
                }
            }

            GameEvents.TriggerBlockPlaced();
        }

        public bool CanPlaceAnyBlock(List<BlockData> blocks)
        {
            foreach (var block in blocks)
            {
                if (block == null) continue;
                for (int x = 0; x < boardWidth; x++)
                {
                    for (int y = 0; y < boardHeight; y++)
                    {
                        if (CanPlaceBlock(block, new Vector2Int(x, y)))
                            return true;
                    }
                }
            }
            return false;
        }

        public bool IsRowFull(int y)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                if (grid[x, y].IsEmpty) return false;
            }
            return true;
        }

        public bool IsColumnFull(int x)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                if (grid[x, y].IsEmpty) return false;
            }
            return true;
        }

        public void ClearCell(int x, int y)
        {
            grid[x, y].Clear();
            tilemapController?.ClearBlockTile(x, y);
        }

        public Vector2Int WorldToBoard(Vector3 worldPos)
        {
            return tilemapController.WorldToBoard(worldPos);
        }

        public Vector3 BoardToWorld(Vector2Int boardPos)
        {
            return tilemapController.BoardToWorld(boardPos);
        }

        public bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < boardWidth && y >= 0 && y < boardHeight;
        }
    }
}
