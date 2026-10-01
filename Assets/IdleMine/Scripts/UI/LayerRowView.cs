using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleMine
{
    [Serializable]
    public class MinerFigure
    {
        public Image slot;          // slot background
        public RectTransform miner; // the little miner (hidden when the slot is empty)
        public RectTransform pick;  // pickaxe pivot that swings
        [NonSerialized] public float phase;
    }

    /// <summary>
    /// One row of the mine (LayerRow prefab). MineView pools these and re-binds them while scrolling,
    /// so the mine can be hundreds of layers deep while only ~8 rows exist.
    /// A row shows a real layer, the locked layer just below the frontier, or fading darkness beyond.
    /// </summary>
    public class LayerRowView : MonoBehaviour, IPointerClickHandler
    {
        enum Mode { Layer, Locked, Dark }

        [Header("Panel")]
        [SerializeField] RectTransform visual;
        [SerializeField] Image panel;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Text incomeText;
        [SerializeField] Text centerText;
        [SerializeField] Text hintText;

        [Header("Breakthrough bar")]
        [SerializeField] GameObject progressBar;
        [SerializeField] Image progressFill;
        [SerializeField] Text progressText;
        [SerializeField] Color progressColor = new Color(1f, 0.78f, 0.27f, 1f);
        [SerializeField] Color clearedColor = new Color(0.42f, 0.88f, 0.48f, 0.55f);

        [Header("Miners")]
        [SerializeField] GameObject slotsArea;
        [SerializeField] List<MinerFigure> figures = new List<MinerFigure>();
        [SerializeField] Text overflowText;
        [SerializeField] float slotSpacing = 80f;
        [SerializeField] float slotWidth = 74f;

        [Header("Controls")]
        [SerializeField] GameObject controls;
        [SerializeField] Button plusButton;
        [SerializeField] Button minusButton;
        [SerializeField] HoldRepeater plusRepeater;
        [SerializeField] HoldRepeater minusRepeater;
        [SerializeField] Text countText;
        [SerializeField] Color countHighlight = new Color(1f, 0.62f, 0.26f, 1f);

        GameManager _game;
        FxLayer _fx;
        Mode _mode;
        int _row = -1;
        int _shownMiners = -1, _shownSlots = -1;
        Color _countNormal;

        public int RowIndex { get { return _row; } }
        public MineLayer Layer { get; private set; }
        public Vector3 MinerAreaWorld { get { return slotsArea.transform.position; } }

        /// <summary>Called once by MineView right after instantiating the prefab.</summary>
        public void Init(GameManager game, FxLayer fx)
        {
            _game = game;
            _fx = fx;
            _countNormal = countText.color;
            plusRepeater.OnFire = () => Step(+1);
            minusRepeater.OnFire = () => Step(-1);
            foreach (var f in figures) f.phase = UnityEngine.Random.value;
        }

        void Step(int delta)
        {
            if (Layer == null) return;
            if (_game.AssignMiners(Layer.Index, delta) != 0)
            {
                Punch.Play(countText.transform, 0.2f, 0.2f);
                RefreshText();
            }
        }

        // ================================================================== binding

        public void Unbind()
        {
            _row = -1;
            Layer = null;
        }

        public void Bind(int row)
        {
            _row = row;
            _shownMiners = _shownSlots = -1;
            int layers = _game.Layers.Count;
            _mode = row < layers ? Mode.Layer : (row == layers ? Mode.Locked : Mode.Dark);
            Layer = _mode == Mode.Layer ? _game.Layers[row] : null;

            bool real = _mode == Mode.Layer;
            titleText.gameObject.SetActive(_mode != Mode.Dark);
            subtitleText.gameObject.SetActive(real);
            incomeText.gameObject.SetActive(real);
            progressBar.SetActive(real);
            slotsArea.SetActive(real);
            controls.SetActive(real);
            centerText.gameObject.SetActive(!real);

            Color c = Palette.Layer(row);
            if (_mode == Mode.Locked) c = Color.Lerp(c, Color.black, 0.65f);
            if (_mode == Mode.Dark)
            {
                float fade = Mathf.Clamp01((row - layers) / 4f);
                c = Color.Lerp(Color.Lerp(c, Color.black, 0.8f), Palette.Background, fade);
            }
            panel.color = c;

            if (_mode == Mode.Locked)
            {
                titleText.text = "DEPTH " + row + "  \u00B7  ???";
                centerText.text = "LOCKED\n<size=28>Break through Depth " + (row - 1) + " to open</size>";
            }
            else if (_mode == Mode.Dark)
            {
                centerText.text = "\u00B7   \u00B7   \u00B7";
            }
            RefreshText();
        }

        public void RefreshText()
        {
            hintText.gameObject.SetActive(_mode == Mode.Layer && Layer.Index == 0 && _game.TotalMiners == 0);
            if (_mode != Mode.Layer) return;
            var l = Layer;

            titleText.text = "DEPTH " + l.Index + "  \u00B7  " + LayerCatalog.BandName(l.Index);
            subtitleText.text = LayerCatalog.OreName(l.Index) + " ore  \u00B7  " + NumberFormat.Money(l.ValuePerOre) + " each" + RichnessTag(l);

            double inc = _game.LayerIncome(l);
            incomeText.text = inc > 0 ? "+" + NumberFormat.Money(inc) + "/s" : "";

            if (l.Cleared)
            {
                progressFill.color = clearedColor;
                progressText.text = "CLEARED  \u00B7  still producing";
            }
            else
            {
                progressFill.color = progressColor;
                progressText.text = NumberFormat.Format(l.Progress) + " / " + NumberFormat.Format(l.OreRequired) + " to break through";
            }

            int slots = _game.SlotsPerLayer;
            bool canAdd = l.Miners < slots && _game.FreeMiners > 0;
            countText.text = l.Miners + "/" + slots;
            countText.color = canAdd ? countHighlight : _countNormal;
            plusButton.interactable = canAdd;
            minusButton.interactable = l.Miners > 0;

            if (l.Miners != _shownMiners || slots != _shownSlots) RefreshSlots(l.Miners, slots);
        }

        static string RichnessTag(MineLayer l)
        {
            if (l.IsMotherlode) return "   <color=#FFC845>MOTHERLODE \u00D73</color>";
            if (l.Richness >= 1.2) return "   <color=#6BE07B>RICH \u00D7" + l.Richness.ToString("0.0") + "</color>";
            if (l.Richness <= 0.85) return "   <color=#FF9F9F>POOR \u00D7" + l.Richness.ToString("0.0") + "</color>";
            return "";
        }

        void RefreshSlots(int miners, int slots)
        {
            _shownMiners = miners;
            _shownSlots = slots;
            int max = figures.Count;
            bool overflow = slots > max;
            int visibleSlots = overflow ? max - 1 : slots;
            for (int i = 0; i < max; i++)
            {
                var f = figures[i];
                bool slotVisible = i < visibleSlots;
                f.slot.gameObject.SetActive(slotVisible);
                f.miner.gameObject.SetActive(slotVisible && i < miners);
            }
            overflowText.gameObject.SetActive(overflow);
            if (overflow)
            {
                int hidden = Mathf.Max(0, miners - visibleSlots);
                overflowText.rectTransform.anchoredPosition = new Vector2(visibleSlots * slotSpacing + slotWidth * 0.5f, 0);
                overflowText.text = "+" + hidden + "\n<size=22>of " + (slots - visibleSlots) + "</size>";
            }
        }

        // ================================================================== per-frame

        public void Animate(float time, float swingRate)
        {
            if (_mode != Mode.Layer) return;

            // Smooth bar every frame; text refreshes at 10 Hz from MineView.
            var fill = progressFill.rectTransform;
            float frac = (float)Layer.ProgressFraction;
            if (Mathf.Abs(fill.anchorMax.x - frac) > 0.0005f) fill.anchorMax = new Vector2(frac, 1);

            for (int i = 0; i < figures.Count; i++)
            {
                var f = figures[i];
                if (!f.miner.gameObject.activeSelf) continue;
                // Slow wind-up, fast strike.
                float p = Mathf.Repeat(time * swingRate + f.phase, 1f);
                float angle = p < 0.75f ? Mathf.Lerp(-10f, 70f, p / 0.75f) : Mathf.Lerp(70f, -25f, (p - 0.75f) / 0.25f);
                f.pick.localRotation = Quaternion.Euler(0, 0, angle);
                float bob = p > 0.75f ? Mathf.Sin((p - 0.75f) * 4f * Mathf.PI) * 3f : 0f;
                f.miner.localPosition = new Vector3(f.miner.localPosition.x, -bob, 0f);
            }
        }

        // ================================================================== tapping

        public void OnPointerClick(PointerEventData e)
        {
            if (_mode != Mode.Layer)
            {
                Punch.Play(visual, 0.02f, 0.2f);
                return;
            }

            var r = _game.Tap(Layer.Index);
            if (r.Layer < 0) return;

            Vector2 local = _fx.ScreenToLocal(e.position, e.pressEventCamera);
            Color chip = Palette.Layer(Layer.Index);
            if (r.Crit)
            {
                _fx.SpawnText(local + new Vector2(0, 30), "CRIT! +" + NumberFormat.Money(r.Money), Palette.Orange, 64, 1.1f, 220f);
                _fx.SpawnChips(local, Color.Lerp(chip, Palette.Gold, 0.6f), 14);
            }
            else
            {
                _fx.SpawnText(local + new Vector2(0, 30), "+" + NumberFormat.Money(r.Money), Palette.Gold, 48);
                _fx.SpawnChips(local, Color.Lerp(chip, Color.white, 0.25f), 5);
            }
            Punch.Play(visual, r.Crit ? 0.035f : 0.012f, 0.18f);
            RefreshText();
        }
    }
}
