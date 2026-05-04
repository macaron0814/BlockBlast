#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using BlockBlastGame;

/// <summary>
/// BlockData の Inspector を拡張し、cellSprites を shapeFlat と同じ
/// レイアウトでグリッド表示する。空セル(false)はスキップ。
///
/// (top → bottom) の見た目で並ぶよう、内部表現 (y=0 が下) と上下反転して描画する。
/// </summary>
[CustomEditor(typeof(BlockData))]
public class BlockDataEditor : Editor
{
    SerializedProperty blockName;
    SerializedProperty colorType;
    SerializedProperty shapeFlat;
    SerializedProperty shapeWidth;
    SerializedProperty shapeHeight;
    SerializedProperty designTheme;
    SerializedProperty shapeSprite;
    SerializedProperty cellSprites;

    void OnEnable()
    {
        blockName    = serializedObject.FindProperty("blockName");
        colorType    = serializedObject.FindProperty("colorType");
        shapeFlat    = serializedObject.FindProperty("shapeFlat");
        shapeWidth   = serializedObject.FindProperty("shapeWidth");
        shapeHeight  = serializedObject.FindProperty("shapeHeight");
        designTheme  = serializedObject.FindProperty("designTheme");
        shapeSprite  = serializedObject.FindProperty("shapeSprite");
        cellSprites  = serializedObject.FindProperty("cellSprites");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(blockName);
        EditorGUILayout.PropertyField(colorType);
        EditorGUILayout.PropertyField(designTheme);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Shape Sprite (簡易設定 / このシェイプ全セルに使う1枚)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "ケーキやホットドッグなどの食べ物画像を1枚ここに入れるだけで、" +
            "このシェイプの全セルに同じ画像が使われます。\n" +
            "セルごとに違う画像を使いたい場合は下の Cell Sprites グリッドを使ってください。",
            MessageType.None);
        EditorGUILayout.PropertyField(shapeSprite, new GUIContent("Shape Sprite"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newW = Mathf.Max(1, EditorGUILayout.IntField("Shape Width",  shapeWidth.intValue));
        int newH = Mathf.Max(1, EditorGUILayout.IntField("Shape Height", shapeHeight.intValue));
        if (EditorGUI.EndChangeCheck())
        {
            shapeWidth.intValue  = newW;
            shapeHeight.intValue = newH;
            ResizeBoolArray(shapeFlat, newW * newH);
            ResizeObjectArray(cellSprites, newW * newH);
        }

        EnsureArraySize(shapeFlat,   shapeWidth.intValue * shapeHeight.intValue, isObject: false);
        EnsureArraySize(cellSprites, shapeWidth.intValue * shapeHeight.intValue, isObject: true);

        DrawShapeGrid();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Cell Sprites (シェイプ別デザイン)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "各セルに配置するスプライトをグリッドで指定します。\n" +
            "shapeFlat=true のセルだけ有効。空セルにスプライトを入れても無視されます。\n" +
            "全セル空 (null) の場合は BlockSpawner.blockCellSprite が使われます。",
            MessageType.Info);

        DrawCellSpriteGrid();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Clear All Cell Sprites", GUILayout.Height(20)))
        {
            for (int i = 0; i < cellSprites.arraySize; i++)
                cellSprites.GetArrayElementAtIndex(i).objectReferenceValue = null;
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawShapeGrid()
    {
        int w = shapeWidth.intValue;
        int h = shapeHeight.intValue;
        if (w <= 0 || h <= 0) return;

        EditorGUILayout.LabelField("Shape (上が y = h-1, 下が y = 0)", EditorStyles.miniBoldLabel);

        // 上下反転して描画 (top-most 行が y = h-1)
        for (int row = 0; row < h; row++)
        {
            int y = h - 1 - row;
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                var prop = shapeFlat.GetArrayElementAtIndex(idx);
                bool cur = prop.boolValue;
                bool next = GUILayout.Toggle(cur, GUIContent.none, GUI.skin.button,
                    GUILayout.Width(24), GUILayout.Height(24));
                if (next != cur) prop.boolValue = next;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawCellSpriteGrid()
    {
        int w = shapeWidth.intValue;
        int h = shapeHeight.intValue;
        if (w <= 0 || h <= 0) return;

        const float CELL = 60f;

        for (int row = 0; row < h; row++)
        {
            int y = h - 1 - row;
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                bool filled = idx < shapeFlat.arraySize && shapeFlat.GetArrayElementAtIndex(idx).boolValue;
                var prop = cellSprites.GetArrayElementAtIndex(idx);

                if (filled)
                {
                    var sprite = (Sprite)EditorGUILayout.ObjectField(
                        GUIContent.none,
                        prop.objectReferenceValue,
                        typeof(Sprite),
                        false,
                        GUILayout.Width(CELL),
                        GUILayout.Height(CELL));
                    prop.objectReferenceValue = sprite;
                }
                else
                {
                    GUILayout.Box(GUIContent.none, GUILayout.Width(CELL), GUILayout.Height(CELL));
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    static void ResizeBoolArray(SerializedProperty prop, int newLen)
    {
        EnsureArraySize(prop, newLen, isObject: false);
    }

    static void ResizeObjectArray(SerializedProperty prop, int newLen)
    {
        EnsureArraySize(prop, newLen, isObject: true);
    }

    static void EnsureArraySize(SerializedProperty prop, int newLen, bool isObject)
    {
        if (prop == null || newLen < 0) return;
        while (prop.arraySize < newLen)
        {
            prop.InsertArrayElementAtIndex(prop.arraySize);
            var added = prop.GetArrayElementAtIndex(prop.arraySize - 1);
            if (isObject) added.objectReferenceValue = null;
            else          added.boolValue = false;
        }
        while (prop.arraySize > newLen)
            prop.DeleteArrayElementAtIndex(prop.arraySize - 1);
    }
}
#endif
