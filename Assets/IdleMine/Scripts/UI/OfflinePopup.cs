using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// "Welcome back" modal. GameManager has already granted the cash; this just makes it feel like a reward.
    /// Watches for pending reports, so it also appears after the app resumes from the background.
    /// The COLLECT button is wired to Collect() in the Inspector. A natural spot for a "watch ad for x2" button.
    /// </summary>
    public class OfflinePopup : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] CanvasGroup dimGroup;   // the full-screen dim; starts inactive
        [SerializeField] RectTransform card;
        [SerializeField] Text awayText;
        [SerializeField] Text moneyText;
        [SerializeField] Text layersText;

        float _t = -1f;
        bool _closing;

        void Update()
        {
            if (!dimGroup.gameObject.activeSelf)
            {
                var r = game.PendingOfflineReport;
                if (r != null)
                {
                    game.PendingOfflineReport = null;
                    if (r.Money > 0) Show(r);
                }
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (_closing)
            {
                dimGroup.alpha -= dt / 0.2f;
                if (dimGroup.alpha <= 0f) dimGroup.gameObject.SetActive(false);
                return;
            }
            if (_t < 0f) return;
            _t += dt;
            float x = Mathf.Clamp01(_t / 0.35f);
            float s = 1f + 2.2f * Mathf.Pow(x - 1f, 3f) + 1.2f * Mathf.Pow(x - 1f, 2f); // ease-out-back
            card.localScale = new Vector3(s, s, 1f);
            dimGroup.alpha = Mathf.Clamp01(_t / 0.2f);
            if (x >= 1f) _t = -1f;
        }

        void Show(OfflineReport r)
        {
            string away = "You were away for " + NumberFormat.Time(r.AwaySeconds);
            if (r.SimulatedSeconds < r.AwaySeconds - 1)
                away += "\n<size=26>(offline cap " + NumberFormat.Time(r.SimulatedSeconds) + ": raise it in Logistics)</size>";
            awayText.text = away;
            moneyText.text = NumberFormat.Money(r.Money);
            layersText.gameObject.SetActive(r.LayersGained > 0);
            layersText.text = "and dug " + r.LayersGained + " layer" + (r.LayersGained == 1 ? "" : "s") + " deeper";

            transform.SetAsLastSibling();
            dimGroup.gameObject.SetActive(true);
            dimGroup.alpha = 0f;
            card.localScale = Vector3.zero;
            _closing = false;
            _t = 0f;
        }

        public void Collect() { _closing = true; }
    }
}
