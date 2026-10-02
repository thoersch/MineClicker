using System;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>What an OfferPopup shows: a reward for watching an ad, and optionally a free alternative.</summary>
    public class AdOffer
    {
        public AdPlacement Placement;
        public string Title;
        public string Body;
        /// <summary>Big gold line, e.g. "$12.4K" or "x2 CASH  30 MIN".</summary>
        public string Reward;
        public string AdLabel = "WATCH AD";
        /// <summary>Called after the ad was watched (or skipped by the Foreman Pass).</summary>
        public Action OnRewarded;
        /// <summary>Secondary button, e.g. "TAKE $1.2K". Null means it reads NO THANKS and just closes.</summary>
        public string AltLabel;
        public Action OnAlt;
    }

    /// <summary>
    /// Modal that asks before any ad plays: what you get, a WATCH AD button, and a way out. Used by the
    /// income boost, the ore cart and the motherlode chest. Its GameObject starts inactive in the scene.
    /// Buttons are wired in the Inspector: Watch -> Watch, Alt -> Alt, Pass link -> OpenPass.
    /// </summary>
    public class OfferPopup : MonoBehaviour
    {
        [SerializeField] AdManager ads;
        [SerializeField] RectTransform card;
        [SerializeField] Text titleText;
        [SerializeField] Text bodyText;
        [SerializeField] Text rewardText;
        [SerializeField] Text statusText;
        [SerializeField] AdButtonView watchButton;
        [SerializeField] Text altLabel;
        [Tooltip("'No ads? Foreman Pass' link. Hidden once the pass is owned.")]
        [SerializeField] GameObject passLink;
        [SerializeField] ForemanPassPopup passPopup;

        AdOffer _offer;
        bool _waiting;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        public void Show(AdOffer offer)
        {
            _offer = offer;
            _waiting = false;
            titleText.text = offer.Title;
            bodyText.text = offer.Body;
            rewardText.text = offer.Reward;
            altLabel.text = offer.AltLabel ?? "NO THANKS";
            statusText.text = "";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Punch.Play(card, 0.15f, 0.3f);
            Refresh();
        }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) { Alt(); return; } // Android back button
#endif
            Refresh();
        }

        void Refresh()
        {
            if (_offer == null) return;
            bool pass = ads.HasForemanPass;
            watchButton.Set(pass ? "CLAIM" : _offer.AdLabel, !pass);
            watchButton.Button.interactable = !_waiting && ads.CanOffer(_offer.Placement);
            if (passLink != null && passLink.activeSelf == pass) passLink.SetActive(!pass);
        }

        /// <summary>Inspector-wired to the WATCH AD button.</summary>
        public void Watch()
        {
            if (_offer == null || _waiting) return;
            _waiting = true;
            statusText.text = "";
            var offer = _offer;
            ads.ShowRewarded(offer.Placement, ok =>
            {
                _waiting = false;
                if (!ok)
                {
                    statusText.text = "No reward this time. You can try again in a moment.";
                    return;
                }
                Hide();
                if (offer.OnRewarded != null) offer.OnRewarded();
            });
        }

        /// <summary>Inspector-wired to the secondary button (NO THANKS / TAKE $X).</summary>
        public void Alt()
        {
            if (_offer == null || _waiting) return;
            var offer = _offer;
            Hide();
            if (offer.OnAlt != null) offer.OnAlt();
        }

        /// <summary>Inspector-wired to the "No ads? Get the Foreman Pass" link.</summary>
        public void OpenPass()
        {
            if (passPopup != null) passPopup.Open();
        }

        void Hide()
        {
            _offer = null;
            gameObject.SetActive(false);
        }
    }
}
