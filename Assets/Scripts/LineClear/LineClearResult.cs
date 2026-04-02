using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    public class LineClearResult
    {
        public int linesCleared;
        public List<Vector2Int> clearedCells = new List<Vector2Int>();
        public List<int> clearedRows = new List<int>();
        public List<int> clearedColumns = new List<int>();
        public bool hasSameColorLine;
        public BlockColorType sameColorType;
    }
}
