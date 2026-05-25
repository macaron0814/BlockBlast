using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// 全体一時停止を司る汎用シングルトン (static)。
    ///
    /// ■ コンセプト
    ///   ・Time.timeScale を 0 に落として「Time.deltaTime / WaitForSeconds に依存するもの全部」を一括停止する。
    ///       - EnemySystem.UpdateSurvivalTimer (Time.deltaTime)
    ///       - ArchRoadSystem の回転 (Time.deltaTime)
    ///       - EnemyController の移動・チェイス
    ///       - PlayerBullet の角度更新
    ///       - SpriteFrameAnimator2D / Spinner2D / CharacterAnimator など
    ///       - RunWaves / SlideEnemiesAway 等の WaitForSeconds 系コルーチン
    ///   ・「複数の発信元が独立して Pause / Resume を呼ぶ」ためにハンドル制を採用。
    ///     同じハンドルからの二重 Pause は無視し、最後のハンドルが Resume されたら復帰する。
    ///
    /// ■ ハンドル例
    ///   - "VendingMachine"  (自販機到着 → Canvas 表示中)
    ///   - "PauseMenu"       (将来のメニュー / 設定画面)
    ///   - "Tutorial"        (チュートリアルポップアップ等)
    ///
    /// ■ ポーズ中も動かしたいもの (UI フェード等) は WaitForSecondsRealtime / Time.unscaledDeltaTime を使うこと。
    /// </summary>
    public static class GamePauseService
    {
        static readonly HashSet<string> _activeHandles = new HashSet<string>();
        static float _savedTimeScale = 1f;

        /// <summary>1 つ以上のハンドルが Pause を主張しているか。</summary>
        public static bool IsPaused => _activeHandles.Count > 0;

        /// <summary>現在 Pause 中のハンドル数 (デバッグ用)。</summary>
        public static int ActiveHandleCount => _activeHandles.Count;

        /// <summary>true=Paused, false=Resumed (= 全 Pause が解除された)。</summary>
        public static event System.Action<bool> OnPauseChanged;

        /// <summary>
        /// 指定ハンドルで Pause をかける。
        /// 同じハンドルから複数回呼んでも 1 回しかカウントされない (= idempotent)。
        /// 別ハンドル (例: 自販機中にチュートリアル等) は重ねて Pause できる。
        /// </summary>
        public static void Pause(string handle)
        {
            if (string.IsNullOrEmpty(handle)) handle = "default";

            bool wasPaused = IsPaused;
            if (!_activeHandles.Add(handle))
                return; // 同ハンドルは既に Pause 状態

            if (!wasPaused)
            {
                // 念のため timeScale が変な値だった場合の保護
                _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                Debug.Log($"[GamePauseService] Pause('{handle}') → timeScale 0 (saved={_savedTimeScale:F2})");
                OnPauseChanged?.Invoke(true);
            }
            else
            {
                Debug.Log($"[GamePauseService] Pause('{handle}') 追加 (active={_activeHandles.Count})");
            }
        }

        /// <summary>
        /// 指定ハンドルの Pause を解除する。
        /// 全ハンドルが解除されたら Time.timeScale を元の値に戻す。
        /// </summary>
        public static void Resume(string handle)
        {
            if (string.IsNullOrEmpty(handle)) handle = "default";

            if (!_activeHandles.Remove(handle))
                return; // そもそも Pause していなかった

            if (_activeHandles.Count == 0)
            {
                Time.timeScale = _savedTimeScale;
                Debug.Log($"[GamePauseService] Resume('{handle}') → timeScale {_savedTimeScale:F2}");
                OnPauseChanged?.Invoke(false);
            }
            else
            {
                Debug.Log($"[GamePauseService] Resume('{handle}') (まだ active={_activeHandles.Count} 残り)");
            }
        }

        /// <summary>シーン再ロード等で全 Pause を強制クリアする。</summary>
        public static void ResetAll()
        {
            bool wasPaused = IsPaused;
            _activeHandles.Clear();
            Time.timeScale = 1f;
            _savedTimeScale = 1f;
            if (wasPaused) OnPauseChanged?.Invoke(false);
        }
    }
}
