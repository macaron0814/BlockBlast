#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using BlockBlastGame;

public static class BlockBlastSceneBuilder
{
    const string ROOT     = "Assets/Generated";
    const string TILES    = "Assets/Generated/Tiles";
    const string BLOCKS   = "Assets/Generated/Blocks";
    const string TEXTURES = "Assets/Generated/Textures";

    // ──────────────────────────────────────────────
    //  Menu Entry
    // ──────────────────────────────────────────────
    [MenuItem("Tools/BlockBlast/Build Scene (Pre-generate)")]
    static void BuildScene()
    {
        if (Object.FindObjectOfType<GameManager>() != null)
        {
            bool rebuild = EditorUtility.DisplayDialog(
                "BlockBlast Scene Builder",
                "Scene already has a GameManager. Rebuild from scratch?",
                "Rebuild", "Cancel");
            if (!rebuild) return;
            CleanupExisting();
        }

        EnsureFolders();

        var sprite     = GetOrCreateSquareSprite();
        var tiles      = GetOrCreateTiles(sprite);
        var blockShapes = GetOrCreateBlockShapes();

        // ---- managers ----（boardManager を先に作成してサイズを確定させる）
        var gmObj           = new GameObject("GameManager");
        var gm              = gmObj.AddComponent<GameManager>();
        var boardManager    = CreateChild(gmObj, "BoardManager")   .AddComponent<BoardManager>();
        var lineClearSystem = CreateChild(gmObj, "LineClearSystem").AddComponent<LineClearSystem>();
        var comboSystem     = CreateChild(gmObj, "ComboSystem")    .AddComponent<ComboSystem>();
        var turnManager     = CreateChild(gmObj, "TurnManager")    .AddComponent<TurnManager>();
        var stageManager    = CreateChild(gmObj, "StageManager")   .AddComponent<StageManager>();
        var itemSystem      = CreateChild(gmObj, "ItemSystem")     .AddComponent<ItemSystem>();
        var chaseSystem     = CreateChild(gmObj, "ChaseSystem")    .AddComponent<ChaseSystem>();
        var roguelikeSystem = CreateChild(gmObj, "RoguelikeSystem").AddComponent<RoguelikeSystem>();
        var spaceshipBuilder= CreateChild(gmObj, "SpaceshipBuilder").AddComponent<SpaceshipBuilder>();

        // ---- grid + tilemaps ----
        var gridObj = new GameObject("Grid");
        gridObj.AddComponent<Grid>().cellSize = new Vector3(1, 1, 0);
        var tilemapController = gridObj.AddComponent<TilemapController>();
        tilemapController.boardTilemap   = CreateTilemapLayer(gridObj.transform, "BoardTilemap",   0);
        tilemapController.blockTilemap   = CreateTilemapLayer(gridObj.transform, "BlockTilemap",   1);
        tilemapController.previewTilemap = CreateTilemapLayer(gridObj.transform, "PreviewTilemap", 2);
        tilemapController.boardTile          = tiles[0];
        tilemapController.previewValidTile   = tiles[1];
        tilemapController.previewInvalidTile = tiles[2];
        tilemapController.colorTiles = new TileBase[]
            { tiles[3], tiles[4], tiles[5], tiles[6], tiles[7], tiles[8], tiles[9] };

        // ---- カメラ（boardManager のサイズで計算）----
        SetupCamera(boardManager.boardWidth, boardManager.boardHeight);

        // ---- block spawner ----
        var spawnerObj   = new GameObject("BlockSpawner");
        var blockSpawner = spawnerObj.AddComponent<BlockSpawner>();
        blockSpawner.allShapes = blockShapes;

        int   bw      = boardManager.boardWidth;
        int   bh      = boardManager.boardHeight;
        float centerX = (bw - 1) * 0.5f;
        float spacing = bw / 3f;
        float spawnY  = -bh * 0.3f;

        var spawnPoints = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var pt = new GameObject($"SpawnPoint_{i}");
            pt.transform.SetParent(spawnerObj.transform);
            pt.transform.position = new Vector3(centerX + (i - 1) * spacing, spawnY, 0);
            spawnPoints[i] = pt.transform;
        }
        blockSpawner.spawnPoints = spawnPoints;

        var dragHandler = new GameObject("BlockDragHandler").AddComponent<BlockDragHandler>();

        // ---- UI ----
        var uiManager = BuildUI();

        // ---- wire references ----
        gm.boardManager     = boardManager;
        gm.blockSpawner     = blockSpawner;
        gm.turnManager      = turnManager;
        gm.stageManager     = stageManager;
        gm.lineClearSystem  = lineClearSystem;
        gm.comboSystem      = comboSystem;
        gm.itemSystem       = itemSystem;
        gm.chaseSystem      = chaseSystem;
        gm.roguelikeSystem  = roguelikeSystem;
        gm.spaceshipBuilder = spaceshipBuilder;
        gm.uiManager        = uiManager;

        boardManager.tilemapController        = tilemapController;
        lineClearSystem.boardManager          = boardManager;
        lineClearSystem.tilemapController     = tilemapController;
        itemSystem.boardManager               = boardManager;
        itemSystem.tilemapController          = tilemapController;
        itemSystem.turnManager                = turnManager;
        chaseSystem.turnManager               = turnManager;
        dragHandler.boardManager              = boardManager;
        dragHandler.blockSpawner              = blockSpawner;
        dragHandler.mainCamera                = Camera.main;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[BlockBlast] Scene built! All objects pre-generated. Press Play to run.");
    }

    // ──────────────────────────────────────────────
    //  Cleanup
    // ──────────────────────────────────────────────
    static void CleanupExisting()
    {
        string[] targets = { "GameManager", "Grid", "BlockSpawner",
                             "BlockDragHandler", "Canvas", "EventSystem", "GameSetup" };
        foreach (var name in targets)
        {
            var obj = GameObject.Find(name);
            if (obj != null) Object.DestroyImmediate(obj);
        }
    }

    // ──────────────────────────────────────────────
    //  Asset Helpers
    // ──────────────────────────────────────────────
    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(ROOT))     AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(TILES))    AssetDatabase.CreateFolder(ROOT, "Tiles");
        if (!AssetDatabase.IsValidFolder(BLOCKS))   AssetDatabase.CreateFolder(ROOT, "Blocks");
        if (!AssetDatabase.IsValidFolder(TEXTURES)) AssetDatabase.CreateFolder(ROOT, "Textures");
    }

    static Sprite GetOrCreateSquareSprite()
    {
        const string assetPath = TEXTURES + "/SquareTile.png";
        string absPath = Path.Combine(Application.dataPath,
                                      "Generated/Textures/SquareTile.png");

        if (!File.Exists(absPath))
        {
            const int size = 32;
            var tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || x == size - 1 || y == 0 || y == size - 1;
                    pixels[y * size + x] = border
                        ? new Color(0, 0, 0, 0.3f)
                        : Color.white;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(absPath, tex.EncodeToPNG());
            AssetDatabase.Refresh();

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                importer.textureType       = TextureImporterType.Sprite;
                importer.filterMode        = FilterMode.Point;
                importer.spritePixelsPerUnit = 32;
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static TileBase[] GetOrCreateTiles(Sprite sprite)
    {
        var info = new (string name, Color color)[]
        {
            ("BoardTile",          new Color(0.25f, 0.25f, 0.35f)),
            ("PreviewValidTile",   new Color(1f, 1f, 1f, 0.3f)),
            ("PreviewInvalidTile", new Color(1f, 0.2f, 0.2f, 0.3f)),
            ("ColorTile_Red",      new Color(0.9f,  0.2f,  0.2f)),
            ("ColorTile_Blue",     new Color(0.2f,  0.4f,  0.9f)),
            ("ColorTile_Green",    new Color(0.2f,  0.8f,  0.3f)),
            ("ColorTile_Yellow",   new Color(0.95f, 0.85f, 0.2f)),
            ("ColorTile_Purple",   new Color(0.7f,  0.2f,  0.9f)),
            ("ColorTile_Orange",   new Color(0.95f, 0.55f, 0.1f)),
            ("ColorTile_Cyan",     new Color(0.2f,  0.85f, 0.9f)),
        };

        var result = new TileBase[info.Length];
        for (int i = 0; i < info.Length; i++)
        {
            string path = $"{TILES}/{info[i].name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color  = info[i].color;
                AssetDatabase.CreateAsset(tile, path);
            }
            result[i] = tile;
        }
        return result;
    }

    static List<BlockData> GetOrCreateBlockShapes()
    {
        var raw    = BlockShapeLibrary.GenerateAllShapes();
        var result = new List<BlockData>(raw.Count);

        foreach (var shape in raw)
        {
            string path     = $"{BLOCKS}/{shape.blockName}.asset";
            var existing    = AssetDatabase.LoadAssetAtPath<BlockData>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(shape, path);
                existing = shape;
            }
            result.Add(existing);
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    // ──────────────────────────────────────────────
    //  Camera
    // ──────────────────────────────────────────────
    static void SetupCamera(int boardWidth = 8, int boardHeight = 8)
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic      = true;
        cam.orthographicSize  = (boardHeight + 6f) / 2f;
        cam.transform.position = new Vector3((boardWidth - 1) * 0.5f, (boardHeight - 1) * 0.5f, -10f);
        cam.backgroundColor   = new Color(0.12f, 0.12f, 0.18f);
    }

    // ──────────────────────────────────────────────
    //  UI Builder
    // ──────────────────────────────────────────────
    static UIManager BuildUI()
    {
        var canvasObj = new GameObject("Canvas");
        var canvas    = canvasObj.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        var uiObj     = CreateUIChild(canvasObj.transform, "UIManager");
        var uiManager = uiObj.AddComponent<UIManager>();

        BuildHUD(canvasObj.transform, uiManager);
        BuildGameOverPanel(canvasObj.transform, uiManager);
        BuildStageTransitionPanel(canvasObj.transform, uiManager);
        BuildSpaceshipPanel(canvasObj.transform, uiManager);

        var evtSys = new GameObject("EventSystem");
        evtSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
        evtSys.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        return uiManager;
    }

    static void BuildHUD(Transform canvas, UIManager ui)
    {
        var hud  = CreateUIChild(canvas, "HUD");
        var rect = hud.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        ui.turnText = MakeText(hud.transform, "TurnText", "TURN: 20",
            new Vector2(0,1), new Vector2(0,1), new Vector2(0,1),
            new Vector2(10,-10), new Vector2(300,60),
            TextAnchor.UpperLeft, 32, FontStyle.Bold, Color.white);

        ui.scoreText = MakeText(hud.transform, "ScoreText", "SCORE: 0",
            new Vector2(0,1), new Vector2(0,1), new Vector2(0,1),
            new Vector2(10,-70), new Vector2(250,40),
            TextAnchor.UpperLeft, 22, FontStyle.Normal, Color.white);

        ui.stageText = MakeText(hud.transform, "StageText", "STAGE 1/5",
            new Vector2(1,1), new Vector2(1,1), new Vector2(1,1),
            new Vector2(-10,-10), new Vector2(250,40),
            TextAnchor.UpperRight, 22, FontStyle.Normal, Color.white);

        ui.lineProgressText = MakeText(hud.transform, "LineProgressText", "LINE: 0/10",
            new Vector2(1,1), new Vector2(1,1), new Vector2(1,1),
            new Vector2(-10,-50), new Vector2(250,40),
            TextAnchor.UpperRight, 24, FontStyle.Bold, Color.white);

        ui.comboText = MakeText(hud.transform, "ComboText", "",
            new Vector2(0.5f,1), new Vector2(0.5f,1), new Vector2(0.5f,1),
            new Vector2(0,-80), new Vector2(400,50),
            TextAnchor.UpperCenter, 28, FontStyle.Bold, Color.yellow);
        ui.comboText.gameObject.SetActive(false);

        var overlay     = CreateUIChild(canvas, "UrgencyOverlay");
        var overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        ui.urgencyOverlay = overlay.AddComponent<Image>();
        ui.urgencyOverlay.color          = new Color(1,0,0,0);
        ui.urgencyOverlay.raycastTarget  = false;
    }

    static void BuildGameOverPanel(Transform canvas, UIManager ui)
    {
        var panel = MakePanel(canvas, "GameOverPanel");
        ui.gameOverPanel = panel;
        panel.SetActive(false);

        ui.gameOverTitle = MakeText(panel.transform, "Title", "GAME OVER",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0,100), new Vector2(500,60),
            TextAnchor.MiddleCenter, 36, FontStyle.Bold, Color.white);

        ui.gameOverMessage = MakeText(panel.transform, "Message", "",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0,0), new Vector2(500,150),
            TextAnchor.MiddleCenter, 22, FontStyle.Normal, Color.white);

        ui.restartButton = MakeButton(panel.transform, "RestartButton", "RETRY",
            new Vector2(0,-120), new Vector2(200,50));
    }

    static void BuildStageTransitionPanel(Transform canvas, UIManager ui)
    {
        var panel = MakePanel(canvas, "StageTransitionPanel");
        ui.stageTransitionPanel = panel;
        panel.SetActive(false);

        ui.stageClearText = MakeText(panel.transform, "StageClearText", "STAGE CLEAR!",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0,150), new Vector2(500,60),
            TextAnchor.MiddleCenter, 36, FontStyle.Bold, Color.white);

        MakeText(panel.transform, "SelectPerkText", "Select a perk:",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0,80), new Vector2(500,40),
            TextAnchor.MiddleCenter, 20, FontStyle.Normal, Color.white);

        ui.perkButtons = new Button[3];
        ui.perkTexts   = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            float y = 20 - i * 80;
            var btn = MakeButton(panel.transform, $"PerkButton_{i}", "Perk",
                new Vector2(0, y), new Vector2(400, 65));
            ui.perkButtons[i] = btn;
            ui.perkTexts[i]   = btn.GetComponentInChildren<Text>();
        }
    }

    static void BuildSpaceshipPanel(Transform canvas, UIManager ui)
    {
        var panel = MakePanel(canvas, "SpaceshipPanel");
        ui.spaceshipPanel = panel;
        panel.SetActive(false);

        MakeText(panel.transform, "Title", "SPACESHIP COMPLETE!",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0,150), new Vector2(500,60),
            TextAnchor.MiddleCenter, 36, FontStyle.Bold, Color.white);

        ui.spaceshipInfoText = MakeText(panel.transform, "InfoText", "",
            new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f), new Vector2(0.5f,0.5f),
            new Vector2(0,20), new Vector2(500,200),
            TextAnchor.MiddleCenter, 22, FontStyle.Normal, Color.white);

        ui.launchButton = MakeButton(panel.transform, "LaunchButton", "LAUNCH!",
            new Vector2(0,-140), new Vector2(300,60));
    }

    // ──────────────────────────────────────────────
    //  Low-level UI Helpers
    // ──────────────────────────────────────────────
    static Font _font;
    static Font GetFont()
    {
        if (_font == null)
            _font = Resources.Load<Font>("GameFont");
        return _font;
    }

    static GameObject CreateUIChild(Transform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    static GameObject MakePanel(Transform parent, string name)
    {
        var obj  = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        obj.AddComponent<Image>().color = new Color(0,0,0,0.85f);
        return obj;
    }

    static Text MakeText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 position, Vector2 size,
        TextAnchor alignment, int fontSize, FontStyle style, Color color)
    {
        var obj  = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin       = anchorMin;
        rect.anchorMax       = anchorMax;
        rect.pivot           = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta       = size;

        var t = obj.AddComponent<Text>();
        t.text              = text;
        t.font              = GetFont();
        t.fontSize          = fontSize;
        t.fontStyle         = style;
        t.color             = color;
        t.alignment         = alignment;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.supportRichText    = true;
        return t;
    }

    static Button MakeButton(Transform parent, string name, string label,
        Vector2 position, Vector2 size)
    {
        var obj  = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin       = new Vector2(0.5f, 0.5f);
        rect.anchorMax       = new Vector2(0.5f, 0.5f);
        rect.pivot           = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta       = size;

        var img = obj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.5f, 0.9f);

        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtObj  = new GameObject("Text", typeof(RectTransform));
        txtObj.transform.SetParent(obj.transform, false);
        var txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        var t = txtObj.AddComponent<Text>();
        t.text              = label;
        t.font              = GetFont();
        t.fontSize          = 20;
        t.color             = Color.white;
        t.alignment         = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return btn;
    }

    // ──────────────────────────────────────────────
    //  Tilemap Helpers
    // ──────────────────────────────────────────────
    static Tilemap CreateTilemapLayer(Transform parent, string name, int sortOrder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent);
        var tm  = obj.AddComponent<Tilemap>();
        var r   = obj.AddComponent<TilemapRenderer>();
        r.sortingOrder = sortOrder;
        return tm;
    }

    // ──────────────────────────────────────────────
    //  General Helpers
    // ──────────────────────────────────────────────
    static GameObject CreateChild(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform);
        return child;
    }
}
#endif
