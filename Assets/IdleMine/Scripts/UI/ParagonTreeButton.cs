using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// The skill tree's "PARAGON TREE" button: shows unspent Paragon Points and gives a gentle heartbeat while
    /// there's something to spend. Hidden until the first ascension, when the Paragon tree first matters.
    /// The button is wired in the Inspector to ParagonTreeView.FlipIn.
    /// </summary>
    public class ParagonTreeButton : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] GameObject button;
        [SerializeField] Text label;
        [SerializeField] float heartbeatSeconds = 2f;

        float _timer, _pulse;

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.25f;

            bool show = game.ParagonLevel > 0;
            if (button.activeSelf != show) button.SetActive(show);
            if (!show) return;

            int pts = game.ParagonPointsAvailable;
            label.text = "PARAGON TREE\n<size=26>" + (pts > 0 ? pts + (pts == 1 ? " point" : " points") + " to spend" : "all points spent") + "</size>";
            if (pts > 0)
            {
                _pulse -= 0.25f;
                if (_pulse <= 0f) { _pulse = heartbeatSeconds; Punch.Play(button.transform, 0.1f, 0.35f); }
            }
        }
    }
}
