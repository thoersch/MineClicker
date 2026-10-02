using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// The one-time Foreman Pass purchase: every ad reward becomes instant, plus more offline earnings.
    /// Its GameObject starts inactive in the scene. Buttons are wired in the Inspector:
    /// Close -> Close, Buy -> Buy, Restore -> Restore (Apple requires a visible restore option).
    /// </summary>
    public class ForemanPassPopup : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] AdManager ads;
        [SerializeField] RectTransform card;
        [SerializeField] Text perksText;
        [SerializeField] Text statusText;
        [SerializeField] Button buyButton;
        [SerializeField] Image buyButtonImage;
        [SerializeField] Text buyLabel;
        [SerializeField] GameObject restoreButton;

        bool _busy;

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            statusText.text = "";
            perksText.text =
                "Skip every ad: rewards are instant\n" +
                "+" + NumberFormat.Percent(game.Config.foremanOfflineBonus) + " offline earnings, forever";
            Punch.Play(card, 0.15f, 0.3f);
            Refresh();
        }

        public void Close() { gameObject.SetActive(false); }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; } // Android back button
#endif
            Refresh();
        }

        void Refresh()
        {
            bool owned = ads.HasForemanPass;
            string price = ads.ForemanPassPrice;
            restoreButton.SetActive(!owned);
            if (owned)
            {
                SetBuy("ACTIVE\n<size=30>Thank you!</size>", Palette.PanelLight, Palette.Green, false);
            }
            else if (_busy || price == null)
            {
                SetBuy(_busy ? "..." : "STORE\n<size=30>connecting</size>", Palette.PanelLight, Palette.TextDim, false);
            }
            else
            {
                SetBuy("GET IT\n<size=34>" + price + "</size>", Palette.Gold, Palette.Panel, true);
            }
        }

        void SetBuy(string label, Color bg, Color fg, bool interactable)
        {
            if (buyLabel.text != label) buyLabel.text = label;
            buyLabel.color = fg;
            buyButtonImage.color = bg;
            buyButton.interactable = interactable;
        }

        /// <summary>Inspector-wired to the price button.</summary>
        public void Buy()
        {
            if (_busy || ads.HasForemanPass) return;
            _busy = true;
            statusText.text = "";
            ads.BuyForemanPass(ok =>
            {
                _busy = false;
                statusText.text = ok ? "" : "Purchase didn't go through. You haven't been charged.";
                if (ok) { Punch.Play(card, 0.2f, 0.4f); AudioManager.Play(Sfx.Reward); }
            });
        }

        /// <summary>Inspector-wired to "Restore purchases".</summary>
        public void Restore()
        {
            if (_busy) return;
            _busy = true;
            statusText.text = "Restoring...";
            ads.RestorePurchases(ok =>
            {
                _busy = false;
                statusText.text = ads.HasForemanPass ? "Foreman Pass restored." :
                                  ok ? "No previous purchase found on this account." : "Couldn't reach the store. Try again later.";
            });
        }
    }
}
