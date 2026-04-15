#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// EnemySystem のプレイヤー側当たり判定・弾スポーン地点をシーンビューにギズモ表示。
    /// • 黄星     = bulletSpawnPoint の位置（ドラッグで移動可）
    /// • 緑の円   = 弾のヒット判定範囲（bulletHitAngleOffset + bulletHitAngleRadius）
    /// • 青矢印   = ヒットオフセットの方向
    /// • 赤線     = ゲームオーバーライン（敵がここに到達するとゲームオーバー）
    /// </summary>
    [CustomEditor(typeof(EnemySystem))]
    public class EnemySystemEditor : Editor
    {
        static readonly Color SpawnColor    = new Color(1f,   0.95f, 0.2f,  0.9f);
        static readonly Color HitColor      = new Color(0.2f, 1f,   0.35f, 0.85f);
        static readonly Color HitFill       = new Color(0.2f, 1f,   0.35f, 0.10f);
        static readonly Color OffsetColor   = new Color(0.3f, 0.8f, 1f,   0.9f);
        static readonly Color GameOverColor = new Color(1f,   0.15f, 0.15f, 0.9f);
        static readonly Color GameOverFill  = new Color(1f,   0.1f,  0.1f,  0.12f);

        void OnSceneGUI()
        {
            var sys = (EnemySystem)target;
            if (sys.archRoadSystem == null) return;

            float archR  = sys.archRoadSystem.archRadius;
            Vector3 arcC = sys.archRoadSystem.transform.position;

            // ── スポーン地点 ──
            if (sys.bulletSpawnPoint != null)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(
                    sys.bulletSpawnPoint.position,
                    sys.bulletSpawnPoint.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sys.bulletSpawnPoint, "Move Bullet Spawn Point");
                    sys.bulletSpawnPoint.position = newPos;
                }

                // 星マーク
                Handles.color = SpawnColor;
                Handles.DrawSolidDisc(sys.bulletSpawnPoint.position, Vector3.forward, 0.08f);
                Handles.Label(sys.bulletSpawnPoint.position + Vector3.up * 0.15f,
                    "Bullet Spawn", EditorStyles.miniLabel);
            }

            // ── 弾ヒット判定ゾーン（スポーン地点 = 角度0 の位置を基準に表示）──
            // スポーン角度を計算（bulletSpawnPoint が無ければ 0°）
            float startAngle = 0f;
            if (sys.bulletSpawnPoint != null)
            {
                Vector3 local = sys.bulletSpawnPoint.position - arcC;
                startAngle = -Mathf.Atan2(local.x, local.y + archR) * Mathf.Rad2Deg;
                startAngle = Mathf.Max(startAngle, 0f);
            }

            float hitAngle = startAngle + sys.bulletHitAngleOffset;

            // ヒット中心をワールド座標に変換
            float visualAngle = -hitAngle;
            float rad = visualAngle * Mathf.Deg2Rad;
            float effectiveR = archR;
            Vector3 hitCenter = arcC + new Vector3(
                Mathf.Sin(rad) * effectiveR,
                -archR + Mathf.Cos(rad) * effectiveR, 0f);

            // ヒット半径（角度 → ワールド半径）
            float hitWorldRadius = archR * sys.bulletHitAngleRadius * Mathf.Deg2Rad;

            // オフセット矢印（スポーン → ヒット中心）
            if (sys.bulletSpawnPoint != null && Mathf.Abs(sys.bulletHitAngleOffset) > 0.01f)
            {
                Handles.color = OffsetColor;
                Handles.DrawDottedLine(sys.bulletSpawnPoint.position, hitCenter, 3f);
                Handles.DrawSolidDisc(hitCenter, Vector3.forward, 0.05f);
            }

            // ヒット円
            Handles.color = HitFill;
            Handles.DrawSolidDisc(hitCenter, Vector3.forward, hitWorldRadius);
            Handles.color = HitColor;
            Handles.DrawWireDisc(hitCenter, Vector3.forward, hitWorldRadius, 2f);

            // hitAngleRadius スライダードット
            EditorGUI.BeginChangeCheck();
            Vector3 radiusHandle = hitCenter + new Vector3(hitWorldRadius, 0f, 0f);
            Vector3 newR = Handles.FreeMoveHandle(
                radiusHandle, 0.06f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sys, "Edit Bullet Hit Radius");
                float newWorld = Vector3.Distance(hitCenter, newR);
                sys.bulletHitAngleRadius = Mathf.Max(0.1f, (newWorld / Mathf.Max(archR, 0.01f)) * Mathf.Rad2Deg);
            }

            // hitAngleOffset スライダードット（アーチ方向に動かす）
            EditorGUI.BeginChangeCheck();
            Vector3 offsetHandle = hitCenter;
            Vector3 newOffset = Handles.FreeMoveHandle(
                offsetHandle, 0.07f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sys, "Edit Bullet Hit Offset");
                Vector3 delta   = newOffset - hitCenter;
                float tangentX  = -Mathf.Cos(rad);
                float tangentY  = -Mathf.Sin(rad);
                var tangent     = new Vector3(tangentX, tangentY, 0f);
                float worldMove = Vector3.Dot(delta, tangent);
                float degMove   = (worldMove / Mathf.Max(archR, 0.01f)) * Mathf.Rad2Deg;
                sys.bulletHitAngleOffset -= degMove;
            }

            // ラベル
            Handles.Label(hitCenter + Vector3.up * (hitWorldRadius + 0.12f),
                $"BulletHit  offset={sys.bulletHitAngleOffset:F1}°  R={sys.bulletHitAngleRadius:F1}°",
                EditorStyles.miniLabel);

            // ── ゲームオーバーライン ──
            DrawGameOverLine(sys, archR, arcC);
        }

        void DrawGameOverLine(EnemySystem sys, float archR, Vector3 arcC)
        {
            float goAngle = sys.gameOverAngle;
            float goVisual = -goAngle;
            float goRad = goVisual * Mathf.Deg2Rad;
            Vector3 circleCenter = arcC + new Vector3(0f, -archR, 0f);
            Vector3 radial = new Vector3(Mathf.Sin(goRad), Mathf.Cos(goRad), 0f);

            Vector3 goCenter = arcC + new Vector3(
                Mathf.Sin(goRad) * archR,
                -archR + Mathf.Cos(goRad) * archR, 0f);

            float innerOffset = 1.25f;
            float outerOffset = 2.5f;
            Vector3 lineStart = circleCenter + radial * Mathf.Max(0f, archR - innerOffset);
            Vector3 lineEnd = circleCenter + radial * (archR + outerOffset);

            Handles.color = GameOverColor;
            Handles.DrawLine(lineStart, lineEnd, 3f);

            Handles.color = GameOverFill;
            Handles.DrawSolidDisc(goCenter, Vector3.forward, 0.1f);

            // ドラッグハンドル（アーチ上を移動して角度を調整）
            EditorGUI.BeginChangeCheck();
            Vector3 newGo = Handles.FreeMoveHandle(
                goCenter, 0.12f, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(sys, "Edit Game Over Angle");
                Vector3 local = newGo - arcC;
                float newAngle = -Mathf.Atan2(local.x, local.y + archR) * Mathf.Rad2Deg;
                sys.gameOverAngle = Mathf.Max(0f, newAngle);
            }

            Handles.color = GameOverColor;
            Handles.Label(goCenter + Vector3.up * 0.25f,
                $"GAME OVER LINE  {goAngle:F1}°",
                EditorStyles.boldLabel);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "シーンビューで EnemySystem を選択するとギズモが表示されます。\n" +
                "• 黄点    = Bullet Spawn Point\n" +
                "• 緑円    = 弾ヒット判定範囲（ドット or ドラッグで調整）\n" +
                "• 青点線  = Hit Angle Offset の方向\n" +
                "• 赤線    = ゲームオーバーライン（ドラッグで位置調整）",
                MessageType.Info);
        }
    }
}
#endif
