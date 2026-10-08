using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// The dynamite fuse timer, drawn across the whole layer row being held so a thumb can't hide it.
    /// A braided fuse burns left to right toward a bundle of TNT: the rope chars to glowing ash behind a living
    /// flame, a spark flare showers streaking embers and smoke curls off the ash, heat glows along the burned
    /// length and around the row's edge, and a power readout sits on the side away from the finger. At full charge
    /// the flame roars, the TNT strains and glows, and the readout shouts RELEASE.
    /// Built in code from the sprites in Resources/Fuse the first time it's needed, then moved onto whichever row
    /// is lit. Particles live on the FX layer so they fly over neighbouring rows and finish after release.
    /// </summary>
    public class FuseBar
    {
        const float RopeY = 0.22f;                       // fuse height on the row, as a fraction from the bottom
        const float TrackLeft = 44f, TrackRight = 176f;  // insets; the TNT bundle sits past the right end
        const float RopeHeight = 36f;                    // one rope tile, displayed
        const float FlameFps = 22f;

        class Particle { public RectTransform Rt; public Image Img; public Vector2 Pos, Vel; public float Age, Life, Size, Spin; public bool Smoke; }

        public readonly RectTransform Root;
        readonly RectTransform _fxLayer, _track, _freshClip, _fresh, _ashClip, _ash, _head, _flame, _flameBack, _glow, _flare;
        readonly RectTransform _tnt, _tntGlow, _plate, _trail;
        readonly Image _heat, _trailImg, _ashImg, _flameImg, _flameBackImg, _glowImg, _flareImg, _tntGlowImg, _plateImg;
        readonly Text _number, _caption;
        readonly FuseGradient _numberGradient;
        readonly Sprite[] _flames, _smokes;
        readonly Sprite _spark;
        readonly List<Particle> _particles = new List<Particle>();
        float _charge, _punch, _emitSpark, _emitSmoke;
        bool _lit;

        static Sprite Load(string name) { return Resources.Load<Sprite>("Fuse/" + name); }

        public FuseBar(RectTransform fxLayer, Font font)
        {
            _fxLayer = fxLayer;
            _flames = new Sprite[6];
            for (int i = 0; i < _flames.Length; i++) _flames[i] = Load("FuseFlame_" + i);
            _smokes = new[] { Load("FuseSmoke_0"), Load("FuseSmoke_1"), Load("FuseSmoke_2") };
            _spark = Load("FuseSpark");
            Sprite glow = Load("FuseGlow");

            Root = Rect("TNT Fuse", null);
            Root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Warm inner glow hugging the row's rounded edge.
            _heat = Img(Rect("Heat", Root), Load("FuseHeat"), Palette.Orange);
            _heat.type = Image.Type.Sliced;

            _track = Rect("Track", Root);
            _track.offsetMin = new Vector2(TrackLeft, 0f);
            _track.offsetMax = new Vector2(-TrackRight, 0f);

            // Heat haze along the burned length, brightest at the flame.
            _trail = Rect("Heat Trail", _track);
            _trailImg = Img(_trail, Load("FuseTrail"), Color.white);

            // The rope: two clipped copies of one full-length fuse, so the braid stays put as it burns.
            _freshClip = Clip("Rope Clip", _track);
            _fresh = Rect("Rope", _freshClip);
            Tiled(Img(_fresh, Load("FuseRope"), Color.white));
            _ashClip = Clip("Ash Clip", _track);
            _ash = Rect("Ash", _ashClip);
            _ashImg = Img(_ash, Load("FuseAsh"), Color.white);
            Tiled(_ashImg);

            _tntGlow = Rect("TNT Glow", Root);
            _tntGlowImg = Img(_tntGlow, glow, Palette.Hex("FF4A1C"));
            _tnt = Rect("TNT", Root);
            Img(_tnt, Load("FuseTNT"), Color.white).preserveAspect = true;
            _tnt.anchorMin = _tnt.anchorMax = new Vector2(1f, RopeY);
            _tnt.pivot = new Vector2(4f / 300f, 1f - 100f / 210f);  // the bundle's own fuse tail meets the rope
            _tnt.sizeDelta = new Vector2(171f, 120f);
            _tntGlow.anchorMin = _tntGlow.anchorMax = new Vector2(1f, RopeY);

            _head = Rect("Spark", _track);
            _head.sizeDelta = Vector2.zero;
            _glowImg = Img(_glow = Centered("Glow", _head, 260f), glow, Color.white);
            _flameBackImg = Img(_flameBack = Rect("Flame Back", _head), _flames[3], new Color(1f, 0.8f, 0.7f, 0.85f));
            _flameImg = Img(_flame = Rect("Flame", _head), _flames[0], Color.white);
            foreach (var f in new[] { _flame, _flameBack })
            {
                f.anchorMin = f.anchorMax = new Vector2(0.5f, 0.5f);
                f.pivot = new Vector2(0.5f, 0.04f);
            }
            _flareImg = Img(_flare = Centered("Flare", _head, 230f), Load("FuseFlare"), new Color(1f, 0.95f, 0.8f, 1f));

            _plate = Rect("Readout", Root);
            _plate.sizeDelta = new Vector2(330f, 150f);
            _plateImg = Img(_plate, Load("FusePlate"), Color.white);
            _plateImg.type = Image.Type.Sliced;
            _number = Txt(Rect("Number", _plate), font, 76);
            _number.rectTransform.offsetMin = new Vector2(0f, 34f);
            _numberGradient = _number.gameObject.AddComponent<FuseGradient>();
            var outline = _number.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.04f, 0.01f, 1f);
            outline.effectDistance = new Vector2(3f, -3f);
            var shadow = _number.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -6f);
            _caption = Txt(Rect("Caption", _plate), font, 28);
            _caption.rectTransform.offsetMax = new Vector2(0f, -96f);
            _caption.color = Palette.Hex("FFC58A");

            Root.gameObject.SetActive(false);
        }

        /// <summary>World position of the burning point (where the blast goes off).</summary>
        public Vector3 HeadWorld { get { return _head.position; } }
        public bool Lit { get { return _lit; } }

        public void Attach(RectTransform row, bool fingerOnRight)
        {
            Root.SetParent(row, false);
            Root.anchorMin = Vector2.zero;
            Root.anchorMax = Vector2.one;
            Root.offsetMin = Root.offsetMax = Vector2.zero;
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            // The readout goes on the half of the row the finger isn't on.
            _plate.anchorMin = _plate.anchorMax = new Vector2(fingerOnRight ? 0.27f : 0.6f, 0.64f);
            _plate.anchoredPosition = Vector2.zero;
            _punch = 0f;
            _lit = true;
            Animate(0f, 0f);
        }

        public void Hide()
        {
            _lit = false;
            Root.gameObject.SetActive(false);
        }

        /// <summary>Kick the readout and flame when the fuse reaches full power.</summary>
        public void Punch() { _punch = 1f; }

        /// <summary>Call every frame, lit or not, so loose particles finish their flight.</summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                if (p.Life <= 0f) continue;
                p.Age += dt;
                float k = p.Age / p.Life;
                if (k >= 1f) { p.Life = 0f; p.Rt.gameObject.SetActive(false); continue; }
                if (p.Smoke)
                {
                    p.Vel *= 1f - 1.6f * dt;
                    p.Vel.y += 40f * dt;
                    p.Pos += p.Vel * dt;
                    p.Rt.anchoredPosition = p.Pos;
                    p.Rt.localEulerAngles = new Vector3(0f, 0f, p.Spin * p.Age);
                    float s = p.Size * (0.5f + 1.1f * k);
                    p.Rt.sizeDelta = new Vector2(s, s);
                    float a = Mathf.Sin(k * Mathf.PI) * 0.22f;
                    p.Img.color = new Color(0.62f, 0.56f, 0.52f, a);
                }
                else
                {
                    p.Vel.y -= 1300f * dt;
                    p.Vel *= 1f - 1.2f * dt;
                    p.Pos += p.Vel * dt;
                    p.Rt.anchoredPosition = p.Pos;
                    // Streaks point along their flight and stretch with speed.
                    float ang = Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg - 90f;
                    p.Rt.localEulerAngles = new Vector3(0f, 0f, ang);
                    float len = Mathf.Clamp(p.Vel.magnitude * 0.06f, 10f, 46f) * (1f - 0.5f * k);
                    p.Rt.sizeDelta = new Vector2(p.Size * (1f - 0.6f * k), len);
                    Color c = k < 0.3f ? Color.Lerp(Color.white, Palette.Gold, k / 0.3f) : Color.Lerp(Palette.Gold, Palette.Hex("FF5A1F"), (k - 0.3f) / 0.7f);
                    c.a = 1f - k * k;
                    p.Img.color = c;
                }
            }
        }

        public void Animate(float c, float dt)
        {
            float time = Time.unscaledTime;
            bool full = c >= 1f;
            _charge = c;
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(time * 17f, 0.37f);
            float strobe = full && Mathf.Repeat(time * 10f, 1f) < 0.5f ? 1f : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * (5f + 20f * c));
            _punch = Mathf.Max(0f, _punch - dt * 2.5f);
            float punch = _punch * _punch;

            // Row edge glow: builds with the charge, beats faster, flares gold when full.
            _heat.color = full ? Color.Lerp(Palette.Hex("FF5A1F"), Palette.Gold, strobe) * new Color(1f, 1f, 1f, 0.9f)
                               : Palette.WithAlpha(Palette.Hex("FF6A1F"), 0.25f + 0.45f * c + 0.15f * c * pulse);

            // Burned length, its ash and the heat haze over it. Each clip shows its slice of one fixed-length rope.
            float cc = Mathf.Clamp(c, 0.0001f, 0.9999f);
            Span(_trail, 0f, cc, RopeY, -78f, 122f);
            _trailImg.color = new Color(1f, 1f, 1f, (0.55f + 0.35f * c) * flicker);
            float trackW = _track.rect.width;
            Span(_ashClip, 0f, cc, RopeY, -RopeHeight * 0.5f, RopeHeight * 0.5f);
            Pin(_ash, 0f, trackW);
            Span(_freshClip, cc, 1f, RopeY, -RopeHeight * 0.5f, RopeHeight * 0.5f);
            Pin(_fresh, 1f, trackW);
            _ashImg.color = Color.Lerp(new Color(0.75f, 0.7f, 0.7f), Color.white, flicker);

            // The burning point.
            _head.anchorMin = _head.anchorMax = new Vector2(c, RopeY);
            _head.anchoredPosition = Vector2.zero;
            int frame = (int)(time * FlameFps);
            _flameImg.sprite = _flames[frame % _flames.Length];
            _flameBackImg.sprite = _flames[(frame + 3) % _flames.Length];
            float fs = (0.75f + 0.45f * c + (full ? 0.08f + 0.08f * strobe : 0f) + 0.4f * punch) * (0.92f + 0.1f * flicker);
            _flame.sizeDelta = new Vector2(120f, 180f) * fs;
            _flame.anchoredPosition = new Vector2(0f, -8f);
            _flameBack.sizeDelta = new Vector2(100f, 150f) * fs;
            _flameBack.anchoredPosition = new Vector2(-34f, -8f);
            _flameBack.localEulerAngles = new Vector3(0f, 0f, 12f);
            float g = (0.75f + 0.5f * c) * (0.85f + 0.3f * flicker) * (1f + 0.5f * punch);
            _glow.localScale = Vector3.one * g;
            _glowImg.color = new Color(1f, 0.62f, 0.28f, 0.45f + 0.3f * c);
            _flare.localEulerAngles = new Vector3(0f, 0f, time * 40f);
            _flare.localScale = Vector3.one * (0.7f + 0.35f * c + 0.25f * pulse * c + 0.6f * punch);
            _flareImg.color = new Color(1f, 0.95f, 0.8f, 0.6f + 0.4f * flicker);

            // TNT strains harder as the flame closes in, with a danger glow behind it.
            float shake = 1f + 7f * c * c + (full ? 5f : 0f);
            Vector2 tntAt = new Vector2(-TrackRight + 4f, 0f);
            _tnt.anchoredPosition = tntAt + Random.insideUnitCircle * shake;
            _tnt.localEulerAngles = new Vector3(0f, 0f, Random.Range(-1f, 1f) * shake * 0.35f);
            _tnt.localScale = Vector3.one * (1f + 0.05f * c * pulse + (full ? 0.06f * strobe : 0f));
            _tntGlow.anchoredPosition = tntAt + new Vector2(86f, 0f);
            float tg = 200f + 160f * c + (full ? 60f * strobe : 0f);
            _tntGlow.sizeDelta = new Vector2(tg * 1.3f, tg);
            _tntGlowImg.color = Palette.WithAlpha(full ? Color.Lerp(Palette.Hex("FF3A1A"), Palette.Gold, strobe) : Palette.Hex("FF4A1C"), 0.15f + 0.55f * c * (0.7f + 0.3f * pulse));

            // Readout.
            _plate.localScale = Vector3.one * (1f + 0.04f * c * pulse + 0.35f * punch);
            _plateImg.color = full ? Color.Lerp(Color.white, new Color(1f, 0.85f, 0.6f), strobe) : Color.white;
            if (full)
            {
                _number.text = "RELEASE!";
                _number.fontSize = 66;
                _caption.text = "MAX POWER";
                _caption.color = strobe > 0f ? Color.white : Palette.Gold;
                _numberGradient.Set(Color.white, strobe > 0f ? Palette.Gold : Palette.Hex("FF8A2A"));
            }
            else
            {
                _number.text = Mathf.FloorToInt(c * 100f) + "%";
                _number.fontSize = 76;
                _caption.text = "BLAST POWER";
                _caption.color = Palette.Hex("FFC58A");
                _numberGradient.Set(Palette.Hex("FFF4C8"), Color.Lerp(Palette.Gold, Palette.Hex("FF6A1F"), c));
            }

            if (dt > 0f) Emit(dt, full);
        }

        void Emit(float dt, bool full)
        {
            Vector2 head = _fxLayer.InverseTransformPoint(_head.position);
            _emitSpark += dt * (full ? 110f : 45f + 45f * _charge);
            while (_emitSpark >= 1f)
            {
                _emitSpark -= 1f;
                float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float speed = Random.Range(320f, 760f) * (full ? 1.3f : 1f);
                Spawn(false, head + Random.insideUnitCircle * 8f, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed,
                      Random.Range(0.35f, 0.65f), Random.Range(7f, 12f));
            }
            _emitSmoke += dt * (6f + 4f * _charge);
            while (_emitSmoke >= 1f)
            {
                _emitSmoke -= 1f;
                Vector2 at = head + new Vector2(-Random.Range(20f, 90f), Random.Range(0f, 14f));
                Spawn(true, at, new Vector2(Random.Range(-30f, 10f), Random.Range(50f, 110f)), Random.Range(0.9f, 1.4f), Random.Range(36f, 64f));
            }
        }

        void Spawn(bool smoke, Vector2 pos, Vector2 vel, float life, float size)
        {
            Particle p = null;
            for (int i = 0; i < _particles.Count; i++) if (_particles[i].Life <= 0f) { p = _particles[i]; break; }
            if (p == null)
            {
                if (_particles.Count >= 160) return;
                p = new Particle();
                p.Rt = Centered("Fuse Particle", _fxLayer, 10f);
                p.Img = Img(p.Rt, null, Color.white);
                _particles.Add(p);
            }
            p.Smoke = smoke;
            p.Img.sprite = smoke ? _smokes[Random.Range(0, _smokes.Length)] : _spark;
            p.Pos = pos; p.Vel = vel; p.Age = 0f; p.Life = life; p.Size = size;
            p.Spin = Random.Range(-60f, 60f);
            p.Rt.anchoredPosition = pos;
            p.Rt.gameObject.SetActive(true);
            p.Rt.SetAsLastSibling();
        }

        // Horizontal span between anchors x0..x1 at vertical anchor y, with vertical offsets bottom..top.
        static void Span(RectTransform rt, float x0, float x1, float y, float bottom, float top)
        {
            rt.anchorMin = new Vector2(x0, y);
            rt.anchorMax = new Vector2(x1, y);
            rt.offsetMin = new Vector2(0f, bottom);
            rt.offsetMax = new Vector2(0f, top);
        }

        // Full track length, pinned to the clip's left (0) or right (1) edge, which is also the track's.
        static void Pin(RectTransform rt, float side, float width)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(side, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, RopeHeight);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Centered(string name, Transform parent, float size)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        static RectTransform Clip(string name, Transform parent)
        {
            var rt = Rect(name, parent);
            rt.gameObject.AddComponent<RectMask2D>();
            return rt;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // Tile the rope sprite along its length at the rope's display height.
        static void Tiled(Image img)
        {
            img.type = Image.Type.Tiled;
            if (img.sprite != null) img.pixelsPerUnitMultiplier = img.sprite.rect.height / RopeHeight;
        }

        static Text Txt(RectTransform rt, Font font, int size)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }

    /// <summary>Top-to-bottom colour ramp for UI text (multiplies the text colour).</summary>
    public class FuseGradient : BaseMeshEffect
    {
        Color _top = Color.white, _bottom = Color.white;
        readonly List<UIVertex> _verts = new List<UIVertex>();

        public void Set(Color top, Color bottom)
        {
            if (top == _top && bottom == _bottom) return;
            _top = top; _bottom = bottom;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            _verts.Clear();
            vh.GetUIVertexStream(_verts);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < _verts.Count; i++) { float y = _verts[i].position.y; if (y < min) min = y; if (y > max) max = y; }
            float h = Mathf.Max(1f, max - min);
            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                v.color = (Color32)((Color)v.color * Color.Lerp(_bottom, _top, (v.position.y - min) / h));
                _verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }
    }
}
