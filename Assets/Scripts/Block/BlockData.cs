using UnityEngine;

namespace BlockBlastGame
{
    [CreateAssetMenu(fileName = "NewBlockData", menuName = "BlockBlast/Block Data")]
    public class BlockData : ScriptableObject
    {
        public string blockName;
        public BlockColorType colorType;

        [Tooltip("Flat array representing the shape. Read row by row from bottom-left.")]
        public bool[] shapeFlat;
        public int shapeWidth;
        public int shapeHeight;

        public bool[,] GetShapeArray()
        {
            bool[,] result = new bool[shapeWidth, shapeHeight];
            for (int y = 0; y < shapeHeight; y++)
            {
                for (int x = 0; x < shapeWidth; x++)
                {
                    int index = y * shapeWidth + x;
                    if (index < shapeFlat.Length)
                        result[x, y] = shapeFlat[index];
                }
            }
            return result;
        }

        public int GetCellCount()
        {
            int count = 0;
            foreach (bool b in shapeFlat)
                if (b) count++;
            return count;
        }

        public static BlockData CreateShape(string name, BlockColorType color, int w, int h, bool[] flat)
        {
            var data = CreateInstance<BlockData>();
            data.blockName = name;
            data.colorType = color;
            data.shapeWidth = w;
            data.shapeHeight = h;
            data.shapeFlat = flat;
            return data;
        }
    }
}
