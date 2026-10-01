using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// The mine: a vertical ScrollRect with the surface scene on top and one LayerRow per depth below.
    /// Rows are virtualized (pooled and re-bound while scrolling), so depth is effectively unlimited.
    /// </summary>
    public class MineView : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] FxLayer fx;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] LayerRowView rowPrefab;
        [SerializeField] GameObject goDeepestButton;

        [Header("Layout")]
        [Tooltip("Height of the surface scene at the top of Content.")]
        [SerializeField] float surfaceHeight = 380f;
        [SerializeField] float rowHeight = 300f;
        [Tooltip("Locked preview + darkness rows shown below the deepest layer.")]
        [SerializeField] int extraRows = 4;

        readonly List<LayerRowView> _rows = new List<LayerRowView>();
        RectTransform _viewport;
        int _first, _last, _lastLayerCount = -1;
        float _textTimer, _incomeTimer;
        float _scrollAnimT = -1f, _scrollFrom, _scrollTo;
        int _initialScrollFrames = -1;

        void Start()
        {
            _viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            // Returning players start at their deepest layer (after the canvas has sized itself).
            _initialScrollFrames = game.Depth >= 2 ? 2 : -1;
        }

        /// <summary>Hooked to the GO DEEPEST button in the Inspector.</summary>
        public void ScrollToDeepest() { ScrollToRow(game.Depth); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_initialScrollFrames >= 0 && _initialScrollFrames-- == 0) ScrollToRow(game.Depth, true);
            AnimateScroll(dt);

            int layerCount = game.Layers.Count;
            int rowCount = layerCount + extraRows;
            float contentH = surfaceHeight + rowCount * rowHeight;
            if (Mathf.Abs(content.sizeDelta.y - contentH) > 0.5f) content.sizeDelta = new Vector2(content.sizeDelta.x, contentH);

            // Which rows intersect the viewport?
            float top = content.anchoredPosition.y;
            float vh = _viewport.rect.height;
            _first = Mathf.Max(0, Mathf.FloorToInt((top - surfaceHeight) / rowHeight));
            _last = Mathf.Min(rowCount - 1, Mathf.FloorToInt((top + vh - surfaceHeight) / rowHeight));

            bool layersChanged = layerCount != _lastLayerCount;
            _lastLayerCount = layerCount;

            // Release rows that scrolled away, or whose meaning changed because a new layer opened.
            foreach (var r in _rows)
            {
                if (r.RowIndex < 0) continue;
                if (r.RowIndex < _first || r.RowIndex > _last || (layersChanged && r.RowIndex >= layerCount - 1))
                {
                    r.Unbind();
                    r.gameObject.SetActive(false);
                }
            }
            // Bind rows that scrolled in.
            for (int idx = _first; idx <= _last; idx++)
            {
                if (FindRow(idx) != null) continue;
                var row = GetFreeRow();
                row.gameObject.SetActive(true);
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -(surfaceHeight + idx * rowHeight));
                row.Bind(idx);
            }

            // Animate visible miners; swing speed grows (gently) with Miner Speed.
            float speed = (float)game.Stats.Get(StatType.MinerSpeed);
            float swing = Mathf.Clamp(0.9f + 0.35f * Mathf.Log10(Mathf.Max(1f, speed)), 0.9f, 3.5f);
            float time = Time.time;
            foreach (var r in _rows) if (r.RowIndex >= 0) r.Animate(time, swing);

            _textTimer -= dt;
            if (_textTimer <= 0f)
            {
                _textTimer = 0.1f;
                foreach (var r in _rows) if (r.RowIndex >= 0) r.RefreshText();
            }

            _incomeTimer -= dt;
            if (_incomeTimer <= 0f)
            {
                _incomeTimer = 1f;
                PopIdleIncome();
            }

            bool frontierVisible = game.Depth >= _first && game.Depth <= _last;
            goDeepestButton.SetActive(!frontierVisible && _scrollAnimT < 0f);
        }

        /// <summary>Once a second each visible working layer pops a "+$X" with what it earned. Payday rhythm.</summary>
        void PopIdleIncome()
        {
            foreach (var r in _rows)
            {
                if (r.RowIndex < 0 || r.Layer == null || r.Layer.PendingIncome <= 0) continue;
                Vector2 pos = fx.WorldToLocal(r.MinerAreaWorld) + new Vector2(Random.Range(40f, 380f), 90f);
                fx.SpawnText(pos, "+" + NumberFormat.Money(r.Layer.PendingIncome), Palette.WithAlpha(Palette.Gold, 0.95f), 36, 1.1f, 110f);
            }
            foreach (var l in game.Layers) l.PendingIncome = 0;
        }

        LayerRowView FindRow(int idx)
        {
            foreach (var r in _rows) if (r.RowIndex == idx) return r;
            return null;
        }

        LayerRowView GetFreeRow()
        {
            foreach (var r in _rows) if (r.RowIndex < 0) return r;
            var row = Instantiate(rowPrefab, content);
            row.name = "LayerRow " + _rows.Count;
            row.Init(game, fx);
            _rows.Add(row);
            return row;
        }

        // ================================================================== scrolling

        public void ScrollToRow(int row, bool instant = false)
        {
            float vh = _viewport.rect.height;
            float contentH = surfaceHeight + (game.Layers.Count + extraRows) * rowHeight;
            float target = surfaceHeight + row * rowHeight + rowHeight * 0.5f - vh * 0.5f;
            target = Mathf.Clamp(target, 0f, Mathf.Max(0f, contentH - vh));
            scroll.StopMovement();
            if (instant)
            {
                content.sizeDelta = new Vector2(content.sizeDelta.x, contentH);
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, target);
                return;
            }
            _scrollFrom = content.anchoredPosition.y;
            _scrollTo = target;
            _scrollAnimT = 0f;
        }

        void AnimateScroll(float dt)
        {
            if (_scrollAnimT < 0f) return;
            _scrollAnimT += dt / 0.45f;
            float t = Mathf.Clamp01(_scrollAnimT);
            float e = t * t * (3f - 2f * t);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Lerp(_scrollFrom, _scrollTo, e));
            scroll.velocity = Vector2.zero;
            if (t >= 1f) _scrollAnimT = -1f;
        }
    }
}
