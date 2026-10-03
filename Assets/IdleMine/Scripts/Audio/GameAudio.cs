using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// Turns game events into sounds and haptics (via Feedback), so the simulation and most views stay
    /// feedback-free:
    /// taps dig (crits clang), new layers rumble open (Motherlodes shimmer), skills pop (keystones chime),
    /// ascending swells, and every ad or Foreman Pass reward sparkles. Auto-taps stay silent.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [Tooltip("Optional: plays the reward sparkle when an ad reward is granted.")]
        [SerializeField] AdManager ads;

        void Start()
        {
            game.Tapped += OnTapped;
            game.LayerUnlocked += OnLayerUnlocked;
            game.NodePurchased += OnNodePurchased;
            game.Ascended += OnAscended;
            game.PerkPurchased += OnPerkPurchased;
            if (ads != null) ads.Rewarded += OnRewarded;
        }

        void OnDestroy()
        {
            if (game != null)
            {
                game.Tapped -= OnTapped;
                game.LayerUnlocked -= OnLayerUnlocked;
                game.NodePurchased -= OnNodePurchased;
                game.Ascended -= OnAscended;
                game.PerkPurchased -= OnPerkPurchased;
            }
            if (ads != null) ads.Rewarded -= OnRewarded;
        }

        void OnTapped(TapResult r)
        {
            if (!r.Auto) Feedback.Play(r.Crit ? Sfx.Crit : Sfx.Dig);
        }

        void OnLayerUnlocked(MineLayer l)
        {
            Feedback.Play(l.IsMotherlode ? Sfx.Motherlode : Sfx.Breakthrough);
        }

        void OnNodePurchased(SkillNode n)
        {
            Feedback.Play(n.Kind == NodeKind.Keystone ? Sfx.Keystone : Sfx.Purchase);
        }

        void OnAscended(int levels) { } // the full-screen AscendCelebration owns this moment's sound and haptics

        void OnPerkPurchased(ParagonPerk p) { Feedback.Play(Sfx.Keystone); }

        void OnRewarded(AdPlacement p) { Feedback.Play(Sfx.Reward); }
    }
}
