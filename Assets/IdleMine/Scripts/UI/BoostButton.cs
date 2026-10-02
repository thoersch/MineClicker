using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// HUD button for the ad-bought income boost. Idle: "x2 BOOST" (hidden if no ad can be offered).
    /// Running: a green countdown, tappable to stack more time while under the cap.
    /// The button is wired in the Inspector to Open().
    /// </summary>
    public class BoostButton : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] AdManager ads;
        [SerializeField] OfferPopup offers;
        [SerializeField] Toasts toasts;
        [SerializeField] AdButtonView view;
        [SerializeField] Image background;
        [SerializeField] float heartbeatSeconds = 3f;

        float _timer, _pulseTimer;

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.25f;

            bool active = game.BoostActive;
            bool canBuy = ads.CanOffer(AdPlacement.IncomeBoost) && game.CanExtendBoost;
            view.Visible = active || canBuy;
            if (!view.Visible) return;

            view.Button.interactable = canBuy;
            if (active)
            {
                view.Set(NumberFormat.Multiplier(game.Config.boostMultiplier) + "\n<size=34>" + Clock(game.BoostSecondsLeft) + "</size>",
                         canBuy && !ads.HasForemanPass);
                background.color = Palette.Green;
            }
            else
            {
                view.Set(NumberFormat.Multiplier(game.Config.boostMultiplier) + "\n<size=34>BOOST</size>", !ads.HasForemanPass);
                background.color = Palette.Gold;
                _pulseTimer -= 0.25f;
                if (_pulseTimer <= 0f) { _pulseTimer = heartbeatSeconds; Punch.Play(view.transform, 0.12f, 0.35f); }
            }
        }

        static string Clock(double seconds)
        {
            int s = Mathf.CeilToInt((float)seconds);
            int h = s / 3600, m = s / 60 % 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + (s % 60).ToString("00") : m + ":" + (s % 60).ToString("00");
        }

        /// <summary>Inspector-wired to the button.</summary>
        public void Open()
        {
            var cfg = game.Config;
            string mult = NumberFormat.Multiplier(cfg.boostMultiplier);
            offers.Show(new AdOffer
            {
                Placement = AdPlacement.IncomeBoost,
                Title = game.BoostActive ? "EXTEND BOOST" : "BOOST YOUR MINE",
                Body = "All cash " + mult + " for " + cfg.boostMinutesPerAd + " minutes, even while you're away.\n" +
                       "Stacks up to " + cfg.boostMaxHours + " hours.",
                Reward = mult + " CASH  ·  +" + cfg.boostMinutesPerAd + " MIN",
                OnRewarded = () =>
                {
                    game.AddBoost(cfg.boostMinutesPerAd * 60.0);
                    Punch.Play(view.transform, 0.3f, 0.4f);
                    if (toasts != null)
                        toasts.Show("BOOST ACTIVE!", mult + " cash for the next " + NumberFormat.Time(game.BoostSecondsLeft), Palette.Green);
                },
            });
        }
    }
}
