using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Bottom bar. The buttons themselves are wired in the Inspector (Auto Assign -> GameManager.AutoAssign,
    /// Skill Tree -> SkillTreeView.Open, Paragon -> ParagonView.Open); this script keeps the badges and the
    /// "x / y unlocked" line updated.
    /// </summary>
    public class BottomBar : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] RectTransform badge;
        [SerializeField] Text badgeText;
        [SerializeField] Text unlockedText;
        [SerializeField] float heartbeatSeconds = 1.6f;

        [Header("Paragon (optional)")]
        [Tooltip("Small badge on the Paragon button. Leave unassigned if the button doesn't need one.")]
        [SerializeField] RectTransform paragonBadge;
        [SerializeField] Text paragonBadgeText;

        float _timer, _pulseTimer;
        int _lastAffordable = -1;
        bool _lastCanAscend;

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = 0.4f;
                int affordable = 0;
                foreach (var n in game.Tree.Nodes)
                    if (game.Tree.IsAvailable(n) && game.CanAfford(n)) affordable++;

                if (affordable != _lastAffordable)
                {
                    if (affordable > _lastAffordable && _lastAffordable >= 0) Punch.Play(badge, 0.4f);
                    _lastAffordable = affordable;
                    badge.gameObject.SetActive(affordable > 0);
                    badgeText.text = affordable > 99 ? "99+" : affordable.ToString();
                }
                unlockedText.text = game.Tree.UnlockedCount + " / " + game.Tree.Nodes.Count + " unlocked";

                if (paragonBadge != null)
                {
                    bool canAscend = game.CanAscend;
                    if (canAscend && !_lastCanAscend) Punch.Play(paragonBadge, 0.4f);
                    _lastCanAscend = canAscend;
                    paragonBadge.gameObject.SetActive(canAscend);
                    if (canAscend && paragonBadgeText != null)
                    {
                        int available = game.AvailableParagonLevels;
                        paragonBadgeText.text = available > 99 ? "99+" : available.ToString();
                    }
                }
            }

            // Gentle heartbeat on the badge while something is affordable.
            if (_lastAffordable > 0)
            {
                _pulseTimer -= Time.unscaledDeltaTime;
                if (_pulseTimer <= 0f) { _pulseTimer = heartbeatSeconds; Punch.Play(badge, 0.18f, 0.35f); }
            }
        }
    }
}
