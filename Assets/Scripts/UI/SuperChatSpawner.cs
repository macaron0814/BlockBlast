using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockBlastGame
{
    /// <summary>
    /// ステージ合計金額と目標表示回数から、6 種類の金種
    /// (100/500/1000/3000/5000/10000) の組み合わせを自動で分配し、
    /// 生存時間中に均等にスパチャを表示する。
    /// ノックバック発生時にはボーナススパチャを追加スポーン。
    /// </summary>
    public class SuperChatSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class DenomColor
        {
            [Tooltip("金額 (円)")]
            public int amount;

            [Tooltip("この金額で表示するときのカード色")]
            public Color color = Color.white;
        }

        [System.Serializable]
        public class StageChatConfig
        {
            [Tooltip("このステージで獲得させたい合計金額 (円)")]
            public int totalAmount = 1000;

            [Tooltip("このステージで何回スパチャを表示したいか")]
            public int targetCount = 10;
        }

        [Header("Prefab & Parent")]
        [Tooltip("SuperChatInstance を持つプレハブ")]
        public GameObject superChatPrefab;

        [Tooltip("生成先 (Canvas 下の RectTransform 推奨)")]
        public RectTransform spawnParent;

        [Tooltip("親内で生成時に配置するローカル位置。0,0 ならプレハブ設定を利用")]
        public Vector2 spawnAnchoredPosition = Vector2.zero;

        [Tooltip("スポーン位置を spawnAnchoredPosition で上書きするか")]
        public bool overrideSpawnPosition = false;

        [Tooltip("複数チャットが同時表示されても重ならないよう、順番に下方向にずらす間隔 (px)")]
        public float stackYOffset = 70f;

        [Tooltip("同時表示できる最大数（これ以上は古いものから破棄）")]
        public int maxConcurrent = 5;

        [Header("Stage Config (index = stageNumber - 1)")]
        [Tooltip("ステージごとのスパチャ設定。配列外のステージは defaultStageConfig を使用")]
        public StageChatConfig[] stageConfigs;

        [Tooltip("stageConfigs 範囲外のステージで使うデフォルト設定")]
        public StageChatConfig defaultStageConfig = new StageChatConfig { totalAmount = 1000, targetCount = 10 };

        [Tooltip("最初のスパチャを表示するまでの遅延秒数")]
        public float startDelay = 2f;

        [Tooltip("最後のスパチャを生存時間終了の何秒前までに撃ち切るか (余白)")]
        public float endSafetyMargin = 1f;

        [Header("Denominations")]
        [Tooltip("6 種類の金額と色。金額の昇順で並べる")]
        public DenomColor[] denominations = new[]
        {
            new DenomColor { amount =   100, color = new Color(0.30f, 0.70f, 1.00f) }, // 水色
            new DenomColor { amount =   500, color = new Color(0.40f, 0.85f, 0.40f) }, // 緑
            new DenomColor { amount =  1000, color = new Color(1.00f, 0.95f, 0.30f) }, // 黄
            new DenomColor { amount =  3000, color = new Color(1.00f, 0.60f, 0.20f) }, // オレンジ
            new DenomColor { amount =  5000, color = new Color(1.00f, 0.40f, 0.70f) }, // ピンク
            new DenomColor { amount = 10000, color = new Color(1.00f, 0.25f, 0.25f) }, // 赤
        };

        [Header("Defeat Bonus")]
        [Tooltip("敵撃破のスパチャ最短間隔秒 (スパム防止)")]
        public float defeatMinInterval = 0.05f;

        [Header("Runtime (read-only)")]
        [SerializeField] int _stageEarnings;
        public int StageEarnings => _stageEarnings;

        readonly List<float> _spawnSchedule = new List<float>();
        readonly List<int>   _spawnAmounts  = new List<int>();
        int _scheduleCursor;
        bool _scheduleActive;
        float _lastDefeatTime;
        readonly List<SuperChatInstance> _activeChats = new List<SuperChatInstance>();
        int _stackIndex;

        void OnEnable()
        {
            GameEvents.OnStageChanged += HandleStageChanged;
            GameEvents.OnSurvivalTimerUpdate += HandleSurvivalTimerUpdate;
            GameEvents.OnEnemyDefeated += HandleEnemyDefeated;
        }

        void OnDisable()
        {
            GameEvents.OnStageChanged -= HandleStageChanged;
            GameEvents.OnSurvivalTimerUpdate -= HandleSurvivalTimerUpdate;
            GameEvents.OnEnemyDefeated -= HandleEnemyDefeated;
        }

        void HandleStageChanged(int stageNumber)
        {
            // EnemySystem 側が SurvivalTimeLimit を設定するのを待ってから
            // schedule を組む（購読順依存を回避）
            StartCoroutine(DelayedBuildSchedule(stageNumber));
        }

        IEnumerator DelayedBuildSchedule(int stageNumber)
        {
            yield return null;
            BuildSchedule(stageNumber);
        }

        StageChatConfig ResolveStageConfig(int stageNumber)
        {
            int idx = stageNumber - 1;
            if (stageConfigs != null
                && idx >= 0
                && idx < stageConfigs.Length
                && stageConfigs[idx] != null)
            {
                return stageConfigs[idx];
            }
            return defaultStageConfig;
        }

        /// <summary>
        /// survivalTimer の Update で現在時刻を受け取り、予定スパチャを発火。
        /// </summary>
        void HandleSurvivalTimerUpdate(float elapsed, float limit)
        {
            if (!_scheduleActive) return;

            while (_scheduleCursor < _spawnSchedule.Count
                   && elapsed >= _spawnSchedule[_scheduleCursor])
            {
                SpawnChat(_spawnAmounts[_scheduleCursor]);
                _scheduleCursor++;
            }
        }

        void HandleEnemyDefeated(Vector3 worldPos, int bonusAmount)
        {
            if (bonusAmount <= 0) return;
            if (Time.time - _lastDefeatTime < defeatMinInterval) return;
            _lastDefeatTime = Time.time;

            SpawnChat(bonusAmount);
        }

        // ────────────────────────────────────────
        //  Schedule Build
        // ────────────────────────────────────────

        public void BuildSchedule(int stageNumber)
        {
            _spawnSchedule.Clear();
            _spawnAmounts.Clear();
            _scheduleCursor = 0;
            _stageEarnings = 0;
            _scheduleActive = false;
            _stackIndex = 0;

            StageChatConfig cfg = ResolveStageConfig(stageNumber);
            if (cfg == null || cfg.totalAmount <= 0 || cfg.targetCount <= 0) return;

            List<int> amounts = BuildAmountDistribution(cfg.totalAmount, cfg.targetCount);
            if (amounts == null || amounts.Count == 0) return;

            // シャッフル（同じ額が固まらないように）
            for (int i = amounts.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                (amounts[i], amounts[r]) = (amounts[r], amounts[i]);
            }

            float limit = ResolveSurvivalLimit();
            float effectiveEnd = Mathf.Max(startDelay + 0.1f, limit - endSafetyMargin);
            int n = amounts.Count;
            // n 個を startDelay 〜 effectiveEnd に均等配置（両端含む）
            for (int i = 0; i < n; i++)
            {
                float t = n == 1
                    ? startDelay
                    : Mathf.Lerp(startDelay, effectiveEnd, (float)i / (n - 1));
                _spawnSchedule.Add(t);
                _spawnAmounts.Add(amounts[i]);
            }

            _scheduleActive = true;

            Debug.Log($"[SuperChatSpawner] Stage {stageNumber}: {n} 回 / 合計{cfg.totalAmount}円 / {limit}s 内に分配");
        }

        float ResolveSurvivalLimit()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.enemySystem != null && gm.enemySystem.SurvivalTimeLimit > 0f)
                return gm.enemySystem.SurvivalTimeLimit;
            return 30f;
        }

        // ────────────────────────────────────────
        //  金額分割アルゴリズム
        // ────────────────────────────────────────

        /// <summary>
        /// totalAmount を「targetCount に一番近い回数」で、
        /// 金種配列 denominations を使って割り切れる分配を返す。
        /// </summary>
        public List<int> BuildAmountDistribution(int totalAmount, int targetCount)
        {
            int[] denoms = GetSortedDenomAmounts();
            if (denoms.Length == 0) return null;

            // 1) 2 金種以内で total/count 両方一致する組み合わせを優先的に探す
            List<int> bestExact = null;
            int bestCountDiff = int.MaxValue;

            for (int i = 0; i < denoms.Length; i++)
            {
                int a = denoms[i];

                // 単一金種
                if (totalAmount % a == 0)
                {
                    int count = totalAmount / a;
                    int diff = Mathf.Abs(count - targetCount);
                    if (diff < bestCountDiff)
                    {
                        bestCountDiff = diff;
                        bestExact = new List<int>(count);
                        for (int k = 0; k < count; k++) bestExact.Add(a);
                    }
                }

                // 2 金種組み合わせ: a*x + b*y = total, x + y = targetCount
                for (int j = i + 1; j < denoms.Length; j++)
                {
                    int b = denoms[j];
                    int num = totalAmount - a * targetCount;
                    int den = b - a;
                    if (den == 0) continue;
                    if (num < 0) continue;
                    if (num % den != 0) continue;
                    int y = num / den;
                    int x = targetCount - y;
                    if (x < 0 || y < 0) continue;

                    // 回数が targetCount にピタリ合う
                    int totalCount = x + y;
                    int diff = Mathf.Abs(totalCount - targetCount);
                    if (diff < bestCountDiff)
                    {
                        bestCountDiff = diff;
                        bestExact = new List<int>(totalCount);
                        for (int k = 0; k < x; k++) bestExact.Add(a);
                        for (int k = 0; k < y; k++) bestExact.Add(b);
                    }
                }
            }

            if (bestExact != null) return bestExact;

            // 2) フォールバック: 回数固定で、金額ができるだけ近づくよう
            int avg = Mathf.Max(denoms[0], totalAmount / targetCount);
            int baseDenom = denoms[0];
            foreach (int d in denoms)
                if (d <= avg) baseDenom = d;

            var result = new List<int>(targetCount);
            int remaining = totalAmount;
            for (int k = 0; k < targetCount - 1; k++)
            {
                result.Add(baseDenom);
                remaining -= baseDenom;
            }

            // 最後の 1 枚は残額に最も近い金種
            int nearest = denoms[0];
            int minDiff = int.MaxValue;
            foreach (int d in denoms)
            {
                int diff = Mathf.Abs(d - remaining);
                if (diff < minDiff) { minDiff = diff; nearest = d; }
            }
            result.Add(nearest);

            return result;
        }

        int[] GetSortedDenomAmounts()
        {
            if (denominations == null || denominations.Length == 0)
                return new int[0];

            var list = new List<int>(denominations.Length);
            foreach (var d in denominations)
                if (d != null && d.amount > 0)
                    list.Add(d.amount);

            list.Sort();
            return list.ToArray();
        }

        // ────────────────────────────────────────
        //  Spawn
        // ────────────────────────────────────────

        public void SpawnChat(int amount)
        {
            if (superChatPrefab == null) return;
            if (spawnParent == null)
            {
                Debug.LogWarning("[SuperChatSpawner] spawnParent が未設定です");
                return;
            }

            // 同時表示数を超える古いものを破棄
            _activeChats.RemoveAll(c => c == null);
            while (maxConcurrent > 0 && _activeChats.Count >= maxConcurrent)
            {
                var oldest = _activeChats[0];
                _activeChats.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            var obj = Instantiate(superChatPrefab, spawnParent);

            // スポーン位置 ─ ベース位置から stackYOffset 分下に順番にずらす
            var rt = obj.transform as RectTransform;
            if (rt != null)
            {
                Vector2 basePos = overrideSpawnPosition
                    ? spawnAnchoredPosition
                    : rt.anchoredPosition;
                rt.anchoredPosition = basePos + new Vector2(0f, -_stackIndex * stackYOffset);
                _stackIndex = maxConcurrent > 0 ? (_stackIndex + 1) % maxConcurrent : _stackIndex + 1;
            }

            var instance = obj.GetComponent<SuperChatInstance>();
            if (instance == null)
                instance = obj.AddComponent<SuperChatInstance>();

            Color color = GetColorFor(amount);
            instance.Setup(amount, color);
            instance.Play();

            _activeChats.Add(instance);
            _stageEarnings += amount;

            Debug.Log($"[SuperChatSpawner] Spawn {amount}円  color={color}");
        }

        Color GetColorFor(int amount)
        {
            if (denominations == null) return Color.white;

            DenomColor best = null;
            int bestDiff = int.MaxValue;
            foreach (var d in denominations)
            {
                if (d == null) continue;
                int diff = Mathf.Abs(d.amount - amount);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = d;
                }
            }
            return best != null ? best.color : Color.white;
        }
    }
}
