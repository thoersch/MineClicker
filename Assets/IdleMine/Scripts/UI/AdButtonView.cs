using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// A button that pays out via a rewarded ad. Shows a small "AD" tag so players always know before
    /// tapping that a video will play; the tag disappears for Foreman Pass owners, who get the reward
    /// straight away.
    /// </summary>
    public class AdButtonView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Text label;
        [Tooltip("The little 'AD' marker in the corner.")]
        [SerializeField] GameObject adTag;

        public Button Button { get { return button; } }

        public void Set(string text, bool showAdTag)
        {
            if (label.text != text) label.text = text;
            if (adTag != null && adTag.activeSelf != showAdTag) adTag.SetActive(showAdTag);
        }

        public bool Visible
        {
            get { return gameObject.activeSelf; }
            set { if (gameObject.activeSelf != value) gameObject.SetActive(value); }
        }
    }
}
