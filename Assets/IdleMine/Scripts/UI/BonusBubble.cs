using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Tappable bonuses that visit the mine:
    ///  * Ore Cart: rolls across every few minutes. Tap for a small free payout, or watch an ad for a big one.
    ///    It shows up even when no ad is available (then the free payout is paid straight away).
    ///  * Motherlode Chest: waits in the corner after you reach a Motherlode, as an ad-only reward.
    /// Lives on a non-blocking rect laid over the mine; the bubble child's Button is wired to Tapped().
    /// The cart timer pauses while the skill tree or a popup covers the mine, so it's never wasted.
    /// </summary>
    public class BonusBubble : MonoBehaviour
    {
        enum Kind { None, Cart, Chest }

        [SerializeField] GameManager game;
        [SerializeField] AdManager ads;
        [SerializeField] OfferPopup offers;
        [SerializeField] FxLayer fx;
        [SerializeField] SkillTreeView skillTree;
        [SerializeField] ParagonView paragon;

        [Header("Bubble")]
        [SerializeField] RectTransform bubble;
        [SerializeField] Image bubbleImage;
        [SerializeField] Text bubbleLabel;
        [SerializeField] float bobAmount = 14f;

        RectTransform _area;
        Kind _kind;
        float _age, _life, _nextCart, _y;

        void Start()
        {
            _area = (RectTransform)transform;
            bubble.gameObject.SetActive(false);
            ScheduleCart();
            game.LayerUnlocked += OnLayerUnlocked;
        }

        void OnDestroy()
        {
            if (game != null) game.LayerUnlocked -= OnLayerUnlocked;
        }

        void ScheduleCart()
        {
            _nextCart = Random.Range(game.Config.cartMinIntervalSeconds, game.Config.cartMaxIntervalSeconds);
        }

        void OnLayerUnlocked(MineLayer l)
        {
            if (l.IsMotherlode && ads.CanOffer(AdPlacement.MotherlodeChest)) Spawn(Kind.Chest);
        }

        bool Covered
        {
            get
            {
                return (skillTree != null && skillTree.IsOpen) || (paragon != null && paragon.IsOpen) || offers.IsOpen;
            }
        }

        void Spawn(Kind kind)
        {
            _kind = kind;
            _age = 0f;
            var cfg = game.Config;
            _life = kind == Kind.Cart ? cfg.cartLifetimeSeconds : cfg.chestLifetimeSeconds;
            _y = Random.Range(-0.25f, 0.25f) * _area.rect.height;
            bubbleLabel.text = kind == Kind.Cart ? "ORE\nCART" : "CHEST";
            bubbleImage.color = kind == Kind.Cart ? Palette.Gold : Palette.Orange;
            bubble.localScale = Vector3.one;
            bubble.localRotation = Quaternion.identity;
            bubble.SetAsLastSibling();
            bubble.gameObject.SetActive(true);
            Position();
            Punch.Play(bubble, 0.3f, 0.4f);
        }

        void Despawn()
        {
            if (_kind == Kind.Cart) ScheduleCart();
            _kind = Kind.None;
            bubble.gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_kind == Kind.None)
            {
                if (!Covered) _nextCart -= dt;
                if (_nextCart <= 0f) Spawn(Kind.Cart);
                return;
            }
            if (!Covered) _age += dt;
            if (_age >= _life) { Despawn(); return; }
            Position();
        }

        void Position()
        {
            Rect r = _area.rect;
            float bob = Mathf.Sin(_age * 5f) * bobAmount;
            if (_kind == Kind.Cart)
            {
                float margin = bubble.rect.width;
                float x = Mathf.Lerp(r.xMin - margin, r.xMax + margin, _age / _life);
                bubble.anchoredPosition = new Vector2(x, _y + bob);
                bubble.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_age * 9f) * 4f);
            }
            else
            {
                bubble.anchoredPosition = new Vector2(r.xMin + bubble.rect.width * 0.5f + 40f, r.yMin + bubble.rect.height * 0.5f + 140f + bob);
                // Fade out over the last few seconds so it doesn't just vanish.
                float fade = Mathf.Clamp01((_life - _age) / 4f);
                bubble.localScale = new Vector3(fade, fade, 1f);
            }
        }

        /// <summary>Inspector-wired to the bubble's Button.</summary>
        public void Tapped()
        {
            if (_kind == Kind.None) return;
            Vector2 at = fx.WorldToLocal(bubble.position);
            var cfg = game.Config;
            var kind = _kind;
            Despawn();

            if (kind == Kind.Cart)
            {
                double free = game.IncomeForMinutes(cfg.cartFreeMinutes);
                double big = game.IncomeForMinutes(cfg.cartAdMinutes);
                if (!ads.CanOffer(AdPlacement.OreCart)) { Pay(free, at); return; }
                offers.Show(new AdOffer
                {
                    Placement = AdPlacement.OreCart,
                    Title = "ORE CART!",
                    Body = "A cart loaded with ore rolled past.\nTake the cash, or watch an ad for a much bigger haul.",
                    Reward = NumberFormat.Money(big),
                    AdLabel = "WATCH AD",
                    AltLabel = "TAKE " + NumberFormat.Money(free),
                    OnAlt = () => Pay(free, at),
                    OnRewarded = () => Pay(big, at),
                });
            }
            else
            {
                double amount = game.IncomeForMinutes(cfg.chestAdMinutes);
                offers.Show(new AdOffer
                {
                    Placement = AdPlacement.MotherlodeChest,
                    Title = "MOTHERLODE CHEST",
                    Body = "Your miners struck a chest in the Motherlode.\nWatch an ad to crack it open.",
                    Reward = NumberFormat.Money(amount),
                    AdLabel = "OPEN IT",
                    OnRewarded = () => Pay(amount, at),
                });
            }
        }

        void Pay(double amount, Vector2 at)
        {
            game.GrantMoney(amount);
            fx.SpawnChips(at, Palette.Gold, 24);
            fx.SpawnText(at + new Vector2(0, 60), "+" + NumberFormat.Money(amount), Palette.Gold, 64, 1.5f, 220f);
        }
    }
}
