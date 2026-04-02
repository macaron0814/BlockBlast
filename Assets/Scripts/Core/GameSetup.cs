using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;

namespace BlockBlastGame
{
    [DefaultExecutionOrder(-100)]
    public class GameSetup : MonoBehaviour
    {
        [Header("Auto-generated at runtime if not assigned")]
        public bool autoSetup = true;

        BoardManager boardManager;
        TilemapController tilemapController;
        LineClearSystem lineClearSystem;
        ComboSystem comboSystem;
        TurnManager turnManager;
        StageManager stageManager;
        ItemSystem itemSystem;
        ChaseSystem chaseSystem;
        RoguelikeSystem roguelikeSystem;
        SpaceshipBuilder spaceshipBuilder;
        BlockSpawner blockSpawner;
        BlockDragHandler dragHandler;
        UIManager uiManager;

        void Awake()
        {
            if (!autoSetup) return;

            // Scene was pre-built by BlockBlastSceneBuilder — skip runtime setup
            if (FindObjectOfType<GameManager>() != null) return;

            SetupManagers();       // boardManager を先に作成
            SetupCamera();         // boardManager のサイズを使って計算
            SetupTilemaps();
            SetupBlockSpawner();   // boardManager のサイズを使って計算
            SetupUI();
            WireReferences();
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            int w = boardManager != null ? boardManager.boardWidth  : 8;
            int h = boardManager != null ? boardManager.boardHeight : 8;

            cam.orthographic     = true;
            // ボードの高さ + 上部UI余白(2.5) + 下部スポーナー余白(3.5) = h+6 → 半分がorthoSize
            cam.orthographicSize = (h + 6f) / 2f;
            // X: ボード中央、Y: ボード中央（上下余白差を吸収）
            cam.transform.position = new Vector3((w - 1) * 0.5f, (h - 1) * 0.5f, -10f);
            cam.backgroundColor  = new Color(0.12f, 0.12f, 0.18f);
        }

        void SetupManagers()
        {
            var gmObj = new GameObject("GameManager");
            var gm = gmObj.AddComponent<GameManager>();

            var boardObj = CreateChild(gmObj, "BoardManager");
            boardManager = boardObj.AddComponent<BoardManager>();

            var lineClearObj = CreateChild(gmObj, "LineClearSystem");
            lineClearSystem = lineClearObj.AddComponent<LineClearSystem>();

            var comboObj = CreateChild(gmObj, "ComboSystem");
            comboSystem = comboObj.AddComponent<ComboSystem>();

            var turnObj = CreateChild(gmObj, "TurnManager");
            turnManager = turnObj.AddComponent<TurnManager>();

            var stageObj = CreateChild(gmObj, "StageManager");
            stageManager = stageObj.AddComponent<StageManager>();

            var itemObj = CreateChild(gmObj, "ItemSystem");
            itemSystem = itemObj.AddComponent<ItemSystem>();

            var chaseObj = CreateChild(gmObj, "ChaseSystem");
            chaseSystem = chaseObj.AddComponent<ChaseSystem>();

            var rogueObj = CreateChild(gmObj, "RoguelikeSystem");
            roguelikeSystem = rogueObj.AddComponent<RoguelikeSystem>();

            var spaceObj = CreateChild(gmObj, "SpaceshipBuilder");
            spaceshipBuilder = spaceObj.AddComponent<SpaceshipBuilder>();
        }

        void SetupTilemaps()
        {
            var gridObj = new GameObject("Grid");
            var grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            tilemapController = gridObj.AddComponent<TilemapController>();

            tilemapController.boardTilemap = CreateTilemapLayer(gridObj.transform, "BoardTilemap", 0);
            tilemapController.blockTilemap = CreateTilemapLayer(gridObj.transform, "BlockTilemap", 1);
            tilemapController.previewTilemap = CreateTilemapLayer(gridObj.transform, "PreviewTilemap", 2);

            tilemapController.boardTile = CreateColorTile(new Color(0.25f, 0.25f, 0.35f));
            tilemapController.previewValidTile = CreateColorTile(new Color(1f, 1f, 1f, 0.3f));
            tilemapController.previewInvalidTile = CreateColorTile(new Color(1f, 0.2f, 0.2f, 0.3f));

            tilemapController.colorTiles = new TileBase[]
            {
                CreateColorTile(new Color(0.9f, 0.2f, 0.2f)),   // Red
                CreateColorTile(new Color(0.2f, 0.4f, 0.9f)),   // Blue
                CreateColorTile(new Color(0.2f, 0.8f, 0.3f)),   // Green
                CreateColorTile(new Color(0.95f, 0.85f, 0.2f)), // Yellow
                CreateColorTile(new Color(0.7f, 0.2f, 0.9f)),   // Purple
                CreateColorTile(new Color(0.95f, 0.55f, 0.1f)), // Orange
                CreateColorTile(new Color(0.2f, 0.85f, 0.9f)),  // Cyan
            };
        }

        void SetupBlockSpawner()
        {
            var spawnerObj = new GameObject("BlockSpawner");
            blockSpawner = spawnerObj.AddComponent<BlockSpawner>();

            int   w           = boardManager != null ? boardManager.boardWidth  : 8;
            int   h           = boardManager != null ? boardManager.boardHeight : 8;
            float centerX     = (w - 1) * 0.5f;
            float spacing     = w / 3f;
            float spawnY      = -h * 0.3f;

            var spawnPoints = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var point = new GameObject($"SpawnPoint_{i}");
                point.transform.SetParent(spawnerObj.transform);
                point.transform.position = new Vector3(centerX + (i - 1) * spacing, spawnY, 0);
                spawnPoints[i] = point.transform;
            }
            blockSpawner.spawnPoints = spawnPoints;

            var dragObj = new GameObject("BlockDragHandler");
            dragHandler = dragObj.AddComponent<BlockDragHandler>();
        }

        void SetupUI()
        {
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            var uiObj = CreateUIChild(canvasObj.transform, "UIManager");
            uiManager = uiObj.AddComponent<UIManager>();

            CreateHUD(canvasObj.transform);
            CreateGameOverPanel(canvasObj.transform);
            CreateStageTransitionPanel(canvasObj.transform);
            CreateSpaceshipPanel(canvasObj.transform);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        void CreateHUD(Transform canvasTransform)
        {
            var hudObj = CreateUIChild(canvasTransform, "HUD");
            var hudRect = hudObj.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 0);
            hudRect.anchorMax = new Vector2(1, 1);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            // Turn display - top left, large and bold
            uiManager.turnText = CreateText(hudObj.transform, "TurnText", "TURN: 20",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, -10), new Vector2(300, 60),
                TextAnchor.UpperLeft, 32, FontStyle.Bold, Color.white);

            // Score - below turn
            uiManager.scoreText = CreateText(hudObj.transform, "ScoreText", "SCORE: 0",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(10, -70), new Vector2(250, 40),
                TextAnchor.UpperLeft, 22, FontStyle.Normal, Color.white);

            // Stage - top right
            uiManager.stageText = CreateText(hudObj.transform, "StageText", "STAGE 1/5",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-10, -10), new Vector2(250, 40),
                TextAnchor.UpperRight, 22, FontStyle.Normal, Color.white);

            // Line progress - below stage, top right
            uiManager.lineProgressText = CreateText(hudObj.transform, "LineProgressText", "LINE: 0/10",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-10, -50), new Vector2(250, 40),
                TextAnchor.UpperRight, 24, FontStyle.Bold, Color.white);

            // Combo text - top center
            uiManager.comboText = CreateText(hudObj.transform, "ComboText", "",
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(0, -80), new Vector2(400, 50),
                TextAnchor.UpperCenter, 28, FontStyle.Bold, Color.yellow);
            uiManager.comboText.gameObject.SetActive(false);

            // Urgency overlay
            var overlayObj = CreateUIChild(canvasTransform, "UrgencyOverlay");
            var overlayRect = overlayObj.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            uiManager.urgencyOverlay = overlayObj.AddComponent<Image>();
            uiManager.urgencyOverlay.color = new Color(1, 0, 0, 0);
            uiManager.urgencyOverlay.raycastTarget = false;
        }

        void CreateGameOverPanel(Transform canvasTransform)
        {
            var panel = CreatePanel(canvasTransform, "GameOverPanel");
            uiManager.gameOverPanel = panel;
            panel.SetActive(false);

            uiManager.gameOverTitle = CreateText(panel.transform, "Title", "GAME OVER",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 100), new Vector2(500, 60),
                TextAnchor.MiddleCenter, 36, FontStyle.Bold, Color.white);

            uiManager.gameOverMessage = CreateText(panel.transform, "Message", "",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 0), new Vector2(500, 150),
                TextAnchor.MiddleCenter, 22, FontStyle.Normal, Color.white);

            var restartBtn = CreateButton(panel.transform, "RestartButton", "RETRY",
                new Vector2(0, -120), new Vector2(200, 50));
            uiManager.restartButton = restartBtn;
        }

        void CreateStageTransitionPanel(Transform canvasTransform)
        {
            var panel = CreatePanel(canvasTransform, "StageTransitionPanel");
            uiManager.stageTransitionPanel = panel;
            panel.SetActive(false);

            uiManager.stageClearText = CreateText(panel.transform, "StageClearText", "STAGE CLEAR!",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 150), new Vector2(500, 60),
                TextAnchor.MiddleCenter, 36, FontStyle.Bold, Color.white);

            CreateText(panel.transform, "SelectPerkText", "Select a perk:",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 80), new Vector2(500, 40),
                TextAnchor.MiddleCenter, 20, FontStyle.Normal, Color.white);

            uiManager.perkButtons = new Button[3];
            uiManager.perkTexts = new Text[3];

            for (int i = 0; i < 3; i++)
            {
                float yPos = 20 - i * 80;
                var btn = CreateButton(panel.transform, $"PerkButton_{i}", "Perk",
                    new Vector2(0, yPos), new Vector2(400, 65));
                uiManager.perkButtons[i] = btn;
                uiManager.perkTexts[i] = btn.GetComponentInChildren<Text>();
            }
        }

        void CreateSpaceshipPanel(Transform canvasTransform)
        {
            var panel = CreatePanel(canvasTransform, "SpaceshipPanel");
            uiManager.spaceshipPanel = panel;
            panel.SetActive(false);

            CreateText(panel.transform, "Title", "SPACESHIP COMPLETE!",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 150), new Vector2(500, 60),
                TextAnchor.MiddleCenter, 36, FontStyle.Bold, Color.white);

            uiManager.spaceshipInfoText = CreateText(panel.transform, "InfoText", "",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 20), new Vector2(500, 200),
                TextAnchor.MiddleCenter, 22, FontStyle.Normal, Color.white);

            var launchBtn = CreateButton(panel.transform, "LaunchButton", "LAUNCH!",
                new Vector2(0, -140), new Vector2(300, 60));
            uiManager.launchButton = launchBtn;
        }

        // --- UI Helper Methods ---

        static Font _cachedFont;
        static Font GetFont()
        {
            if (_cachedFont == null)
                _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 24);
            return _cachedFont;
        }

        GameObject CreateUIChild(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        GameObject CreatePanel(Transform parent, string name)
        {
            var panelObj = new GameObject(name, typeof(RectTransform));
            panelObj.transform.SetParent(parent, false);

            var rect = panelObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);

            return panelObj;
        }

        Text CreateText(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size,
            TextAnchor alignment, int fontSize, FontStyle fontStyle, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);

            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var t = obj.AddComponent<Text>();
            t.text = text;
            t.font = GetFont();
            t.fontSize = fontSize;
            t.fontStyle = fontStyle;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;

            return t;
        }

        Button CreateButton(Transform parent, string name, string label,
            Vector2 position, Vector2 size)
        {
            var btnObj = new GameObject(name, typeof(RectTransform));
            btnObj.transform.SetParent(parent, false);

            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.3f, 0.5f, 0.9f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            var textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var t = textObj.AddComponent<Text>();
            t.text = label;
            t.font = GetFont();
            t.fontSize = 20;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;

            return btn;
        }

        // --- Tilemap & General Helpers ---

        Tilemap CreateTilemapLayer(Transform parent, string name, int sortOrder)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var tilemap = obj.AddComponent<Tilemap>();
            var renderer = obj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortOrder;
            return tilemap;
        }

        TileBase CreateColorTile(Color color)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.color = color;
            tile.sprite = CreateSquareSprite();
            return tile;
        }

        static Sprite cachedSquareSprite;

        Sprite CreateSquareSprite()
        {
            if (cachedSquareSprite != null) return cachedSquareSprite;

            int size = 32;
            var texture = new Texture2D(size, size);
            texture.filterMode = FilterMode.Point;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x == 0 || x == size - 1 || y == 0 || y == size - 1;
                    pixels[y * size + x] = isBorder ? new Color(0, 0, 0, 0.3f) : Color.white;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            cachedSquareSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return cachedSquareSprite;
        }

        GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        void WireReferences()
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                gm = FindObjectOfType<GameManager>();
            }

            gm.boardManager = boardManager;
            gm.blockSpawner = blockSpawner;
            gm.turnManager = turnManager;
            gm.stageManager = stageManager;
            gm.lineClearSystem = lineClearSystem;
            gm.comboSystem = comboSystem;
            gm.itemSystem = itemSystem;
            gm.chaseSystem = chaseSystem;
            gm.roguelikeSystem = roguelikeSystem;
            gm.spaceshipBuilder = spaceshipBuilder;
            gm.uiManager = uiManager;

            boardManager.tilemapController = tilemapController;
            lineClearSystem.boardManager = boardManager;
            lineClearSystem.tilemapController = tilemapController;
            itemSystem.boardManager = boardManager;
            itemSystem.tilemapController = tilemapController;
            itemSystem.turnManager = turnManager;
            chaseSystem.turnManager = turnManager;
            dragHandler.boardManager = boardManager;
            dragHandler.blockSpawner = blockSpawner;
            dragHandler.mainCamera = Camera.main;
        }
    }
}
