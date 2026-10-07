using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Overclock (Deep Core, Overdrive branch). A round gauge above GO DEEPEST fills as you play (taps fill it a
    /// little faster). When full it pulses gold: tap it for a burst of extra production. While overclocked, the
    /// gauge counts down and the screen edges glow. The button child is wired in the Inspector to Activate().
    /// </summary>
    public class OverclockButton : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] GameObject root;
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Image meter;
        [SerializeField] Text label;
        [SerializeField] Image edgeGlow;

        static readonly Color Overdrive = Palette.Hex("FF4F8B");
        float _pulse;

        void Update()
        {
            bool show = game.OverclockUnlocked || game.OverclockActive;
            if (root.activeSelf != show) root.SetActive(show);
            bool glow = game.OverclockActive;
            if (edgeGlow.gameObject.activeSelf != glow) edgeGlow.gameObject.SetActive(glow);
            if (!show) return;

            if (game.OverclockActive)
            {
                float total = Mathf.Max(1f, (float)game.Stats.Get(StatType.OverclockDuration));
                meter.fillAmount = game.OverclockSecondsLeft / total;
                meter.color = Palette.Text;
                background.color = Overdrive;
                label.text = "OVER\nCLOCK\n<size=24>" + Mathf.CeilToInt(game.OverclockSecondsLeft) + "s</size>";
                label.color = Palette.Text;
                button.interactable = false;
                edgeGlow.color = Palette.WithAlpha(Overdrive, 0.45f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
                return;
            }

            bool ready = game.OverclockMeter >= 1.0;
            meter.fillAmount = (float)game.OverclockMeter;
            meter.color = ready ? Palette.Gold : Overdrive;
            background.color = ready ? Palette.Gold : Palette.PanelLight;
            label.color = ready ? Palette.Panel : Palette.Text;
            label.text = ready ? "OVER\nCLOCK\n<size=24>TAP!</size>" : "OVER\nCLOCK\n<size=24>" + Mathf.FloorToInt((float)game.OverclockMeter * 100f) + "%</size>";
            button.interactable = ready;
            if (ready)
            {
                _pulse -= Time.unscaledDeltaTime;
                if (_pulse <= 0f) { _pulse = 1.2f; Punch.Play(button.transform, 0.12f, 0.35f); }
            }
        }

        /// <summary>Inspector-wired to the gauge button.</summary>
        public void Activate()
        {
            if (!game.ActivateOverclock()) return;
            Feedback.Play(Sfx.Overclock);
            Punch.Play(button.transform, 0.35f, 0.4f);
        }
    }
}
