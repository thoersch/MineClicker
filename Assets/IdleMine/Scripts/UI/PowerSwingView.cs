using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Power swings (Deep Core, Timing branch). Now and then a POWER SWING panel slides up over the mine: a pickaxe
    /// sweeps back and forth along a bar, past a bright gold PERFECT zone inside a wider orange GOOD zone. While it's
    /// live, tapping anywhere on the mine swings. PERFECT strikes the deepest layer for SwingPower seconds of income
    /// with a big payoff (hit-stop, flash, sunburst, shockwave, confetti, screen shake, fanfare, heavy haptics);
    /// GOOD pays a quarter; anything else is a MISS. Wait too long and it's gone. For the first swingArmSeconds
    /// ("GET READY...") taps are swallowed, so a tap meant for a layer never counts as an instant miss.
    /// The catcher (full-area) and panel Buttons are wired in the Inspector to Strike().
    /// </summary>
    public class PowerSwingView : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] FxLayer fx;
        [SerializeField] RectTransform shakeTarget;

        [Header("Panel")]
        [SerializeField] GameObject catcher;
        [SerializeField] CanvasGroup panel;
        [SerializeField] RectTransform bar;
        [SerializeField] Image barImage;
        [SerializeField] RectTransform goodZone;
        [SerializeField] RectTransform perfectZone;
        [SerializeField] Image perfectImage;
        [SerializeField] RectTransform marker;
        [SerializeField] RectTransform[] trail = new RectTransform[0];
        [SerializeField] Image timerFill;
        [SerializeField] Text titleText;
        [SerializeField] Text hintText;

        [Header("Payoff")]
        [SerializeField] Image flash;
        [SerializeField] RectTransform burst;
        [SerializeField] Image burstImage;
        [SerializeField] RectTransform ring;
        [SerializeField] Image ringImage;
        [SerializeField] Text resultText;
        [SerializeField] Text resultSub;

        [Header("Covered by")]
        [SerializeField] SkillTreeView skillTree;
        [SerializeField] SkillTreeView deepCore;
        [SerializeField] ParagonView paragon;
        [SerializeField] OfferPopup offers;

        enum State { Waiting, Live, Result }
        State _state;
        float _next = -1f, _t, _zoneCenter, _perfectWidth, _goodWidth, _phase, _resultHold;
        Vector2 _panelHome, _shakeHome;
        RectTransform _panelRect, _root;

        static readonly Color GoodColor = Palette.WithAlpha(Palette.Orange, 0.55f);

        void Start()
        {
            _root = (RectTransform)transform;
            _panelRect = (RectTransform)panel.transform;
            _panelHome = _panelRect.anchoredPosition;
            if (shakeTarget != null) _shakeHome = shakeTarget.anchoredPosition;
            HideAll();
        }

        float _restoreScale = -1f; // set during the hit-stop so an interruption can't leave the game frozen

        void RestoreTime()
        {
            if (_restoreScale >= 0f) { Time.timeScale = _restoreScale; _restoreScale = -1f; }
        }

        void OnDisable() { RestoreTime(); }

        void HideAll()
        {
            panel.gameObject.SetActive(false);
            catcher.SetActive(false);
            flash.gameObject.SetActive(false);
            burst.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
            resultText.gameObject.SetActive(false);
            resultSub.gameObject.SetActive(false);
        }

        bool Covered
        {
            get
            {
                return (skillTree != null && skillTree.IsOpen) || (deepCore != null && deepCore.IsOpen)
                       || (paragon != null && paragon.IsOpen) || (offers != null && offers.IsOpen);
            }
        }

        float Interval()
        {
            double rate = game.Stats.Get(StatType.SwingRate);
            return rate <= 0 ? 999f : (float)(60.0 / rate) * Random.Range(0.7f, 1.3f);
        }

        float PosAt(float phase) { return 0.5f + 0.5f * Mathf.Sin(phase); }
        float MarkerPos { get { return PosAt(_phase); } }
        bool InPerfect { get { return Mathf.Abs(MarkerPos - _zoneCenter) <= _perfectWidth * 0.5f; } }
        bool Armed { get { return _t >= game.Config.swingArmSeconds; } }
        bool _wasArmed;

        // ================================================================== frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!game.SwingsUnlocked)
            {
                if (_state != State.Waiting) { HideAll(); _state = State.Waiting; }
                _next = -1f;
                return;
            }
            if (_next < 0f) _next = Interval();

            switch (_state)
            {
                case State.Waiting:
                    if (!Covered) _next -= dt;
                    if (_next <= 0f) Show();
                    break;
                case State.Live: UpdateLive(dt); break;
                case State.Result: UpdateResult(dt); break;
            }
        }

        void UpdateLive(float dt)
        {
            _t += dt;
            float life = game.Config.swingLifetimeSeconds;
            _phase += dt * game.Config.swingSpeed * Mathf.PI * 2f;

            // Slide up with a little bounce, fade out at the very end.
            float s = Mathf.Clamp01(_t / 0.35f);
            float back = 1f + 2.2f * Mathf.Pow(s - 1f, 3f) + 1.2f * Mathf.Pow(s - 1f, 2f);
            _panelRect.anchoredPosition = _panelHome + new Vector2(0, -420f * (1f - back));
            panel.alpha = Mathf.Clamp01(_t / 0.15f) * Mathf.Clamp01((life - _t) / 0.4f);

            float w = bar.rect.width;
            marker.anchoredPosition = new Vector2((MarkerPos - 0.5f) * w, 14f);
            for (int i = 0; i < trail.Length; i++)
                trail[i].anchoredPosition = new Vector2((PosAt(_phase - (i + 1) * 0.16f) - 0.5f) * w, 14f);

            // A short arming beat first, so a tap meant for a layer can't count as a swing before you've seen it.
            bool armed = Armed;
            if (armed && !_wasArmed)
            {
                _wasArmed = true;
                Punch.Play(hintText.transform, 0.25f, 0.3f);
                Haptics.Play(Haptic.Light);
            }
            var pickImage = marker.GetComponent<Image>();
            if (pickImage != null) pickImage.color = armed ? Color.white : new Color(1f, 1f, 1f, 0.45f);

            bool hot = armed && InPerfect;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            perfectImage.color = hot ? Color.Lerp(Palette.Gold, Color.white, 0.55f) : Color.Lerp(Palette.Gold, Palette.Orange, 0.25f * pulse);
            float zs = hot ? 1.12f : 1f + 0.03f * pulse;
            perfectZone.localScale = new Vector3(1f, zs, 1f);
            marker.localScale = Vector3.one * (hot ? 1.15f : 1f);
            hintText.text = !armed ? "GET READY..." : hot ? "<size=46>NOW!</size>" : "TAP ANYWHERE when the pick is in the <color=#FFC845>GOLD</color>";
            hintText.color = !armed ? Palette.TextDim : hot ? Palette.Gold : Palette.Text;

            timerFill.fillAmount = 1f - _t / life;
            if (_t >= life) Finish(0, 0, true);
        }

        void UpdateResult(float dt)
        {
            _t += dt;
            if (_t > _resultHold)
            {
                float k = Mathf.Clamp01((_t - _resultHold) / 0.3f);
                panel.alpha = 1f - k;
                var c = resultText.color; c.a = 1f - k; resultText.color = c;
                var c2 = resultSub.color; c2.a = 1f - k; resultSub.color = c2;
                if (k >= 1f) { HideAll(); _state = State.Waiting; _next = Interval(); }
            }
        }

        // ================================================================== show / strike

        /// <summary>Debug/testing: start a power swing now (only shows while power swings are unlocked).</summary>
        public void ForceShow() { if (game.SwingsUnlocked && _state != State.Live) Show(); }

        void Show()
        {
            StopAllCoroutines();
            RestoreTime();
            HideAll();
            _state = State.Live;
            _t = 0f;
            _wasArmed = false;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _perfectWidth = Mathf.Max(0.04f, (float)game.Stats.Get(StatType.SwingWindow));
            _goodWidth = Mathf.Min(0.6f, _perfectWidth * 2.2f);
            _zoneCenter = Random.Range(0.06f + _goodWidth * 0.5f, 0.94f - _goodWidth * 0.5f);

            float w = bar.rect.width;
            goodZone.anchoredPosition = new Vector2((_zoneCenter - 0.5f) * w, 0f);
            goodZone.sizeDelta = new Vector2(_goodWidth * w, goodZone.sizeDelta.y);
            goodZone.GetComponent<Image>().color = GoodColor;
            perfectZone.sizeDelta = new Vector2(_perfectWidth * w, perfectZone.sizeDelta.y);
            barImage.color = Palette.PanelLight;
            titleText.text = "POWER SWING!";
            titleText.color = Palette.Gold;
            _panelRect.localScale = Vector3.one;

            panel.gameObject.SetActive(true);
            catcher.SetActive(true);
            panel.alpha = 0f;
            transform.SetAsLastSibling();
            Feedback.Play(Sfx.Cart, 0.7f);
            Punch.Play(titleText.transform, 0.25f, 0.4f);
        }

        /// <summary>Inspector-wired to the panel and the full-area catcher.</summary>
        public void Strike()
        {
            if (_state != State.Live || !Armed) return; // taps before it arms are swallowed: no dig, no miss
            float d = Mathf.Abs(MarkerPos - _zoneCenter);
            double quality = d <= _perfectWidth * 0.5f ? 1.0 : d <= _goodWidth * 0.5f ? 0.25 : 0.0;
            double money = game.PowerSwing(quality);
            Finish(quality, money, false);
        }

        void Finish(double quality, double money, bool timedOut)
        {
            _state = State.Result;
            _t = 0f;
            catcher.SetActive(false);
            panel.alpha = 1f;
            _panelRect.anchoredPosition = _panelHome;
            hintText.text = "";
            if (quality >= 1.0) { _resultHold = 1.9f; StartCoroutine(Perfect(money)); }
            else if (quality > 0) { _resultHold = 1.1f; Good(money); }
            else { _resultHold = 1.0f; StartCoroutine(Miss(timedOut)); }
        }

        // ================================================================== payoffs

        Vector2 MarkerLocal() { return _root.InverseTransformPoint(marker.position); }

        IEnumerator Perfect(double money)
        {
            Vector2 at = MarkerLocal();
            Vector2 fxAt = fx.WorldToLocal(marker.position);
            Feedback.Play(Sfx.Perfect);
            titleText.text = "PERFECT!";

            // Hit-stop: the world freezes for a beat on impact.
            float prevScale = Time.timeScale;
            _restoreScale = prevScale;
            Time.timeScale = 0.05f;

            flash.gameObject.SetActive(true);
            burst.gameObject.SetActive(true);
            ring.gameObject.SetActive(true);
            resultText.gameObject.SetActive(true);
            resultSub.gameObject.SetActive(true);
            burst.anchoredPosition = at;
            ring.anchoredPosition = at;
            resultText.text = "PERFECT!";
            resultText.color = Palette.Gold;
            resultSub.color = Palette.Text;
            flash.transform.SetAsLastSibling();

            Color[] confetti = { Palette.Gold, Palette.Text, Palette.Orange, Palette.Hex("FFE08A") };
            for (int i = 0; i < 3; i++) fx.SpawnChips(fxAt, confetti[i], 26);
            float shakeT = 0f, t = 0f;
            int haptic = 0, wave = 0;
            float[] hapticAt = { 0f, 0.09f, 0.3f };
            Haptic[] beats = { Haptic.Heavy, Haptic.Rigid, Haptic.Success };

            while (t < 1.6f)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                if (t > 0.13f) RestoreTime();
                while (haptic < hapticAt.Length && t >= hapticAt[haptic]) Haptics.Play(beats[haptic++]);
                if (wave < 3 && t > 0.1f + wave * 0.12f)
                {
                    float side = wave % 2 == 0 ? -1f : 1f;
                    fx.SpawnChips(fxAt + new Vector2(side * 260f, -60f), confetti[(wave + 1) % confetti.Length], 22);
                    fx.SpawnChips(fxAt + new Vector2(-side * 160f, 40f), confetti[wave % confetti.Length], 16);
                    wave++;
                }

                float a = Mathf.Clamp01(t / 0.35f);
                flash.color = new Color(1f, 0.97f, 0.85f, (1f - a) * 0.85f);
                float b = Mathf.Clamp01(t / 0.9f);
                burst.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.4f, 1f - (1f - b) * (1f - b));
                burst.localRotation = Quaternion.Euler(0, 0, -t * 60f);
                burstImage.color = Palette.WithAlpha(Palette.Gold, 0.9f * (1f - b));
                float r = Mathf.Clamp01(t / 0.6f);
                ring.localScale = Vector3.one * Mathf.Lerp(0.2f, 5f, r);
                ringImage.color = Palette.WithAlpha(Palette.Text, 0.9f * (1f - r));

                // "PERFECT!" slams in with an overshoot; the payout counts up underneath.
                float x = Mathf.Clamp01(t / 0.35f);
                float slam = Mathf.Lerp(3f, 1f, x) + 0.18f * Mathf.Sin(x * Mathf.PI) * (1f - x);
                resultText.rectTransform.localScale = new Vector3(slam, slam, 1f);
                float count = Mathf.Clamp01((t - 0.15f) / 0.7f);
                resultSub.text = "+" + NumberFormat.Money(money * (1.0 - (1.0 - count) * (1.0 - count)));

                shakeT += dt;
                float k = 1f - shakeT / 0.4f;
                if (k > 0f)
                {
                    var off = Random.insideUnitCircle * 22f * k * k;
                    _panelRect.anchoredPosition = _panelHome + off;
                    if (shakeTarget != null) shakeTarget.anchoredPosition = _shakeHome + off * 0.6f;
                }
                else
                {
                    _panelRect.anchoredPosition = _panelHome;
                    if (shakeTarget != null) shakeTarget.anchoredPosition = _shakeHome;
                }
                yield return null;
            }
            RestoreTime();
            flash.gameObject.SetActive(false);
            burst.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
        }

        void Good(double money)
        {
            titleText.text = "GOOD";
            titleText.color = Palette.Green;
            resultText.gameObject.SetActive(true);
            resultSub.gameObject.SetActive(true);
            resultText.text = "GOOD";
            resultText.color = Palette.Green;
            resultText.rectTransform.localScale = Vector3.one * 0.75f;
            resultSub.text = "+" + NumberFormat.Money(money);
            resultSub.color = Palette.Text;
            Punch.Play(resultText.transform, 0.3f, 0.35f);
            fx.SpawnChips(fx.WorldToLocal(marker.position), Palette.Green, 16);
            Feedback.Play(Sfx.SwingHit, 0.7f);
            Haptics.Play(Haptic.Medium);
        }

        IEnumerator Miss(bool timedOut)
        {
            titleText.text = timedOut ? "TOO SLOW" : "MISS";
            titleText.color = Palette.Red;
            Feedback.Play(Sfx.SwingMiss);
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / 0.35f;
                _panelRect.anchoredPosition = _panelHome + new Vector2(Mathf.Sin(t * 60f) * 18f * k, 0f);
                barImage.color = Color.Lerp(Palette.PanelLight, Palette.Red, k);
                yield return null;
            }
            _panelRect.anchoredPosition = _panelHome;
            barImage.color = Palette.PanelLight;
        }
    }
}
