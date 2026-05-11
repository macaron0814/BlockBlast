using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// 2D スプライト (Image / SpriteRenderer / 任意 Transform) を Z 軸まわりで回転させる汎用コンポーネント。
    ///
    /// ■ 主な用途
    ///   ・タイヤ / 歯車 / プロペラなど、決まった速度でくるくる回るオブジェクト
    ///   ・装飾的な背景パーツのループ回転
    ///   ・エフェクトオブジェクトを常時回し続けたいとき
    ///
    /// ■ 仕様
    ///   ・degreesPerSecond (度/秒) で回転速度を指定
    ///   ・clockwise を ON にすると時計回り (タイヤ等で自然な見た目)
    ///   ・speedMultiplier で外部から動的に倍率調整可能 (車速連動・一時停止 等)
    ///   ・useUnscaledTime を ON にすると Time.timeScale=0 でも回り続ける (ポーズ画面の演出など)
    ///
    /// ■ セットアップ
    ///   1. 回したい GameObject (子スプライト推奨) に AddComponent
    ///   2. degreesPerSecond をインスペクタで設定 (例: 360 = 1 秒で 1 周)
    ///   3. clockwise / speedMultiplier を必要に応じて調整
    ///
    /// ■ 推奨ヒエラルキー (タイヤ用例)
    ///   Vehicle (車体)
    ///     └ Wheel (この GameObject に Spinner2D)
    ///         └ WheelSprite (SpriteRenderer / Image)
    ///   Wheel ごと回転するので、ホイールのデザインがタイヤらしく回って見える
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BlockBlast/Spinner 2D (Z-axis Rotation)")]
    public class Spinner2D : MonoBehaviour
    {
        [Header("回転速度")]
        [Tooltip("Z 軸まわりの回転速度 (度/秒)。\n" +
                 "例) 360 = 1秒で1周 / 720 = 1秒で2周 / 90 = 4秒で1周\n" +
                 "負の値で逆回転 (clockwise を使うほうが推奨)")]
        public float degreesPerSecond = 360f;

        [Tooltip("ON: 時計回り (タイヤ等で自然) / OFF: 反時計回り (Unity 標準)")]
        public bool clockwise = true;

        [Header("動的制御")]
        [Tooltip("回転速度全体の倍率。外部スクリプトから書き換え可能。\n" +
                 "・1 = 通常速度 / 0 = 停止 / -1 = 逆回転\n" +
                 "・例: 車速に合わせて 0.0〜1.5 の範囲で書き換えると車速連動になる")]
        public float speedMultiplier = 1f;

        [Header("時間軸")]
        [Tooltip("ON: Time.timeScale を無視して回し続ける (ポーズ中も回転)\n" +
                 "OFF: ゲームの TimeScale に従う (ポーズ中は止まる)")]
        public bool useUnscaledTime = false;

        [Header("回転空間")]
        [Tooltip("ON: ローカル軸で回す (子オブジェクトの相対回転、推奨)\n" +
                 "OFF: ワールド軸で回す")]
        public bool useLocalSpace = true;

        [Header("初期位相 (任意)")]
        [Tooltip("Start 時にランダムな初期角度を与える (タイヤ複数並べる時、揃いすぎない見た目に)。\n" +
                 "0 で無効化")]
        [Range(0f, 360f)]
        public float randomStartAngleRange = 0f;

        // ────────────────────────────────────────
        //  Public API
        // ────────────────────────────────────────

        /// <summary>速度 (度/秒) を直接指定。</summary>
        public void SetSpeed(float degPerSec) => degreesPerSecond = degPerSec;

        /// <summary>回転を停止 (倍率を 0 に)。回転値は維持される。</summary>
        public void Stop() => speedMultiplier = 0f;

        /// <summary>回転を再開 (倍率を 1 に戻す)。</summary>
        public void Resume() => speedMultiplier = 1f;

        /// <summary>回転方向を反転。</summary>
        public void ToggleDirection() => clockwise = !clockwise;

        /// <summary>現在の Z 角度 (度)。</summary>
        public float CurrentAngle => transform.localEulerAngles.z;

        // ────────────────────────────────────────
        //  Lifecycle
        // ────────────────────────────────────────

        void Start()
        {
            if (randomStartAngleRange > 0f)
            {
                float jitter = Random.Range(-randomStartAngleRange, randomStartAngleRange);
                Vector3 e = transform.localEulerAngles;
                e.z += jitter;
                transform.localEulerAngles = e;
            }
        }

        void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            float sign = clockwise ? -1f : 1f;
            float deg  = degreesPerSecond * speedMultiplier * sign * dt;

            if (Mathf.Approximately(deg, 0f)) return;

            transform.Rotate(0f, 0f, deg, useLocalSpace ? Space.Self : Space.World);
        }
    }
}
