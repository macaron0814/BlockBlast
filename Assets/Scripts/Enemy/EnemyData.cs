using UnityEngine;

namespace BlockBlastGame
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "BlockBlast/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("基本ステータス")]
        [Tooltip("最大HP（0になると気絶）")]
        public int maxHP = 3;

        [Tooltip("追跡速度 (度/秒)")]
        public float chaseSpeed = 5f;

        [Header("ノックバック")]
        [Tooltip("弾1発あたりの基本ノックバック角度 (度)。バースト時は hits² でスケール")]
        public float knockbackPerHit = 3f;

        [Header("気絶")]
        [Tooltip("HP 0 時の停止時間 (秒)。停止中は地面スクロールで後退する")]
        public float stunDuration = 3f;

        [Header("出現")]
        [Tooltip("プレイヤーからの初期距離 (度)")]
        public float spawnDistance = 60f;

        [Header("ビジュアル")]
        public Sprite[] frames;
        public Sprite[] stunFrames;

        [Tooltip("秒/コマ")]
        public float frameRate = 0.15f;

        public float scale = 0.8f;

        [Tooltip("地面からの浮き高さ (ワールド単位)。0 = 地面に接地")]
        public float hoverHeight = 0f;

        public Color tint = Color.white;

        [Header("当たり判定")]
        [Tooltip("敵の中心からの当たり判定オフセット (ローカル座標)")]
        public Vector2 hitOffset = Vector2.zero;

        [Tooltip("当たり判定の角度範囲 (度)。大きいほど当たりやすい")]
        public float hitAngleRadius = 4f;

        [Header("被弾エフェクト")]
        [Tooltip("被弾時のコマ送りスプライト配列")]
        public Sprite[] hitEffectFrames;

        [Tooltip("被弾エフェクトの秒/コマ")]
        public float hitEffectFrameRate = 0.05f;

        [Tooltip("被弾エフェクトのスケール")]
        public float hitEffectScale = 1f;

        [Tooltip("被弾エフェクトの色")]
        public Color hitEffectColor = Color.white;

        [Header("撃破ボーナス")]
        [Tooltip("HP が 0 になった瞬間に追加で表示するスパチャ金額 (円)")]
        public int defeatBonusAmount = 500;

        public static EnemyData CreateDefault(int index = 0)
        {
            var d = CreateInstance<EnemyData>();
            d.name = $"Enemy_{index}";
            d.maxHP = 3 + index;
            d.chaseSpeed = 4f + index * 0.5f;
            d.knockbackPerHit = 3f;
            d.stunDuration = 3f;
            d.spawnDistance = 50f + index * 20f;
            d.scale = 0.7f;
            d.tint = index switch
            {
                0 => new Color(0.9f, 0.25f, 0.25f),
                1 => new Color(0.3f, 0.3f, 0.9f),
                2 => new Color(0.2f, 0.8f, 0.35f),
                _ => new Color(0.85f, 0.5f, 0.9f),
            };
            return d;
        }
    }
}
