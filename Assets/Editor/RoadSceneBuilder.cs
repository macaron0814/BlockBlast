#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using BlockBlastGame;

/// <summary>
/// メニューから道路＋背景のアーチループシーンを一括構築する Editor ツール。
/// Assets/Image/ のスプライトを自動で割り当てる。
/// </summary>
public static class RoadSceneBuilder
{
    [MenuItem("Tools/BlockBlast/Build Road Scene")]
    static void Build()
    {
        if (Object.FindObjectOfType<ArchRoadSystem>() != null)
        {
            bool rebuild = EditorUtility.DisplayDialog(
                "Road Scene Builder",
                "既にシーンに ArchRoadSystem があります。再構築しますか？",
                "再構築", "キャンセル");
            if (!rebuild) return;
            Cleanup();
        }

        // ---- Load sprites from Assets/Image/ ----
        var roadSprite      = LoadSprite("Assets/Image/road.png");
        var whiteLineSprite = LoadSprite("Assets/Image/白線.png");
        var skySprite       = LoadSprite("Assets/Image/空.png");
        var farBgSprite     = LoadSprite("Assets/Image/遠景.png");
        var midBgSprite     = LoadSprite("Assets/Image/中景.png");
        var cloudSprite     = LoadSprite("Assets/Image/雲.png");

        // 縁石は複数パターン対応: 縁石.png, 縁石2.png, 縁石3.png ... があれば読み込む
        var curbEntryList = new System.Collections.Generic.List<CurbEntry>();
        var baseCurb = LoadSprite("Assets/Image/縁石.png");
        if (baseCurb != null)
            curbEntryList.Add(new CurbEntry { sprite = baseCurb, offset = new Vector2(0f, 0.05f), scale = Vector2.one });
        for (int ci = 2; ci <= 10; ci++)
        {
            var extra = LoadSpriteSilent($"Assets/Image/縁石{ci}.png");
            if (extra != null)
                curbEntryList.Add(new CurbEntry { sprite = extra, offset = new Vector2(0f, 0.05f), scale = Vector2.one });
        }

        // ---- Road (360° 一周) ----
        var roadObj = new GameObject("ArchRoadSystem");
        var road    = roadObj.AddComponent<ArchRoadSystem>();
        road.roadSprite      = roadSprite;
        road.whiteLineSprite = whiteLineSprite;
        road.whiteLineInterval = 4;
        road.curbEntries     = curbEntryList.ToArray();
        road.curbInterval    = 3;
        road.archRadius      = 50f;
        road.tileWorldWidth  = 1.56f;
        road.scrollSpeed     = 15f;
        road.sortingLayer    = "UI";
        road.roadSortingOrder       = 10;
        road.whiteLineSortingOrder  = 11;
        road.curbSortingOrder       = 12;

        // ---- Parallax Background ----
        var bgObj     = new GameObject("ParallaxBackground");
        var parallax  = bgObj.AddComponent<ParallaxBackground>();
        parallax.baseArchRadius  = 50f;
        parallax.baseScrollSpeed = 15f;

        parallax.layers = new ParallaxBackground.BackgroundLayer[]
        {
            new ParallaxBackground.BackgroundLayer
            {
                name             = "Sky",
                sprite           = skySprite,
                parallaxFactor   = 0f,
                offsetY          = 5f,
                archRadiusOffset = 30f,
                sortingLayer     = "UI",
                sortingOrder     = -50,
            },
            new ParallaxBackground.BackgroundLayer
            {
                name             = "FarBG",
                sprite           = farBgSprite,
                parallaxFactor   = 0.15f,
                offsetY          = 3f,
                archRadiusOffset = 20f,
                sortingLayer     = "UI",
                sortingOrder     = -40,
            },
            new ParallaxBackground.BackgroundLayer
            {
                name             = "Cloud",
                sprite           = cloudSprite,
                parallaxFactor   = 0.25f,
                offsetY          = 6f,
                archRadiusOffset = 15f,
                sortingLayer     = "UI",
                sortingOrder     = -35,
            },
            new ParallaxBackground.BackgroundLayer
            {
                name             = "MidBG",
                sprite           = midBgSprite,
                parallaxFactor   = 0.5f,
                offsetY          = 1.5f,
                archRadiusOffset = 5f,
                sortingLayer     = "UI",
                sortingOrder     = -20,
            },
        };

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[RoadScene] 道路 + 背景アーチループを構築しました。Play で確認してください。");
    }

    static void Cleanup()
    {
        string[] targets = { "ArchRoadSystem", "ParallaxBackground" };
        foreach (var name in targets)
        {
            var obj = GameObject.Find(name);
            if (obj != null) Object.DestroyImmediate(obj);
        }
    }

    static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[RoadScene] スプライトが見つかりません: {path}");
        return sprite;
    }

    static Sprite LoadSpriteSilent(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
