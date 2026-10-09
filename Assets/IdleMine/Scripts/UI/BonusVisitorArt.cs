using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// The look of BonusBubble's visitors, built in code from the sprites in Resources/Bonus.
    ///  * Ore cart: a riveted mine cart heaped with gold nuggets and gems, wheels that turn with its speed, a rattle
    ///    as it rolls, dust kicked up behind the wheels, glints dancing over the ore and a warm halo that says "tap me".
    ///  * Motherlode chest: a gold-strapped treasure chest that hops, with slow-turning light rays, a glow and glints.
    /// A dark name plate sits underneath. The bubble's own Image stays as the (invisible) tap target.
    /// </summary>
    public class BonusVisitorArt
    {
        const int Glints = 5, DustPool = 24;
        static readonly Vector2 CartSize = new Vector2(340f, 250f), ChestSize = new Vector2(280f, 243f);

        class Glint { public RectTransform Rt; public Image Img; public float T, Speed; }
        class Dust { public RectTransform Rt; public Image Img; public Vector2 Pos, Vel; public float Age, Life, Size; }

        readonly RectTransform _bubble, _area;
        readonly RectTransform _art, _halo, _rays, _cart, _wheelL, _wheelR, _chest, _plate;
        readonly Image _haloImg, _raysImg;
        readonly Text _label;
        readonly Glint[] _glints = new Glint[Glints];
        readonly Dust[] _dust = new Dust[DustPool];
        readonly Sprite _smoke;
        bool _isCart;
        float _dustTimer, _wheelAngle, _wheelY;
        Vector3 _lastWorld;

        static Sprite Load(string path) { return Resources.Load<Sprite>(path); }

        public BonusVisitorArt(RectTransform bubble, Image tapTarget, Text label, RectTransform area)
        {
            _bubble = bubble;
            _area = area;
            _label = label;
            // Keep the bubble as an invisible tap target; hide the old circle art.
            tapTarget.color = new Color(1f, 1f, 1f, 0f);
            var ring = bubble.Find("Ring");
            if (ring != null) ring.gameObject.SetActive(false);

            _art = Rect("Art", bubble, Vector2.zero);
            _art.SetAsFirstSibling();
            Sprite glow = Load("Fuse/FuseGlow");
            _haloImg = Img(_halo = Rect("Halo", _art, new Vector2(520f, 520f)), glow, Palette.Gold);
            _raysImg = Img(_rays = Rect("Rays", _art, new Vector2(560f, 560f)), Load("Bonus/Rays"), Palette.Gold);
            _cart = Rect("Cart", _art, CartSize);
            Img(_cart, Load("Bonus/Cart"), Color.white);
            Sprite wheel = Load("Bonus/CartWheel");
            // Axle positions measured on the cart sprite (340 x 250): x 98 / 242, y 215.
            float k = CartSize.x / 340f;
            _wheelY = (125f - 215f) * k;
            _wheelL = Rect("Wheel L", _cart, new Vector2(86f, 86f) * k);
            _wheelL.anchoredPosition = new Vector2((98f - 170f) * k, _wheelY);
            _wheelR = Rect("Wheel R", _cart, new Vector2(86f, 86f) * k);
            _wheelR.anchoredPosition = new Vector2((242f - 170f) * k, _wheelY);
            Img(_wheelL, wheel, Color.white);
            Img(_wheelR, wheel, Color.white);
            _chest = Rect("Chest", _art, ChestSize);
            Img(_chest, Load("Bonus/Chest"), Color.white);

            Sprite sparkle = Load("Bonus/Sparkle");
            for (int i = 0; i < Glints; i++)
            {
                var g = new Glint { Rt = Rect("Glint", _art, new Vector2(40f, 40f)) };
                g.Img = Img(g.Rt, sparkle, Color.white);
                g.T = i / (float)Glints;
                g.Speed = Random.Range(1.3f, 1.9f);
                _glints[i] = g;
            }

            // Name plate under the visitor.
            _plate = Rect("Plate", bubble, new Vector2(230f, 62f));
            var plate = Img(_plate, Load("Fuse/FusePlate"), Color.white);
            plate.type = Image.Type.Sliced;
            label.transform.SetParent(_plate, false);
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            label.fontSize = 30;
            label.lineSpacing = 1f;
            label.color = Palette.Gold;
            label.raycastTarget = false;
            if (label.GetComponent<Outline>() == null)
            {
                var o = label.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.2f, 0.06f, 0f, 1f);
                o.effectDistance = new Vector2(2f, -2f);
            }

            _smoke = Load("Fuse/FuseSmoke_0");
        }

        public void Show(bool cart)
        {
            _isCart = cart;
            _cart.gameObject.SetActive(cart);
            _chest.gameObject.SetActive(!cart);
            _rays.gameObject.SetActive(!cart);
            _bubble.sizeDelta = cart ? CartSize : ChestSize;
            _plate.anchoredPosition = new Vector2(0f, cart ? -CartSize.y * 0.5f - 22f : -ChestSize.y * 0.5f - 18f);
            _label.text = cart ? "ORE CART  <color=#FFFFFF>TAP!</color>" : "TAP TO OPEN";
            _plate.sizeDelta = new Vector2(cart ? 250f : 230f, 62f);
            _wheelAngle = 0f;
            _lastWorld = _bubble.position;
            for (int i = 0; i < Glints; i++) Place(_glints[i]);
        }

        public void Animate(float age, float dt)
        {
            float time = Time.unscaledTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
            _halo.localScale = Vector3.one * (0.9f + 0.12f * pulse);
            _haloImg.color = Palette.WithAlpha(Palette.Gold, 0.6f + 0.3f * pulse);

            if (_isCart)
            {
                // Wheels turn with how far the cart actually moved; the body rattles over the rails.
                Vector3 world = _bubble.position;
                float dx = _area.InverseTransformVector(world - _lastWorld).x;
                _lastWorld = world;
                _wheelAngle -= dx / (_wheelL.rect.width * 0.5f) * Mathf.Rad2Deg;
                _wheelL.localEulerAngles = new Vector3(0f, 0f, _wheelAngle);
                _wheelR.localEulerAngles = new Vector3(0f, 0f, _wheelAngle + 30f);
                float rattle = Mathf.Abs(Mathf.Sin(age * 11f)) * 5f + Mathf.Abs(Mathf.Sin(age * 23f)) * 2f;
                _cart.anchoredPosition = new Vector2(0f, rattle);
                _cart.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(age * 7f) * 2.2f - 1.5f);
                _wheelL.anchoredPosition = new Vector2(_wheelL.anchoredPosition.x, _wheelY - rattle);
                _wheelR.anchoredPosition = new Vector2(_wheelR.anchoredPosition.x, _wheelY - rattle);

                _dustTimer -= dt;
                if (_dustTimer <= 0f && dx > 0f)
                {
                    _dustTimer = 0.07f;
                    EmitDust(_wheelL);
                    EmitDust(_wheelR);
                }
            }
            else
            {
                // Hop every 1.8 s with a squash on landing.
                float t = Mathf.Repeat(age, 1.8f);
                float hop = t < 0.42f ? Mathf.Sin(t / 0.42f * Mathf.PI) * 30f : 0f;
                float land = t >= 0.42f && t < 0.62f ? 1f - (t - 0.42f) / 0.2f : 0f;
                _chest.anchoredPosition = new Vector2(0f, hop);
                _chest.localScale = new Vector3(1f + 0.1f * land, 1f - 0.1f * land, 1f);
                _chest.localEulerAngles = new Vector3(0f, 0f, t < 0.42f ? Mathf.Sin(t / 0.42f * Mathf.PI * 2f) * 4f : 0f);
                _rays.localEulerAngles = new Vector3(0f, 0f, time * 25f);
                _rays.localScale = Vector3.one * (0.95f + 0.08f * pulse);
                _raysImg.color = Palette.WithAlpha(Palette.Gold, 0.4f + 0.2f * pulse);
            }

            for (int i = 0; i < Glints; i++)
            {
                var g = _glints[i];
                g.T += dt * g.Speed;
                if (g.T >= 1f) { g.T -= 1f; Place(g); }
                float s = Mathf.Sin(g.T * Mathf.PI);
                g.Rt.localScale = Vector3.one * s * s;
                g.Rt.localEulerAngles = new Vector3(0f, 0f, g.T * 90f);
            }
            TickDust(dt);
        }

        /// <summary>Call while hidden too, so loose dust finishes.</summary>
        public void TickDust(float dt)
        {
            for (int i = 0; i < DustPool; i++)
            {
                var d = _dust[i];
                if (d == null || d.Life <= 0f) continue;
                d.Age += dt;
                float k = d.Age / d.Life;
                if (k >= 1f) { d.Life = 0f; d.Rt.gameObject.SetActive(false); continue; }
                d.Vel *= 1f - 2f * dt;
                d.Pos += d.Vel * dt;
                d.Rt.anchoredPosition = d.Pos;
                float s = d.Size * (0.5f + k);
                d.Rt.sizeDelta = new Vector2(s, s);
                d.Img.color = new Color(0.78f, 0.64f, 0.48f, Mathf.Sin(k * Mathf.PI) * 0.45f);
            }
        }

        void Place(Glint g)
        {
            // Over the ore heap on the cart, around the lid and lock on the chest (art-local coordinates).
            Vector2 at = _isCart ? new Vector2(Random.Range(-130f, 130f), Random.Range(42f, 104f))
                                 : new Vector2(Random.Range(-115f, 115f), Random.Range(-70f, 92f));
            g.Rt.anchoredPosition = at;
            float sz = Random.Range(26f, 46f);
            g.Rt.sizeDelta = new Vector2(sz, sz);
            g.Img.color = Random.value < 0.6f ? Color.white : new Color(1f, 0.92f, 0.6f);
        }

        void EmitDust(RectTransform wheel)
        {
            Dust d = null;
            for (int i = 0; i < DustPool; i++)
            {
                if (_dust[i] == null)
                {
                    var rt = Rect("Cart Dust", _area, Vector2.one * 10f);
                    rt.SetSiblingIndex(_bubble.GetSiblingIndex()); // behind the cart
                    _dust[i] = new Dust { Rt = rt, Img = Img(rt, _smoke, Color.clear) };
                }
                if (_dust[i].Life <= 0f) { d = _dust[i]; break; }
            }
            if (d == null) return;
            Vector3 bottom = wheel.TransformPoint(new Vector3(0f, -wheel.rect.height * 0.45f, 0f));
            d.Pos = (Vector2)_area.InverseTransformPoint(bottom) + new Vector2(Random.Range(-10f, 10f), 0f);
            d.Vel = new Vector2(Random.Range(-140f, -60f), Random.Range(20f, 70f));
            d.Age = 0f; d.Life = Random.Range(0.5f, 0.8f); d.Size = Random.Range(36f, 60f);
            d.Rt.anchoredPosition = d.Pos;
            d.Rt.gameObject.SetActive(true);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return rt;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }
    }
}
