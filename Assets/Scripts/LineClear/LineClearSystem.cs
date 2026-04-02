using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class LineClearSystem : MonoBehaviour
    {
        public BoardManager boardManager;
        public TilemapController tilemapController;

        [Header("Animation")]
        public float clearAnimationDelay = 0.05f;
        public float clearAnimationDuration = 0.3f;

        public LineClearResult CheckAndClearLines()
        {
            var result = new LineClearResult();
            int width = boardManager.boardWidth;
            int height = boardManager.boardHeight;

            for (int y = 0; y < height; y++)
            {
                if (boardManager.IsRowFull(y))
                {
                    result.clearedRows.Add(y);
                    CheckRowSameColor(y, result);
                }
            }

            for (int x = 0; x < width; x++)
            {
                if (boardManager.IsColumnFull(x))
                {
                    result.clearedColumns.Add(x);
                    CheckColumnSameColor(x, result);
                }
            }

            result.linesCleared = result.clearedRows.Count + result.clearedColumns.Count;

            if (result.linesCleared > 0)
            {
                CollectClearedCells(result);
            }

            return result;
        }

        public void ExecuteClearWithCallback(LineClearResult result, Action onComplete)
        {
            StartCoroutine(AnimateAndClear(result, onComplete));
        }

        void CheckRowSameColor(int y, LineClearResult result)
        {
            int width = boardManager.boardWidth;
            BlockColorType firstColor = boardManager.grid[0, y].colorType;
            bool allSame = true;

            for (int x = 1; x < width; x++)
            {
                if (boardManager.grid[x, y].colorType != firstColor)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                result.hasSameColorLine = true;
                result.sameColorType = firstColor;
            }
        }

        void CheckColumnSameColor(int x, LineClearResult result)
        {
            int height = boardManager.boardHeight;
            BlockColorType firstColor = boardManager.grid[x, 0].colorType;
            bool allSame = true;

            for (int y = 1; y < height; y++)
            {
                if (boardManager.grid[x, y].colorType != firstColor)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
            {
                result.hasSameColorLine = true;
                result.sameColorType = firstColor;
            }
        }

        void CollectClearedCells(LineClearResult result)
        {
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
            int width = boardManager.boardWidth;
            int height = boardManager.boardHeight;

            foreach (int y in result.clearedRows)
            {
                for (int x = 0; x < width; x++)
                    cells.Add(new Vector2Int(x, y));
            }

            foreach (int x in result.clearedColumns)
            {
                for (int y = 0; y < height; y++)
                    cells.Add(new Vector2Int(x, y));
            }

            result.clearedCells.AddRange(cells);
        }

        IEnumerator AnimateAndClear(LineClearResult result, Action onComplete)
        {
            foreach (var cell in result.clearedCells)
            {
                Vector3 worldPos = boardManager.BoardToWorld(cell);
                SpawnClearEffect(worldPos, boardManager.grid[cell.x, cell.y].colorType);
                yield return new WaitForSeconds(clearAnimationDelay);
            }

            yield return new WaitForSeconds(clearAnimationDuration);

            foreach (var cell in result.clearedCells)
            {
                boardManager.ClearCell(cell.x, cell.y);
            }

            onComplete?.Invoke();
        }

        void SpawnClearEffect(Vector3 position, BlockColorType colorType)
        {
            GameObject effect = new GameObject("ClearEffect");
            effect.transform.position = position;
            var sr = effect.AddComponent<SpriteRenderer>();
            sr.sprite = BlockPiece.GetOrCreateSquareSprite();
            sr.color = BlockPiece.GetColorForType(colorType);
            sr.sortingOrder = 10;
            StartCoroutine(FadeAndDestroy(effect, clearAnimationDuration));
        }

        IEnumerator FadeAndDestroy(GameObject obj, float duration)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            Vector3 startScale = obj.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                obj.transform.localScale = startScale * (1f + t * 0.5f);
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = 1f - t;
                    sr.color = c;
                }
                yield return null;
            }

            Destroy(obj);
        }
    }
}
