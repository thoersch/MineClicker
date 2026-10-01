using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>Banner that drops in when you break through to a new depth.</summary>
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
            titleText.text = l.IsMotherlode ? "MOTHERLODE AT DEPTH " + l.Index + "!" : "DEPTH " + l.Index + " REACHED!";
            subtitleText.text = LayerCatalog.BandName(l.Index) + "  \u00B7  " + LayerCatalog.OreName(l.Index) + " ore  \u00B7  " + NumberFormat.Money(l.ValuePerOre) + " each";
            bannerImage.color = Color.Lerp(Palette.Layer(l.Index), Palette.Panel, 0.35f);
            banner.gameObject.SetActive(true);
            _t = 0f;
            Punch.Play(titleText.transform, 0.2f, 0.35f);
        }

        void OnAscended(int levelsGained)
        {
            titleText.text = "PARAGON " + game.ParagonLevel + "!";
            subtitleText.text = "Mine reset  \u00B7  now " + NumberFormat.Multiplier(game.ParagonMultiplier) + " stronger, permanently";
            bannerImage.color = Color.Lerp(Palette.Gold, Palette.Panel, 0.35f);
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
