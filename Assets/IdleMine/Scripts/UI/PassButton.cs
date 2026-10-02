using UnityEngine;

namespace IdleMine
{
    /// <summary>
    /// Always-visible HUD entry to the Foreman Pass, so the purchase (and Apple's required Restore button)
    /// can be found without waiting for an ad offer. Hides once the pass is owned.
    /// The button child is wired in the Inspector to ForemanPassPopup.Open.
    /// </summary>
    public class PassButton : MonoBehaviour
    {
        [SerializeField] AdManager ads;
        [SerializeField] GameObject button;

        void Update()
        {
            bool show = !ads.HasForemanPass;
            if (button.activeSelf != show) button.SetActive(show);
        }
    }
}
