using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class BlockSpawner : MonoBehaviour
    {
        [Header("Settings")]
        public int blocksPerSet = 3;
        public Transform[] spawnPoints;

        [Header("Runtime")]
        public List<BlockData> allShapes;
        public List<BlockPiece> currentPieces = new List<BlockPiece>();

        [Header("Prefab")]
        public GameObject blockPiecePrefab;

        [Header("Custom Visuals (optional)")]
        [Tooltip("ブロックセルに使うスプライト画像を入れてください。空の場合は自動生成スプライトを使用します。")]
        public Sprite blockCellSprite;

        float difficultyMultiplier = 1f;

        void Awake()
        {
            if (allShapes == null || allShapes.Count == 0)
                allShapes = BlockShapeLibrary.GenerateAllShapes();
        }

        public void SpawnNewSet()
        {
            ClearCurrentPieces();

            for (int i = 0; i < blocksPerSet; i++)
            {
                BlockData template = GetRandomBlock();
                if (template == null) continue;

                BlockData cloned = CloneBlockData(template, GetRandomColor());

                Vector3 spawnPos;
                if (spawnPoints != null && i < spawnPoints.Length)
                {
                    spawnPos = spawnPoints[i].position;
                }
                else
                {
                    var bm     = GameManager.Instance?.boardManager;
                    float cX   = bm != null ? (bm.boardWidth  - 1) * 0.5f : 3.5f;
                    float spc  = bm != null ? bm.boardWidth / 3f           : 3f;
                    float sY   = bm != null ? -bm.boardHeight * 0.3f       : -2.5f;
                    spawnPos   = new Vector3(cX + (i - 1) * spc, sY, 0);
                }

                Transform parent = (spawnPoints != null && i < spawnPoints.Length)
                    ? spawnPoints[i] : transform;

                GameObject pieceObj;
                if (blockPiecePrefab != null)
                {
                    pieceObj = Instantiate(blockPiecePrefab, spawnPos, Quaternion.identity, parent);
                }
                else
                {
                    pieceObj = new GameObject($"BlockPiece_{i}");
                    pieceObj.transform.position = spawnPos;
                    pieceObj.transform.SetParent(parent);
                    pieceObj.AddComponent<BlockPiece>();
                }

                BlockPiece piece = pieceObj.GetComponent<BlockPiece>();
                if (piece != null)
                {
                    // TilemapController.tileSpacingX/Y を参照して間隔を連動させる
                    var tc = GameManager.Instance?.boardManager?.tilemapController;
                    float sx = tc != null ? tc.tileSpacingX : 0.08f;
                    float sy = tc != null ? tc.tileSpacingY : 0.08f;
                    piece.Initialize(cloned, blockCellSprite, sx, sy);
                    currentPieces.Add(piece);
                }
            }
        }

        BlockData CloneBlockData(BlockData source, BlockColorType color)
        {
            var clone = ScriptableObject.CreateInstance<BlockData>();
            clone.blockName = source.blockName;
            clone.shapeWidth = source.shapeWidth;
            clone.shapeHeight = source.shapeHeight;
            clone.shapeFlat = (bool[])source.shapeFlat.Clone();
            clone.colorType = color;
            return clone;
        }

        BlockData GetRandomBlock()
        {
            if (allShapes == null || allShapes.Count == 0) return null;

            int maxIndex = Mathf.Min(allShapes.Count, Mathf.FloorToInt(allShapes.Count * difficultyMultiplier));
            maxIndex = Mathf.Max(maxIndex, 5);
            maxIndex = Mathf.Min(maxIndex, allShapes.Count);
            return allShapes[Random.Range(0, maxIndex)];
        }

        BlockColorType GetRandomColor()
        {
            var colors = System.Enum.GetValues(typeof(BlockColorType));
            return (BlockColorType)colors.GetValue(Random.Range(0, colors.Length));
        }

        public void ClearCurrentPieces()
        {
            foreach (var piece in currentPieces)
            {
                if (piece != null)
                    Destroy(piece.gameObject);
            }
            currentPieces.Clear();
        }

        public void RemovePiece(BlockPiece piece)
        {
            currentPieces.Remove(piece);
            if (piece != null)
                Destroy(piece.gameObject);
        }

        public bool HasAvailableBlocks()
        {
            return currentPieces.Count > 0;
        }

        public List<BlockData> GetCurrentBlocks()
        {
            var blocks = new List<BlockData>();
            foreach (var piece in currentPieces)
            {
                if (piece != null)
                    blocks.Add(piece.blockData);
            }
            return blocks;
        }

        public void SetDifficulty(float multiplier)
        {
            difficultyMultiplier = multiplier;
        }
    }
}
