#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using BlockBlastGame;

/// <summary>
/// CSV「ステージ構成資料」の敵 (ザコA / ザコA(小) / ザコB / ザコC / ザコD / 中ボス / ボス) と
/// 各ステージの Wave 構成を一括生成し、シーン上の EnemySystem に自動配線するユーティリティ。
///
/// 生成先:
///   Assets/ScriptableObjects/Enemies/   ... EnemyData (7 体)
///   Assets/ScriptableObjects/Waves/     ... EnemyWaveData (Stage 1〜8)
///
/// 既存の Assets/EnemyData/ 以下のスプライト等は触らず、最初の EnemyData アセットを
/// 「ビジュアルテンプレート」として frames / hitEffectFrames 等を新アセットへコピーする。
/// </summary>
public static class EnemyAssetGenerator
{
    const string ENEMIES_FOLDER = "Assets/ScriptableObjects/Enemies";
    const string WAVES_FOLDER   = "Assets/ScriptableObjects/Waves";
    const string LEGACY_ENEMY_FOLDER = "Assets/EnemyData";

    // ──────────────────────────────────────────────
    //  CSV 準拠のキー (内部識別用)
    // ──────────────────────────────────────────────
    const string K_ZakoA      = "ZakoA";
    const string K_ZakoASmall = "ZakoASmall";
    const string K_ZakoB      = "ZakoB";
    const string K_ZakoC      = "ZakoC";
    const string K_ZakoD      = "ZakoD";
    const string K_MidBoss    = "MidBoss";
    const string K_Boss       = "Boss";

    [MenuItem("Tools/BlockBlast/Setup Enemy & Wave Pipeline")]
    public static void SetupPipeline()
    {
        var enemies = GenerateEnemyAssets();
        var waves   = GenerateWaveAssets(enemies);
        int wired   = WireSceneEnemySystem(waves);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "BlockBlast / Setup Enemy & Wave Pipeline",
            $"Enemy アセット: {enemies.Count} 体\n  → {ENEMIES_FOLDER}\n\n" +
            $"Wave アセット (Stage 1〜8): {waves.Length} 個\n  → {WAVES_FOLDER}\n\n" +
            $"シーン内 EnemySystem.stageWaves に自動配線: {wired} 個\n\n" +
            "次のステップ:\n" +
            "  - 各 EnemyData の見た目 (frames) を必要に応じて差し替え\n" +
            "  - 各 EnemyWaveData の routeNodes (Shop/Cake/自販機) を確認",
            "OK");
    }

    [MenuItem("Tools/BlockBlast/Wire Enemy Waves to Scene EnemySystem")]
    public static void RewireOnly()
    {
        var waves = LoadStageWaves();
        if (waves == null || waves.Length == 0)
        {
            EditorUtility.DisplayDialog("BlockBlast",
                "Wave アセットが見つかりません。先に [Setup Enemy & Wave Pipeline] を実行してください。", "OK");
            return;
        }
        int wired = WireSceneEnemySystem(waves);
        EditorUtility.DisplayDialog("BlockBlast",
            $"シーン内 EnemySystem.stageWaves に自動配線: {wired} 個", "OK");
    }

    // ──────────────────────────────────────────────
    //  1) Enemy アセット生成
    // ──────────────────────────────────────────────
    static Dictionary<string, EnemyData> GenerateEnemyAssets()
    {
        EnsureFolder(ENEMIES_FOLDER);

        // 既存 Assets/EnemyData/EnemyData.asset などをビジュアルテンプレートとして利用
        var templates = LoadLegacyEnemyDataTemplates();
        var visualTemplate = templates.Count > 0 ? templates[0] : null;

        var specs = new[]
        {
            new EnemySpec(K_ZakoA,      "ザコA",      maxHP: 30,  chaseSpeed: 3f,  drop: 100,  scale: 0.6f, hover: 1f),
            new EnemySpec(K_ZakoASmall, "ザコA(小)",  maxHP: 10,  chaseSpeed: 4f,  drop: 100,  scale: 0.45f, hover: 1f),
            new EnemySpec(K_ZakoB,      "ザコB",      maxHP: 20,  chaseSpeed: 3.5f,drop: 500,  scale: 0.6f, hover: 1f),
            new EnemySpec(K_ZakoC,      "ザコC",      maxHP: 60,  chaseSpeed: 1f,  drop: 500,  scale: 0.85f, hover: 1f),
            new EnemySpec(K_ZakoD,      "ザコD",      maxHP: 10,  chaseSpeed: 7f,  drop: 500,  scale: 0.55f, hover: 1f),
            new EnemySpec(K_MidBoss,    "中ボス",     maxHP: 75,  chaseSpeed: 3f,  drop: 1000, scale: 1.0f,  hover: 1f),
            new EnemySpec(K_Boss,       "ボス",       maxHP: 150, chaseSpeed: 1f,  drop: 5000, scale: 1.4f,  hover: 1f),
        };

        var result = new Dictionary<string, EnemyData>();

        foreach (var spec in specs)
        {
            string path = $"{ENEMIES_FOLDER}/{spec.Key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(path);

            if (existing == null)
            {
                var asset = ScriptableObject.CreateInstance<EnemyData>();
                ApplySpec(asset, spec, visualTemplate);
                asset.name = spec.Key;
                AssetDatabase.CreateAsset(asset, path);
                result[spec.Key] = asset;
            }
            else
            {
                // ステータス値のみ上書き (既に手動編集された frames は維持)
                bool dirty = ApplyStatsOnly(existing, spec);
                if (existing.frames == null || existing.frames.Length == 0)
                {
                    if (visualTemplate != null && visualTemplate.frames != null)
                    {
                        existing.frames          = (Sprite[])visualTemplate.frames.Clone();
                        existing.stunFrames      = visualTemplate.stunFrames != null ? (Sprite[])visualTemplate.stunFrames.Clone() : null;
                        existing.hitEffectFrames = visualTemplate.hitEffectFrames != null ? (Sprite[])visualTemplate.hitEffectFrames.Clone() : null;
                        existing.frameRate       = visualTemplate.frameRate > 0 ? visualTemplate.frameRate : existing.frameRate;
                        dirty = true;
                    }
                }
                if (dirty) EditorUtility.SetDirty(existing);
                result[spec.Key] = existing;
            }
        }

        return result;
    }

    static bool ApplyStatsOnly(EnemyData asset, EnemySpec spec)
    {
        bool dirty = false;
        if (asset.maxHP != spec.MaxHP)               { asset.maxHP = spec.MaxHP; dirty = true; }
        if (!Mathf.Approximately(asset.chaseSpeed, spec.ChaseSpeed)) { asset.chaseSpeed = spec.ChaseSpeed; dirty = true; }
        if (asset.defeatBonusAmount != spec.Drop)    { asset.defeatBonusAmount = spec.Drop; dirty = true; }
        return dirty;
    }

    static void ApplySpec(EnemyData asset, EnemySpec spec, EnemyData template)
    {
        asset.maxHP             = spec.MaxHP;
        asset.chaseSpeed        = spec.ChaseSpeed;
        asset.knockbackPerHit   = template != null && template.knockbackPerHit > 0f ? template.knockbackPerHit : 1f;
        asset.stunDuration      = template != null && template.stunDuration > 0f ? template.stunDuration : 3f;
        asset.spawnDistance     = template != null && template.spawnDistance > 0f ? template.spawnDistance : 30f;
        asset.scale             = spec.Scale;
        asset.hoverHeight       = spec.Hover;
        asset.tint              = Color.white;
        asset.hitOffset         = template != null ? template.hitOffset : Vector2.zero;
        asset.hitAngleRadius    = template != null && template.hitAngleRadius > 0f ? template.hitAngleRadius : 1f;
        asset.frameRate         = template != null && template.frameRate > 0f ? template.frameRate : 0.15f;
        asset.hitEffectFrameRate= template != null && template.hitEffectFrameRate > 0f ? template.hitEffectFrameRate : 0.05f;
        asset.hitEffectScale    = template != null && template.hitEffectScale > 0f ? template.hitEffectScale : 1f;
        asset.hitEffectColor    = template != null ? template.hitEffectColor : Color.white;
        asset.defeatBonusAmount = spec.Drop;

        if (template != null)
        {
            asset.frames          = template.frames != null ? (Sprite[])template.frames.Clone() : new Sprite[0];
            asset.stunFrames      = template.stunFrames != null ? (Sprite[])template.stunFrames.Clone() : new Sprite[0];
            asset.hitEffectFrames = template.hitEffectFrames != null ? (Sprite[])template.hitEffectFrames.Clone() : new Sprite[0];
        }
        else
        {
            asset.frames          = new Sprite[0];
            asset.stunFrames      = new Sprite[0];
            asset.hitEffectFrames = new Sprite[0];
        }
    }

    static List<EnemyData> LoadLegacyEnemyDataTemplates()
    {
        var list = new List<EnemyData>();
        if (!AssetDatabase.IsValidFolder(LEGACY_ENEMY_FOLDER))
            return list;

        var guids = AssetDatabase.FindAssets("t:EnemyData", new[] { LEGACY_ENEMY_FOLDER });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var asset = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (asset != null && asset.frames != null && asset.frames.Length > 0)
                list.Add(asset);
        }
        return list;
    }

    // ──────────────────────────────────────────────
    //  2) Wave アセット生成 (CSV の Stage 1〜8)
    // ──────────────────────────────────────────────
    static EnemyWaveData[] GenerateWaveAssets(Dictionary<string, EnemyData> enemies)
    {
        EnsureFolder(WAVES_FOLDER);

        var waves = new EnemyWaveData[8];
        for (int stage = 1; stage <= 8; stage++)
        {
            string path = $"{WAVES_FOLDER}/Stage{stage}_Waves.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EnemyWaveData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyWaveData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            BuildWaveDataForStage(asset, stage, enemies);
            EditorUtility.SetDirty(asset);
            waves[stage - 1] = asset;
        }

        return waves;
    }

    static void BuildWaveDataForStage(EnemyWaveData asset, int stage, Dictionary<string, EnemyData> e)
    {
        asset.intervalBetweenWaves = 5f;
        asset.waves     = WavesForStage(stage, e);
        asset.routeNodes = RouteNodesForStage(stage, e);
        asset.survivalTime = StageSurvivalTime(stage);
    }

    static float StageSurvivalTime(int stage) => stage switch
    {
        1 => 30f,
        2 => 40f,
        3 => 60f,
        4 => 60f,
        5 => 90f,
        6 => 90f,
        7 => 120f,
        8 => 160f,
        _ => 60f,
    };

    static EnemyWaveEntry[] WavesForStage(int stage, Dictionary<string, EnemyData> e)
    {
        switch (stage)
        {
            case 1:
                // 30s / 1Wave / ザコA×4
                return new[]
                {
                    Wave(0f,  Many(e, K_ZakoA, 4)),
                };

            case 2:
                // 40s / 2Wave
                // 1/2: ザコA(小)×2 (0s)
                // 2/2: ザコA×2, ザコA(小)×2 (10s)
                return new[]
                {
                    Wave(0f,  Many(e, K_ZakoASmall, 2)),
                    Wave(10f, Concat(Many(e, K_ZakoA, 2), Many(e, K_ZakoASmall, 2))),
                };

            case 3:
                // 60s / 4Wave (CSV)
                // 1/4: ザコB, ザコA×2, ザコA(小)
                // 3/4: ザコD (5s)
                // 4/4: ザコB×2, ザコA(小)×2 (20s)
                return new[]
                {
                    Wave(0f,  Concat(One(e, K_ZakoB), Many(e, K_ZakoA, 2), One(e, K_ZakoASmall))),
                    Wave(5f,  Many(e, K_ZakoD, 1)),
                    Wave(20f, Concat(Many(e, K_ZakoB, 2), Many(e, K_ZakoASmall, 2))),
                };

            case 4:
                // 60s / 4Wave
                // 1/4: ザコC (0s)
                // 2/4: ザコA×4, ザコA(小)×4 (10s)
                // 3/4: ザコD (30s)
                // 4/4: ザコD (45s)
                return new[]
                {
                    Wave(0f,  One(e, K_ZakoC)),
                    Wave(10f, Concat(Many(e, K_ZakoA, 4), Many(e, K_ZakoASmall, 4))),
                    Wave(30f, One(e, K_ZakoD)),
                    Wave(45f, One(e, K_ZakoD)),
                };

            case 5:
                // 90s / 4Wave
                // 1/4: ザコB×4 (0s)
                // 2/4: ザコC×2 (20s)
                // 3/4: ザコA(小)×4 (50s)
                // 4/4: ザコD×4 (75s)
                return new[]
                {
                    Wave(0f,  Many(e, K_ZakoB, 4)),
                    Wave(20f, Many(e, K_ZakoC, 2)),
                    Wave(50f, Many(e, K_ZakoASmall, 4)),
                    Wave(75f, Many(e, K_ZakoD, 4)),
                };

            case 6:
                // 90s / 4Wave (CSV では 1/4 行が欠落しているため軽量な Wave を補完)
                // 2/4: 中ボス (10s)
                // 3/4: ザコB×2, ザコA(小)×4, ザコD×2 (30s)
                // 4/4: ザコA×4, ザコA(小)×4 (50s)
                return new[]
                {
                    Wave(0f,  Many(e, K_ZakoA, 2)),                                            // 補完
                    Wave(10f, One(e, K_MidBoss)),
                    Wave(30f, Concat(Many(e, K_ZakoB, 2), Many(e, K_ZakoASmall, 4), Many(e, K_ZakoD, 2))),
                    Wave(50f, Concat(Many(e, K_ZakoA, 4), Many(e, K_ZakoASmall, 4))),
                };

            case 7:
                // 120s / 8Wave (CSV)。空 Wave (敵なし) は省略し、enemies だけで構成。
                // 1/8: ザコB×2, ザコA(小)×4, ザコD×2 (0s)
                // 2/8: ザコC×2, ザコB×2 (15s)
                // 3/8: ザコA(小)×4, ザコD×2 (30s)
                // 6/8: ザコD×4 (80s)
                return new[]
                {
                    Wave(0f,  Concat(Many(e, K_ZakoB, 2), Many(e, K_ZakoASmall, 4), Many(e, K_ZakoD, 2))),
                    Wave(15f, Concat(Many(e, K_ZakoC, 2), Many(e, K_ZakoB, 2))),
                    Wave(30f, Concat(Many(e, K_ZakoASmall, 4), Many(e, K_ZakoD, 2))),
                    Wave(80f, Many(e, K_ZakoD, 4)),
                };

            case 8:
                // 160s / 12Wave (ボス戦)
                // 1/12: ザコA×4 (0s)
                // 2/12: ザコD×4 (20s)
                // 4/12: 中ボス, ザコB×2, ザコA×4 (40s)
                // 5/12: ザコC×2, ザコB×2 (50s)
                // 7/12: ボス (80s)
                // 8/12: ザコA×4, ザコA×4 (90s)
                // 9/12: ザコC×2 (100s)
                return new[]
                {
                    Wave(0f,   Many(e, K_ZakoA, 4)),
                    Wave(20f,  Many(e, K_ZakoD, 4)),
                    Wave(40f,  Concat(One(e, K_MidBoss), Many(e, K_ZakoB, 2), Many(e, K_ZakoA, 4))),
                    Wave(50f,  Concat(Many(e, K_ZakoC, 2), Many(e, K_ZakoB, 2))),
                    Wave(80f,  One(e, K_Boss)),
                    Wave(90f,  Many(e, K_ZakoA, 8)),
                    Wave(100f, Many(e, K_ZakoC, 2)),
                };
        }
        return new EnemyWaveEntry[0];
    }

    /// <summary>
    /// CSV 道中マスを RouteNodeConfig の配列にマッピングする。
    /// 配列要素は survivalTime を等分して順に消化されるので、
    /// 「タイミング的な近さ」で並べる。
    /// </summary>
    static RouteNodeConfig[] RouteNodesForStage(int stage, Dictionary<string, EnemyData> e)
    {
        switch (stage)
        {
            case 1:
                return new[] { Node(RouteEventType.None) };

            case 2:
                return new[]
                {
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                };

            case 3:
                // 60s / 4 マス → 各 15s。30s で自販機 (= node[1] 終端 = 30s 消化時点)
                // CSV: Wave 2/4 (= 15s 周辺) で +1 Cake、30s で自販機
                return new[]
                {
                    NodeCake(maxCellIncrease: 1),    // ~15s で +1 (4 ブロックまで)
                    Node(RouteEventType.VendingMachine), // ~30s 自販機
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                };

            case 4:
                return new[]
                {
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                };

            case 5:
                // 90s / 4 マス → 各 22.5s。CSV: Wave 2/4 (= 20s) で +1 (5ブロック)
                return new[]
                {
                    NodeCake(maxCellIncrease: 1),    // ~22.5s で +1 (5 ブロックまで)
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                };

            case 6:
                return new[]
                {
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                    Node(RouteEventType.None),
                };

            case 7:
                // 120s / 4 マス → 各 30s
                // CSV: Wave 4/8 (= 45-60s) で +2 全ブロック解放、60s で自販機
                return new[]
                {
                    Node(RouteEventType.None),                  // ~30s
                    NodeCakeUnlockAll(),                        // ~60s で全ブロック解放 (CSV Wave 4)
                    Node(RouteEventType.VendingMachine),        // ~90s 自販機 (CSV 60s に近い順送り)
                    Node(RouteEventType.None),                  // ~120s
                };

            case 8:
                // 160s / 4 マス → 各 40s
                // CSV: 自販機 40s, 80s, 120s。ボスは Wave 側で出現させる。
                return new[]
                {
                    Node(RouteEventType.VendingMachine),        // ~40s
                    Node(RouteEventType.VendingMachine),        // ~80s
                    Node(RouteEventType.VendingMachine),        // ~120s
                    Node(RouteEventType.None),                  // ~160s
                };
        }

        return new RouteNodeConfig[] { Node(RouteEventType.None) };
    }

    // ──────────────────────────────────────────────
    //  3) シーン内 EnemySystem.stageWaves を自動配線
    // ──────────────────────────────────────────────
    static int WireSceneEnemySystem(EnemyWaveData[] waves)
    {
        if (waves == null || waves.Length == 0) return 0;

        var systems = Object.FindObjectsOfType<EnemySystem>(includeInactive: true);
        int count = 0;

        foreach (var sys in systems)
        {
            if (sys == null) continue;

            Undo.RecordObject(sys, "Wire EnemySystem stageWaves");
            sys.stageWaves = waves;

            // stageSurvivalTimes も CSV 値で同期
            sys.stageSurvivalTimes = new float[waves.Length];
            for (int i = 0; i < waves.Length; i++)
                sys.stageSurvivalTimes[i] = StageSurvivalTime(i + 1);

            EditorUtility.SetDirty(sys);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sys.gameObject.scene);
            count++;
        }

        return count;
    }

    static EnemyWaveData[] LoadStageWaves()
    {
        if (!AssetDatabase.IsValidFolder(WAVES_FOLDER)) return null;
        var list = new List<EnemyWaveData>();
        for (int stage = 1; stage <= 8; stage++)
        {
            string path = $"{WAVES_FOLDER}/Stage{stage}_Waves.asset";
            var w = AssetDatabase.LoadAssetAtPath<EnemyWaveData>(path);
            if (w != null) list.Add(w);
        }
        return list.ToArray();
    }

    // ──────────────────────────────────────────────
    //  Wave 構築ヘルパー
    // ──────────────────────────────────────────────
    static EnemyWaveEntry Wave(float startTime, EnemyData[] enemies)
    {
        return new EnemyWaveEntry
        {
            enemies = enemies,
            spawnInterval = 1f,
            startTimeSeconds = startTime,
        };
    }

    static EnemyData[] One(Dictionary<string, EnemyData> dict, string key)
    {
        if (dict.TryGetValue(key, out var d) && d != null) return new[] { d };
        return new EnemyData[0];
    }

    static EnemyData[] Many(Dictionary<string, EnemyData> dict, string key, int count)
    {
        if (count <= 0 || !dict.TryGetValue(key, out var d) || d == null) return new EnemyData[0];
        var result = new EnemyData[count];
        for (int i = 0; i < count; i++) result[i] = d;
        return result;
    }

    static EnemyData[] Concat(params EnemyData[][] arrays)
    {
        int total = 0;
        foreach (var a in arrays) total += a?.Length ?? 0;
        var result = new EnemyData[total];
        int idx = 0;
        foreach (var a in arrays)
        {
            if (a == null) continue;
            System.Array.Copy(a, 0, result, idx, a.Length);
            idx += a.Length;
        }
        return result;
    }

    // ──────────────────────────────────────────────
    //  RouteNode ヘルパー
    // ──────────────────────────────────────────────
    static RouteNodeConfig Node(RouteEventType type)
    {
        return new RouteNodeConfig { eventType = type };
    }

    static RouteNodeConfig NodeCake(int maxCellIncrease)
    {
        return new RouteNodeConfig
        {
            eventType = RouteEventType.Cake,
            maxCellIncrease = maxCellIncrease,
            unlockAllShapes = false,
        };
    }

    static RouteNodeConfig NodeCakeUnlockAll()
    {
        return new RouteNodeConfig
        {
            eventType = RouteEventType.Cake,
            maxCellIncrease = 0,
            unlockAllShapes = true,
        };
    }

    // ──────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────
    struct EnemySpec
    {
        public string Key;
        public string DisplayName;
        public int    MaxHP;
        public float  ChaseSpeed;
        public int    Drop;
        public float  Scale;
        public float  Hover;

        public EnemySpec(string key, string display, int maxHP, float chaseSpeed, int drop, float scale, float hover)
        {
            Key = key; DisplayName = display;
            MaxHP = maxHP; ChaseSpeed = chaseSpeed; Drop = drop;
            Scale = scale; Hover = hover;
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
}
#endif
