using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Drill rigs (Deep Core, Drillworks branch). Every drill works the frontier (the deepest layer), so they're drawn
    /// together on that row: up to three animated rigs side by side, a "x N" badge when there are more, vibration and
    /// rock dust from the tips, and drill-orange cash popping from the lead rig. When the drills break through, the
    /// old row bursts with debris and "DRILLED THROUGH!" and the rigs drop onto the next layer. Everything runs
    /// faster while overclocked. The digging itself happens in GameManager; the row's bar shows the speed-up.
    /// </summary>
    public class DrillRigsView : MonoBehaviour
    {
        const int MaxShown = 3;

        [SerializeField] GameManager game;
        [SerializeField] FxLayer fx;
        [Tooltip("Animation frames, played in a loop.")]
        [SerializeField] Sprite[] frames = new Sprite[0];
        [Tooltip("Older single-image setups; used when no frames are set.")]
        [SerializeField] Sprite drillSprite;
        [SerializeField] Color drillColor = Color.white;
        // Named apart from the old offsetFromRight field so a stale value saved in older scenes isn't picked up.
        [Tooltip("Lead drill position, measured from the middle of the row's right edge. Negative X = further left, negative Y = lower. " +
                 "Default sits in the rock of the row's last miner slot, clear of the slot counter and the progress bar. Live-editable in Play mode.")]
        [SerializeField] Vector2 drillOffset = new Vector2(-530f, -62f);
        [Tooltip("Drill width and height. Live-editable in Play mode.")]
        [SerializeField] Vector2 drillSize = new Vector2(88f, 122f);
        [Tooltip("Where each extra drill sits relative to the one before it. Live-editable in Play mode.")]
        [SerializeField] Vector2 extraDrillStep = new Vector2(-62f, 4f);
        [SerializeField] float framesPerSecond = 16f;
        [SerializeField] Color dustColor = new Color(0.55f, 0.42f, 0.3f, 1f);
        [Tooltip("Seconds between drill cash pops (each shows what the drills earned in that time).")]
        [SerializeField] float cashPopSeconds = 2.5f;

        static readonly Color CashColor = new Color(1f, 0.62f, 0.25f, 1f);
        static readonly Color[] Debris = { new Color(0.55f, 0.42f, 0.3f, 1f), new Color(0.75f, 0.6f, 0.42f, 1f), new Color(1f, 0.62f, 0.25f, 1f), Color.white };

        RectTransform _root;            // on the frontier row
        readonly RectTransform[] _rigs = new RectTransform[MaxShown];
        readonly Image[] _imgs = new Image[MaxShown];
        Text _badge;
        LayerRowView _row;
        float _scan, _dust, _cash, _drop = 1f;
        double _cashBank;

        void Start() { game.DrilledThrough += OnDrilledThrough; }
        void OnDestroy() { if (game != null) game.DrilledThrough -= OnDrilledThrough; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            int drills = game.DrillsActive;
            _scan -= dt;
            if (_scan <= 0f || (_row != null && (!_row.isActiveAndEnabled || _row.Layer == null || !game.HasDrill(_row.Layer.Index))))
            {
                _scan = 0.5f;
                _row = FindFrontierRow();
            }
            if (drills <= 0 || _row == null) { if (_root != null && _root.gameObject.activeSelf) _root.gameObject.SetActive(false); return; }
            if (_root == null) Build(_row);
            if (_root.parent != _row.transform) { _root.SetParent(_row.transform, false); _root.SetAsLastSibling(); }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);

            bool fast = game.OverclockActive;
            float fps = framesPerSecond * (fast ? 2f : 1f);
            float time = Time.unscaledTime;
            _drop = Mathf.Min(1f, _drop + dt / 0.45f);
            // Drop in from above with a little bounce after a drill-through.
            float dropY = _drop < 1f ? (1f - EaseOutBack(_drop)) * 140f : 0f;

            int shown = Mathf.Min(drills, MaxShown);
            float j = fast ? 3f : 1.5f;
            for (int i = 0; i < MaxShown; i++)
            {
                bool on = i < shown;
                if (_rigs[i].gameObject.activeSelf != on) _rigs[i].gameObject.SetActive(on);
                if (!on) continue;
                if (frames.Length > 0) _imgs[i].sprite = frames[((int)(time * fps) + i * 2) % frames.Length];
                float scale = 1f - 0.08f * i;
                _rigs[i].sizeDelta = drillSize * scale;
                _rigs[i].anchoredPosition = drillOffset + extraDrillStep * i + new Vector2(Random.Range(-j, j), Random.Range(-j, j) + dropY);
                _imgs[i].color = fast ? Color.Lerp(drillColor, new Color(1f, 0.85f, 0.5f), 0.35f + 0.25f * Mathf.Sin(time * 20f + i)) : drillColor;
            }

            _badge.gameObject.SetActive(drills > 1);
            if (drills > 1)
            {
                _badge.text = "×" + drills;
                var b = _badge.rectTransform;
                // Just left of the back rig, clear of the bar's text above.
                b.anchoredPosition = drillOffset + extraDrillStep * (shown - 1) + new Vector2(-drillSize.x * 0.85f, 4f + dropY);
                _badge.color = fast ? Palette.Gold : CashColor;
            }

            _dust -= dt;
            if (_dust <= 0f && fx != null)
            {
                _dust = fast ? 0.1f : 0.2f;
                for (int i = 0; i < shown; i++) fx.SpawnChips(fx.WorldToLocal(Tip(i)), dustColor, fast ? 3 : 2);
            }

            // Drill-orange cash from the lead rig, so their share of the income is visible.
            _cashBank += game.DrillIncome(_row.Layer) * dt;
            _cash -= dt;
            if (_cash <= 0f)
            {
                _cash = cashPopSeconds;
                if (_cashBank > 0 && fx != null)
                    fx.SpawnText(fx.WorldToLocal(_rigs[0].TransformPoint(new Vector3(0f, drillSize.y * 0.6f, 0f))),
                                 "+" + NumberFormat.Money(_cashBank), fast ? Palette.Gold : CashColor, 38, 1.1f, 150f);
                _cashBank = 0;
            }
        }

        void OnDrilledThrough(MineLayer layer)
        {
            if (_root == null || !_root.gameObject.activeInHierarchy || fx == null) { _drop = 0f; return; }
            int shown = Mathf.Min(game.DrillsActive, MaxShown);
            for (int i = 0; i < shown; i++)
            {
                Vector2 tip = fx.WorldToLocal(Tip(i));
                for (int k = 0; k < Debris.Length; k++) fx.SpawnChips(tip + Random.insideUnitCircle * 20f, Debris[k], 8);
            }
            Vector2 at = fx.WorldToLocal(_rigs[0].TransformPoint(Vector3.zero));
            fx.SpawnText(at + new Vector2(120f, 70f), "DRILLED THROUGH!", game.OverclockActive ? Palette.Gold : CashColor, 50, 1.3f, 190f);
            Haptics.Play(Haptic.Medium);
            _drop = 0f;
        }

        Vector3 Tip(int i) { return _rigs[i].TransformPoint(new Vector3(0f, -_rigs[i].rect.height * 0.5f, 0f)); }

        LayerRowView FindFrontierRow()
        {
            foreach (var row in FindObjectsOfType<LayerRowView>())
                if (row.isActiveAndEnabled && row.Layer != null && game.HasDrill(row.Layer.Index)) return row;
            return null;
        }

        void Build(LayerRowView row)
        {
            _root = (RectTransform)new GameObject("Drill Rigs", typeof(RectTransform)).transform;
            _root.SetParent(row.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            _root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            // Back rigs first so the lead rig draws on top.
            for (int i = MaxShown - 1; i >= 0; i--)
            {
                var rt = (RectTransform)new GameObject("Drill Rig " + (i + 1), typeof(RectTransform), typeof(Image)).transform;
                rt.SetParent(_root, false);
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.sizeDelta = drillSize;
                var img = rt.GetComponent<Image>();
                img.sprite = frames.Length > 0 ? frames[0] : drillSprite;
                img.color = drillColor;
                img.preserveAspect = true;
                img.raycastTarget = false;
                _rigs[i] = rt;
                _imgs[i] = img;
            }
            var badge = (RectTransform)new GameObject("Drill Count", typeof(RectTransform), typeof(Text)).transform;
            badge.SetParent(_root, false);
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 0.5f);
            badge.sizeDelta = new Vector2(100f, 60f);
            _badge = badge.GetComponent<Text>();
            _badge.font = row.GetComponentInChildren<Text>(true).font;
            _badge.fontSize = 44;
            _badge.alignment = TextAnchor.MiddleCenter;
            _badge.horizontalOverflow = HorizontalWrapMode.Overflow;
            _badge.raycastTarget = false;
            var outline = badge.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.07f, 0f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
