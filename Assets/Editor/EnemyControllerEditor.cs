#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// EnemyController の当たり判定をシーンビューにギズモ表示するカスタムエディタ。
    /// • 赤い円   = 当たり判定範囲（hitAngleRadius → ワールド半径に変換）
    /// • 黄色矢印 = hitOffset の位置（スプライト中心 → 判定中心）
    /// • 青い線   = hoverHeight（地面との距離）
    /// </summary>
    [CustomEditor(typeof(EnemyController))]
    public class EnemyControllerEditor : Editor
    {
        static readonly Color HitColor    = new Color(1f,  0.2f, 0.2f, 0.85f);
        static readonly Color OffsetColor = new Color(1f,  0.9f, 0.1f, 0.9f);
        static readonly Color HoverColor  = new Color(0.3f, 0.8f, 1f,  0.7f);
        static readonly Color FillColor   = new Color(1f,  0.2f, 0.2f, 0.12f);

        void OnSceneGUI()
        {
            var ctrl = (EnemyController)target;

            // _data は SerializeField なので serializedObject 経由で取得
            var dataProp = serializedObject.FindProperty("_data");
            EnemyData data = dataProp?.objectReferenceValue as EnemyData
                          ?? ctrl.editorPreviewData;
            if (data == null) return;

            Vector3 pos   = ctrl.transform.position;
            float distAngle = ctrl.distanceAngle;

            // 現在の visualAngle (Play 時は distanceAngle が有効, Edit 時は 0)
            float visualAngle = -distAngle;
            float rad = visualAngle * Mathf.Deg2Rad;
            var tangent = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            var normal  = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);

            // ── hitOffset ──
            Vector3 hitCenter = pos
                + tangent * data.hitOffset.x
                + normal  * data.hitOffset.y;

            // hitOffset ハンドル（ドラッグして直接調整）
            EditorGUI.BeginChangeCheck();
            Vector3 newHitCenter = Handles.PositionHandle(hitCenter, ctrl.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Hit Offset");
                Vector3 delta = newHitCenter - pos;
                data.hitOffset = new Vector2(
                    Vector3.Dot(delta, tangent),
                    Vector3.Dot(delta, normal));
                EditorUtility.SetDirty(data);
            }

            // hitOffset の線
            Handles.color = OffsetColor;
            Handles.DrawLine(pos, hitCenter, 2f);
            Handles.DrawSolidDisc(hitCenter, Vector3.forward, 0.05f);

            // ── 当たり判定円 ──
            // hitAngleRadius(度) → ワールド半径: arc = r * θ(rad)
            float archR = GetArchRadius(ctrl);
            float hitWorldRadius = archR * data.hitAngleRadius * Mathf.Deg2Rad;

            Handles.color = FillColor;
            Handles.DrawSolidDisc(hitCenter, Vector3.forward, hitWorldRadius);
            Handles.color = HitColor;
            Handles.DrawWireDisc(hitCenter, Vector3.forward, hitWorldRadius, 2f);

            // hitAngleRadius スライダー（円周上のドット）
            EditorGUI.BeginChangeCheck();
            Vector3 radiusHandle = hitCenter + new Vector3(hitWorldRadius, 0f, 0f);
            Vector3 newRadiusHandle = Handles.FreeMoveHandle(
                radiusHandle, 0.06f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Edit Hit Angle Radius");
                float newWorldRadius = Vector3.Distance(hitCenter, newRadiusHandle);
                data.hitAngleRadius = (newWorldRadius / Mathf.Max(archR, 0.01f)) * Mathf.Rad2Deg;
                data.hitAngleRadius = Mathf.Max(data.hitAngleRadius, 0.1f);
                EditorUtility.SetDirty(data);
            }

            // ラベル
            Handles.Label(hitCenter + Vector3.up * (hitWorldRadius + 0.1f),
                $"R={data.hitAngleRadius:F1}°\nOffset({data.hitOffset.x:F2},{data.hitOffset.y:F2})",
                EditorStyles.miniLabel);

            // ── hoverHeight ──
            if (Mathf.Abs(data.hoverHeight) > 0.001f)
            {
                float groundR = archR;
                Vector3 groundPos = ctrl.transform.parent != null
                    ? ctrl.transform.parent.position
                      + new Vector3(Mathf.Sin(rad) * groundR,
                                    -groundR + Mathf.Cos(rad) * groundR, 0f)
                    : pos - normal * data.hoverHeight;

                Handles.color = HoverColor;
                Handles.DrawDottedLine(groundPos, pos, 3f);
                Handles.DrawSolidDisc(groundPos, Vector3.forward, 0.04f);
                Handles.Label(groundPos + Vector3.right * 0.1f,
                    $"hover={data.hoverHeight:F2}", EditorStyles.miniLabel);
            }
        }

        // ────────────────────────────────────────────────────
        //  Inspector
        // ────────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "シーンビューで敵を選択するとギズモが表示されます。\n" +
                "• 赤円  = 当たり判定範囲\n" +
                "• 黄矢印 = hitOffset（ドラッグ可）\n" +
                "• 青点線 = hoverHeight",
                MessageType.Info);
        }

        static float GetArchRadius(EnemyController ctrl)
        {
            // SerializedProperty 経由で _archRadius を取得
            var so = new SerializedObject(ctrl);
            var prop = so.FindProperty("_archRadius");
            float r = prop != null ? prop.floatValue : 0f;
            return r > 0f ? r : 50f;
        }
    }
}
#endif
