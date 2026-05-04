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

        [Header("Design")]
        [Tooltip("デザインのカテゴリ (例: サンドイッチ / ホットドッグ / ピザ など)。表示用の任意ラベル。")]
        public string designTheme;

        [Tooltip("【簡易設定】このシェイプの全セルに使う1枚画像。\nケーキ等のシェイプ別画像をここに入れるだけで、その形状だけそのスプライトになります。\ncellSprites が設定されている場合はそちらが優先されます。")]
        public Sprite shapeSprite;

        [Tooltip("【詳細設定】各セルに使うスプライトをセル位置ごとに指定 (y * width + x)。\n空セルは null のままで構いません。\nshapeSprite と併用した場合はこちらが優先されます。")]
        public Sprite[] cellSprites;

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
            if (shapeFlat == null) return 0;
            foreach (bool b in shapeFlat)
                if (b) count++;
            return count;
        }

        /// <summary>
        /// 指定セル位置 (shape 座標系) のデザイン用スプライト。未設定なら null。
        /// </summary>
        public Sprite GetSpriteForCell(int x, int y)
        {
            if (cellSprites == null || cellSprites.Length == 0) return null;
            if (x < 0 || y < 0 || x >= shapeWidth || y >= shapeHeight) return null;
            int idx = y * shapeWidth + x;
            return idx < cellSprites.Length ? cellSprites[idx] : null;
        }

        public bool HasPerCellSprites()
        {
            if (cellSprites == null) return false;
            foreach (var s in cellSprites)
                if (s != null) return true;
            return false;
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
