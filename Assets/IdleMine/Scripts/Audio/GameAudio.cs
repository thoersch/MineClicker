using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// Turns game events into sounds, so the simulation and most views stay audio-free:
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
            }
            if (ads != null) ads.Rewarded -= OnRewarded;
        }

        void OnTapped(TapResult r)
        {
            if (!r.Auto) AudioManager.Play(r.Crit ? Sfx.Crit : Sfx.Dig);
        }

        void OnLayerUnlocked(MineLayer l)
        {
            AudioManager.Play(l.IsMotherlode ? Sfx.Motherlode : Sfx.Breakthrough);
        }

        void OnNodePurchased(SkillNode n)
        {
            AudioManager.Play(n.Kind == NodeKind.Keystone ? Sfx.Keystone : Sfx.Purchase);
        }

        void OnAscended(int levels) { AudioManager.Play(Sfx.Ascend); }

        void OnRewarded(AdPlacement p) { AudioManager.Play(Sfx.Reward); }
    }
}
