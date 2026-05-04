#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using BlockBlastGame;

/// <summary>
/// CSV「ステージ構成資料」のステージ 1〜8 を StageData アセットとして
/// Assets/ScriptableObjects/Stages/ 以下に書き出す。
///
/// 既存アセットがある場合は各フィールドを CSV 値で上書きする (designer 編集差分は失われる)。
/// 上書きしたくない場合は「Generate Missing Only」を使用する。
/// </summary>
public static class StageDataAssetGenerator
{
    const string FOLDER = "Assets/ScriptableObjects/Stages";
    const int    MIN_STAGE = 1;
    const int    MAX_STAGE = 8;

    [MenuItem("Tools/BlockBlast/Generate Stage Assets (Overwrite)")]
    public static void GenerateOverwrite()
    {
        Generate(overwrite: true);
    }

    [MenuItem("Tools/BlockBlast/Generate Stage Assets (Missing Only)")]
    public static void GenerateMissingOnly()
    {
        Generate(overwrite: false);
    }

    static void Generate(bool overwrite)
    {
        EnsureFolder(FOLDER);

        int created = 0;
        int updated = 0;
        int skipped = 0;

        for (int stage = MIN_STAGE; stage <= MAX_STAGE; stage++)
        {
            string path = $"{FOLDER}/Stage{stage}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageData>(path);
            var preset   = StageData.CreateDefault(stage);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(preset, path);
                created++;
            }
            else if (overwrite)
            {
                CopyValues(preset, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(preset);
                updated++;
            }
            else
            {
                Object.DestroyImmediate(preset);
                skipped++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StageDataAssetGenerator] Stage assets: {created} created, {updated} updated, {skipped} skipped. Folder: {FOLDER}");
    }

    static void CopyValues(StageData src, StageData dst)
    {
        dst.stageNumber          = src.stageNumber;
        dst.stageFeatureNote     = src.stageFeatureNote;
        dst.timeLimitSeconds     = src.timeLimitSeconds;
        dst.linesToClear         = src.linesToClear;
        dst.initialTurns         = src.initialTurns;
        dst.turnsPerLineClear    = src.turnsPerLineClear;
        dst.initialMaxBlockCells = src.initialMaxBlockCells;
        dst.difficultyMultiplier = src.difficultyMultiplier;
        dst.itemCount            = src.itemCount;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf   = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
