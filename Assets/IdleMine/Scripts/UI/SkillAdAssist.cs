using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// "Almost there" helper in the skill tree's details sheet. When the selected skill is unlocked-for-sale
    /// but you're a little short (at least skillAssistThreshold of its cost), a WATCH AD button sits over the
    /// greyed-out BUY button: the ad covers the difference and buys the skill. Has its own cooldown so it
    /// speeds things up without replacing the economy. The button is wired in the Inspector to Watch().
    /// </summary>
    public class SkillAdAssist : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] AdManager ads;
        [SerializeField] SkillTreeView tree;
        [SerializeField] AdButtonView view;

        bool _waiting;
        float _timer;

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.2f;

            var n = tree.Selected;
            bool show = _waiting || (n != null && Eligible(n) && ads.CanOffer(AdPlacement.SkillAssist));
            view.Visible = show;
            if (!show || n == null) return;
            view.Button.interactable = !_waiting;
            view.Set("GET IT NOW\n<size=30>" + NumberFormat.Money(game.GetCost(n)) + "</size>", !ads.HasForemanPass);
        }

        bool Eligible(SkillNode n)
        {
            if (!game.Tree.IsAvailable(n) || game.CanAfford(n)) return false;
            return game.Money >= game.GetCost(n) * game.Config.skillAssistThreshold;
        }

        /// <summary>Inspector-wired to the button.</summary>
        public void Watch()
        {
            var n = tree.Selected;
            if (_waiting || n == null || !Eligible(n)) return;
            _waiting = true;
            ads.ShowRewarded(AdPlacement.SkillAssist, ok =>
            {
                _waiting = false;
                _timer = 0f;
                if (!ok || tree.Selected != n || !game.Tree.IsAvailable(n)) return;
                game.GrantMoney(game.GetCost(n) - game.Money);
                ads.StartCooldown(AdPlacement.SkillAssist, game.Config.skillAssistCooldownMinutes * 60.0);
                tree.Buy();
            });
        }
    }
}
