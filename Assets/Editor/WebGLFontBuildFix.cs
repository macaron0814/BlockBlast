using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class WebGLFontBuildFix : IPreprocessBuildWithReport
{
    const string FontAssetPath = "Assets/Fonts/Resources/GameFont.ttf";

    public int callbackOrder => 0;

    [MenuItem("Tools/BlockBlast/Fix Scene Fonts")]
    static void FixSceneFontsMenu()
    {
        FixAllEnabledScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WebGLFontBuildFix] Enabled scenes font references updated.");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        FixAllEnabledScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void FixAllEnabledScenes()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath);
        if (font == null)
        {
            throw new BuildFailedException($"Font asset not found: {FontAssetPath}");
        }

        string originalScenePath = SceneManager.GetActiveScene().path;

        foreach (var sceneSetting in EditorBuildSettings.scenes)
        {
            if (!sceneSetting.enabled) continue;

            var scene = EditorSceneManager.OpenScene(sceneSetting.path, OpenSceneMode.Single);
            bool changed = false;

            var texts = Object.FindObjectsOfType<Text>(true);
            foreach (var text in texts)
            {
                if (text == null || text.font == font) continue;
                Undo.RecordObject(text, "Fix WebGL Font");
                text.font = font;
                EditorUtility.SetDirty(text);
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[WebGLFontBuildFix] Updated fonts in scene: {scene.path}");
            }
        }

        if (!string.IsNullOrEmpty(originalScenePath))
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }
    }
}
