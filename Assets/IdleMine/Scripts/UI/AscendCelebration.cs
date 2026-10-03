using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Full-screen moment when you reach a new Paragon level: spinning gold rays, the new level slamming in,
    /// the multiplier jump, a confetti storm, the ascend fanfare and a run of heavy haptics. Tap to continue
    /// (allowed after a short beat so it isn't skipped by accident).
    ///
    /// This component sits on an always-active container; the Overlay child starts inactive. The overlay's
    /// full-screen Button is wired in the Inspector to Dismiss(). It has its own FxLayer so confetti draws on
    /// top of the overlay.
    /// </summary>
    public class AscendCelebration : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] CanvasGroup overlay;
        [SerializeField] FxLayer fx;
        [SerializeField] RectTransform rays;
        [SerializeField] RectTransform raysBack;
        [SerializeField] RectTransform titleGroup;
        [SerializeField] Text kickerText;
        [SerializeField] Text levelText;
        [SerializeField] Text multiplierText;
        [SerializeField] Text pointsText;
        [SerializeField] Text tapHint;
        [SerializeField] float minSeconds = 1.2f;
        [SerializeField] float confettiSeconds = 2.4f;

        static readonly Color[] ConfettiColors = { Palette.Gold, Palette.Orange, Palette.Text, Palette.Hex("B98CFF"), Palette.Sky };
        static readonly float[] HapticTimes = { 0f, 0.18f, 0.42f, 0.75f };
        static readonly Haptic[] HapticBeats = { Haptic.Heavy, Haptic.Medium, Haptic.Heavy, Haptic.Success };

        float _t = -1f, _confettiTimer;
        int _nextHaptic;
        bool _closing;

        void Start()
        {
            game.Ascended += OnAscended;
            overlay.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (game != null) game.Ascended -= OnAscended;
        }

        void OnAscended(int levels)
        {
            int level = game.ParagonLevel;
            double before = System.Math.Pow(1.0 + game.Config.paragonMultiplierPerLevel, level - 1);
            kickerText.text = "ASCENDED";
            levelText.text = "PARAGON " + level;
            multiplierText.text = NumberFormat.Multiplier(before) + "  ›  " + NumberFormat.Multiplier(game.ParagonMultiplier)
                                  + "\n<size=30>Ore Value, Miner Speed, Tap Power & Dig Speed, forever</size>";
            pointsText.text = game.ParagonPoints + " Paragon Point" + (game.ParagonPoints == 1 ? "" : "s")
                              + " to spend in the Paragon Tree this run";

            transform.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            overlay.alpha = 0f;
            _t = 0f;
            _closing = false;
            _confettiTimer = 0f;
            _nextHaptic = 0;
            AudioManager.Play(Sfx.Ascend);
            Animate();
        }

        void Update()
        {
            if (_t < 0f) return;
            float dt = Time.unscaledDeltaTime;
            _t += dt;

            if (_closing)
            {
                overlay.alpha -= dt / 0.3f;
                if (overlay.alpha <= 0f) { overlay.gameObject.SetActive(false); _t = -1f; }
                return;
            }

            while (_nextHaptic < HapticTimes.Length && _t >= HapticTimes[_nextHaptic])
                Haptics.Play(HapticBeats[_nextHaptic++]);

            if (_t < confettiSeconds)
            {
                _confettiTimer -= dt;
                if (_confettiTimer <= 0f)
                {
                    _confettiTimer = 0.09f;
                    var area = ((RectTransform)fx.transform).rect;
                    var at = new Vector2(Random.Range(area.xMin * 0.85f, area.xMax * 0.85f), Random.Range(area.yMin * 0.2f, area.yMax * 0.8f));
                    fx.SpawnChips(at, ConfettiColors[Random.Range(0, ConfettiColors.Length)], 16);
                }
            }
            Animate();
        }

        void Animate()
        {
            overlay.alpha = Mathf.Clamp01(_t / 0.25f);

            rays.localRotation = Quaternion.Euler(0, 0, -_t * 18f);
            raysBack.localRotation = Quaternion.Euler(0, 0, _t * 11f);
            float pulse = 1f + 0.04f * Mathf.Sin(_t * 3f);
            float grow = Mathf.Lerp(0.4f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(_t / 0.6f), 3f));
            rays.localScale = Vector3.one * grow * pulse;
            raysBack.localScale = Vector3.one * grow * 1.15f;

            // The title slams in from big to normal with a little overshoot.
            float x = Mathf.Clamp01((_t - 0.1f) / 0.45f);
            float s = x <= 0f ? 0f : Mathf.Lerp(2.6f, 1f, x) + 0.12f * Mathf.Sin(x * Mathf.PI) * (1f - x);
            titleGroup.localScale = new Vector3(s, s, 1f);

            float hint = _t < minSeconds ? 0f : 0.55f + 0.45f * Mathf.Sin((_t - minSeconds) * 4f);
            var c = tapHint.color; c.a = hint; tapHint.color = c;
        }

        /// <summary>Inspector-wired to the overlay's full-screen button.</summary>
        public void Dismiss()
        {
            if (_t < minSeconds || _closing) return;
            _closing = true;
            Feedback.Play(Sfx.Click);
        }
    }
}
