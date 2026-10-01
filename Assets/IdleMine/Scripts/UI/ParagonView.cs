using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Paragon modal: dim background + centered card, opened the same way as the Skill Tree. Shows the
    /// permanent multiplier earned from past ascensions and how close lifetime earnings are to the next
    /// one. Ascending cashes in every level currently available, wipes money/miners/the skill tree back
    /// to a fresh start, and keeps (and grows) the multiplier.
    ///
    /// The panel's GameObject starts inactive in the scene. Buttons are wired in the Inspector:
    /// Close -> Close, Ascend -> Ascend. Ascend arms on the first tap and only fires on a second tap
    /// within armSeconds, since resetting the run can't be undone.
    /// </summary>
    public class ParagonView : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] RectTransform card;

        [Header("Readout")]
        [SerializeField] Text levelText;
        [SerializeField] Text multiplierText;
        [SerializeField] Text lifetimeText;
        [SerializeField] Text nextLevelText;
        [Tooltip("Its RectTransform's anchorMax.x is driven directly, same trick as the layer breakthrough bar.")]
        [SerializeField] Image progressFill;

        [Header("Ascend button")]
        [SerializeField] Button ascendButton;
        [SerializeField] Image ascendButtonImage;
        [SerializeField] Text ascendLabel;
        [SerializeField] float armSeconds = 3f;

        bool _armed;
        float _armTimer, _refreshTimer;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        public void Open()
        {
            if (IsOpen) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _armed = false;
            Punch.Play(card, 0.15f, 0.3f);
            Refresh();
        }

        public void Close() { gameObject.SetActive(false); _armed = false; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_armed)
            {
                _armTimer -= dt;
                if (_armTimer <= 0f) { _armed = false; Refresh(); }
            }

            _refreshTimer -= dt;
            if (_refreshTimer <= 0f) { _refreshTimer = 0.2f; Refresh(); }
        }

        void Refresh()
        {
            int level = game.ParagonLevel;
            double lifetime = game.LifetimeMoney;
            double thisLevel = game.ParagonRequirement(level);
            double nextLevel = game.ParagonRequirement(level + 1);
            int available = game.AvailableParagonLevels;

            levelText.text = "PARAGON " + level;
            multiplierText.text = NumberFormat.Multiplier(game.ParagonMultiplier) + " Ore Value, Miner Speed, Tap Power & Dig Speed";
            lifetimeText.text = "Lifetime earnings: " + NumberFormat.Money(lifetime);

            double span = System.Math.Max(1e-9, nextLevel - thisLevel);
            float frac = Mathf.Clamp01((float)((lifetime - thisLevel) / span));
            var fill = progressFill.rectTransform;
            if (Mathf.Abs(fill.anchorMax.x - frac) > 0.0005f) fill.anchorMax = new Vector2(frac, 1);

            nextLevelText.text = available > 0
                ? "+" + available + " level" + (available == 1 ? "" : "s") + " ready to claim"
                : "Next level at " + NumberFormat.Money(nextLevel);

            bool canAscend = game.CanAscend;
            if (!canAscend) _armed = false;
            ascendButton.interactable = canAscend;

            if (!canAscend)
                SetAscend("ASCEND", Palette.PanelLight, Palette.TextDim);
            else if (_armed)
                SetAscend("RESET THE MINE\n<size=26>TAP AGAIN TO CONFIRM</size>", Palette.Red, Palette.Text);
            else
                SetAscend("ASCEND\n<size=26>+" + available + " Paragon level" + (available == 1 ? "" : "s") + "</size>", Palette.Gold, Palette.Panel);
        }

        void SetAscend(string label, Color bg, Color fg)
        {
            ascendLabel.text = label;
            ascendLabel.color = fg;
            ascendButtonImage.color = bg;
        }

        /// <summary>Inspector-wired to the Ascend button. First tap arms it, a second tap within
        /// armSeconds confirms - this wipes the mine and skill tree and can't be undone.</summary>
        public void Ascend()
        {
            if (!game.CanAscend) return;
            if (!_armed)
            {
                _armed = true;
                _armTimer = armSeconds;
                Punch.Play(ascendButton.transform, 0.15f, 0.25f);
                Refresh();
                return;
            }
            _armed = false;
            game.Ascend();
            Punch.Play(ascendButton.transform, 0.4f, 0.4f);
            Refresh();
        }
    }
}
