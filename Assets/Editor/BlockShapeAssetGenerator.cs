#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BlockBlastGame;

/// <summary>
/// シェイプ別デザインのセットアップを一発で済ませるためのユーティリティ。
///
/// メニュー:
///   Tools/BlockBlast/Setup Block Shape Pipeline
///     1) BlockShapeLibrary 内の全シェイプを Assets/ScriptableObjects/BlockShapes/*.asset に出力
///     2) Assets/Resources/BlockShapeRegistry.asset を作成 (上記アセットを保持)
///     3) 開いているシーン内の BlockSpawner.shapeRegistry に自動アサイン
///
/// 既存アセットがある場合はシェイプ情報のみ再同期し、designer が割り当てた
/// shapeSprite / cellSprites などの値は保持する。
/// </summary>
public static class BlockShapeAssetGenerator
{
    const string SHAPES_FOLDER   = "Assets/ScriptableObjects/BlockShapes";
    const string RESOURCES_FOLDER = "Assets/Resources";
    const string REGISTRY_PATH   = "Assets/Resources/BlockShapeRegistry.asset";

    [MenuItem("Tools/BlockBlast/Setup Block Shape Pipeline")]
    public static void SetupPipeline()
    {
        var shapes = GenerateOrSyncBlockShapeAssets();
        var registry = CreateOrUpdateRegistry(shapes);
        int wired = WireSceneBlockSpawners(registry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "BlockBlast / Setup Block Shape Pipeline",
            $"BlockShape アセット: {shapes.Count} 個\n" +
            $"Registry: {REGISTRY_PATH}\n" +
            $"シーン内 BlockSpawner.shapeRegistry に自動アサイン: {wired} 個\n\n" +
            "次のステップ:\n" +
            "  1) Assets/ScriptableObjects/BlockShapes/ の各 BlockData を選択\n" +
            "  2) Inspector 上部 [Shape Sprite] にケーキ等の画像をドラッグ\n" +
            "     または [Cell Sprites] グリッドでセル別に画像を配置",
            "OK");
    }

    [MenuItem("Tools/BlockBlast/Generate Block Shape Assets")]
    public static void GenerateBlockShapeAssetsOnly()
    {
        GenerateOrSyncBlockShapeAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/BlockBlast/Wire BlockShapeRegistry to Scene BlockSpawner")]
    public static void RewireSceneOnly()
    {
        var registry = AssetDatabase.LoadAssetAtPath<BlockShapeRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            EditorUtility.DisplayDialog("BlockBlast",
                "Registry が見つかりません。先に [Setup Block Shape Pipeline] を実行してください。", "OK");
            return;
        }

        int wired = WireSceneBlockSpawners(registry);
        EditorUtility.DisplayDialog("BlockBlast",
            $"シーン内 BlockSpawner.shapeRegistry に自動アサイン: {wired} 個", "OK");
    }

    // ──────────────────────────────────────────────
    //  1) BlockShape アセット生成
    // ──────────────────────────────────────────────
    static List<BlockData> GenerateOrSyncBlockShapeAssets()
    {
        EnsureFolder(SHAPES_FOLDER);

        var sources = BlockShapeLibrary.GenerateAllShapes();
        var result  = new List<BlockData>(sources.Count);

        foreach (var src in sources)
        {
            if (src == null) continue;
            string path = $"{SHAPES_FOLDER}/{src.blockName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BlockData>(path);

            if (existing == null)
            {
                var asset = ScriptableObject.CreateInstance<BlockData>();
                asset.blockName    = src.blockName;
                asset.colorType    = src.colorType;
                asset.shapeWidth   = src.shapeWidth;
                asset.shapeHeight  = src.shapeHeight;
                asset.shapeFlat    = (bool[])src.shapeFlat.Clone();
                asset.designTheme  = SuggestThemeName(src);
                asset.cellSprites  = new Sprite[src.shapeWidth * src.shapeHeight];

                AssetDatabase.CreateAsset(asset, path);
                result.Add(asset);
            }
            else
            {
                bool dirty = false;
                if (existing.shapeWidth != src.shapeWidth)   { existing.shapeWidth = src.shapeWidth; dirty = true; }
                if (existing.shapeHeight != src.shapeHeight) { existing.shapeHeight = src.shapeHeight; dirty = true; }
                if (!ArraysEqual(existing.shapeFlat, src.shapeFlat))
                {
                    existing.shapeFlat = (bool[])src.shapeFlat.Clone();
                    dirty = true;
                }

                int needed = src.shapeWidth * src.shapeHeight;
                if (existing.cellSprites == null || existing.cellSprites.Length != needed)
                {
                    var resized = new Sprite[needed];
                    if (existing.cellSprites != null)
                    {
                        int len = Mathf.Min(existing.cellSprites.Length, needed);
                        for (int i = 0; i < len; i++) resized[i] = existing.cellSprites[i];
                    }
                    existing.cellSprites = resized;
                    dirty = true;
                }

                if (string.IsNullOrEmpty(existing.designTheme))
                {
                    existing.designTheme = SuggestThemeName(src);
                    dirty = true;
                }

                if (dirty) EditorUtility.SetDirty(existing);
                result.Add(existing);
            }
        }
        return result;
    }

    // ──────────────────────────────────────────────
    //  2) Registry 作成 / 更新
    // ──────────────────────────────────────────────
    static BlockShapeRegistry CreateOrUpdateRegistry(List<BlockData> shapes)
    {
        EnsureFolder(RESOURCES_FOLDER);

        var registry = AssetDatabase.LoadAssetAtPath<BlockShapeRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<BlockShapeRegistry>();
            registry.shapes = new List<BlockData>(shapes);
            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
        }
        else
        {
            // 既存 registry に新しいシェイプを追加 (重複は除く)。順序は新規シェイプを末尾に追加。
            var set = new HashSet<BlockData>(registry.shapes);
            foreach (var s in shapes)
            {
                if (s != null && !set.Contains(s))
                {
                    registry.shapes.Add(s);
                    set.Add(s);
                }
            }
            // null 要素のクリーンアップ
            registry.shapes.RemoveAll(x => x == null);
            EditorUtility.SetDirty(registry);
        }

        return registry;
    }

    // ──────────────────────────────────────────────
    //  3) シーン内 BlockSpawner.shapeRegistry に自動アサイン
    // ──────────────────────────────────────────────
    static int WireSceneBlockSpawners(BlockShapeRegistry registry)
    {
        if (registry == null) return 0;

        var spawners = Object.FindObjectsOfType<BlockSpawner>(includeInactive: true);
        int count = 0;

        foreach (var sp in spawners)
        {
            if (sp == null) continue;
            if (sp.shapeRegistry == registry) continue;

            Undo.RecordObject(sp, "Wire BlockShapeRegistry");
            sp.shapeRegistry = registry;
            EditorUtility.SetDirty(sp);

            // シーン側に変更を保存できるよう dirty にする
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sp.gameObject.scene);
            count++;
        }

        return count;
    }

    // ──────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────
    static string SuggestThemeName(BlockData s)
    {
        int cells = s.GetCellCount();
        switch (s.blockName)
        {
            case "Square2": return "サンドイッチ (2x2)";
            case "Square3": return "ピザ (3x3)";
            case "Vert5":
            case "Horiz5": return "ホットドッグ (1x5)";
            case "Vert4":
            case "Horiz4": return "卵バー (1x4)";
            case "T":
            case "T_Down": return "T 字パン";
            case "L":
            case "L_Mirror":
            case "L_Rot1":
            case "L_Rot2": return "L 字パン";
            case "S":
            case "Z":      return "S/Z チョコ";
            case "SmallL":
            case "SmallL_Mirror": return "ミニコーナー";
            case "Single":
            case "Vert2":
            case "Horiz2":
            case "Vert3":
            case "Horiz3": return $"小さなお菓子 ({cells}セル)";
            default: return $"デザイン未設定 ({cells}セル)";
        }
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf   = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static bool ArraysEqual(bool[] a, bool[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
#endif
