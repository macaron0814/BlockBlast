using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// BlockData アセットを 1 か所にまとめておく ScriptableObject。
    ///
    /// BlockSpawner はこの Registry を参照することで、
    /// 個別の BlockData を全てインスペクタにドラッグせずとも
    /// シェイプ一覧を扱えるようになる。
    ///
    /// Resources/BlockShapeRegistry.asset として配置すると
    /// BlockSpawner が自動ロードする (Editor / 実機共通)。
    /// </summary>
    [CreateAssetMenu(fileName = "BlockShapeRegistry", menuName = "BlockBlast/Block Shape Registry")]
    public class BlockShapeRegistry : ScriptableObject
    {
        [Tooltip("出現させるブロック形状の SO 一覧。BlockShapeAssetGenerator で自動生成可能。")]
        public List<BlockData> shapes = new List<BlockData>();

        public List<BlockData> GetShapes()
        {
            var result = new List<BlockData>(shapes.Count);
            foreach (var s in shapes)
                if (s != null) result.Add(s);
            return result;
        }
    }
}
