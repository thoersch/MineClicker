using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>Banner that drops in when you break through to a new depth. Other views can raise their
    /// own messages through Show.</summary>
    public class Toasts : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] RectTransform banner;
        [SerializeField] Image bannerImage;
        [SerializeField] CanvasGroup bannerGroup;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] float holdSeconds = 2.4f;
        [SerializeField] float slideDistance = 160f;

        float _t = -1f;

        void Start()
        {
            game.LayerUnlocked += OnLayerUnlocked;
            game.Ascended += OnAscended;
            banner.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.LayerUnlocked -= OnLayerUnlocked;
            game.Ascended -= OnAscended;
        }

        void OnLayerUnlocked(MineLayer l)
        {
            Show(l.IsMotherlode ? "MOTHERLODE AT DEPTH " + l.Index + "!" : "DEPTH " + l.Index + " REACHED!",
                 LayerCatalog.BandName(l.Index) + "  \u00B7  " + LayerCatalog.OreName(l.Index) + " ore  \u00B7  " + NumberFormat.Money(l.ValuePerOre) + " each",
                 Palette.Layer(l.Index));
        }

        void OnAscended(int levelsGained)
        {
            Show("PARAGON " + game.ParagonLevel + "!",
                 "Mine reset  \u00B7  now " + NumberFormat.Multiplier(game.ParagonMultiplier) + " stronger, permanently",
                 Palette.Gold);
        }

        /// <summary>Drops the banner in with any message (also used for ad rewards and purchase results).</summary>
        public void Show(string title, string subtitle, Color tint)
        {
            titleText.text = title;
            subtitleText.text = subtitle;
            bannerImage.color = Color.Lerp(tint, Palette.Panel, 0.35f);
            banner.gameObject.SetActive(true);
            _t = 0f;
            Punch.Play(titleText.transform, 0.2f, 0.35f);
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(_t / 0.25f);
            float back = 1f + 2.2f * Mathf.Pow(x - 1f, 3f) + 1.2f * Mathf.Pow(x - 1f, 2f); // ease-out-back
            banner.anchoredPosition = new Vector2(banner.anchoredPosition.x, (1f - back) * slideDistance);
            bannerGroup.alpha = _t < holdSeconds ? 1f : 1f - (_t - holdSeconds) / 0.4f;
            if (_t > holdSeconds + 0.4f) { _t = -1f; banner.gameObject.SetActive(false); }
        }
    }
}
