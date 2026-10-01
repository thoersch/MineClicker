using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Pooled "+$1.2K" floating numbers and ore chips that burst out of taps.
    /// The look comes from the FloatingText and OreChip prefabs; motion is tuned here.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FxLayer : MonoBehaviour
    {
        [SerializeField] Text floatingTextPrefab;
        [SerializeField] Image chipPrefab;
        [SerializeField] int maxFloaters = 48;
        [SerializeField] int maxChips = 96;
        [SerializeField] float chipGravity = 2600f;

        class Floater
        {
            public Text Text; public RectTransform Rt;
            public Vector2 Start; public float Age, Life, Rise; public bool Active;
        }

        class Chip
        {
            public Image Img; public RectTransform Rt;
            public Vector2 Pos, Vel; public float Age, Life, Spin; public bool Active;
        }

        readonly List<Floater> _floaters = new List<Floater>();
        readonly List<Chip> _chips = new List<Chip>();
        RectTransform _rt;
        int _nextFloater, _nextChip;

        void Awake() { _rt = (RectTransform)transform; }

        public Vector2 ScreenToLocal(Vector2 screen, Camera cam)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, cam, out local);
            return local;
        }

        public Vector2 WorldToLocal(Vector3 world) { return _rt.InverseTransformPoint(world); }

        public void SpawnText(Vector2 localPos, string s, Color color, int size = 46, float life = 0.9f, float rise = 170f)
        {
            Floater f = null;
            for (int i = 0; i < _floaters.Count; i++) if (!_floaters[i].Active) { f = _floaters[i]; break; }
            if (f == null)
            {
                if (_floaters.Count < maxFloaters)
                {
                    f = new Floater();
                    f.Text = Instantiate(floatingTextPrefab, _rt);
                    f.Rt = f.Text.rectTransform;
                    _floaters.Add(f);
                }
                else { f = _floaters[_nextFloater]; _nextFloater = (_nextFloater + 1) % _floaters.Count; }
            }
            f.Text.text = s;
            f.Text.color = color;
            f.Text.fontSize = size;
            f.Start = localPos;
            f.Age = 0; f.Life = life; f.Rise = rise; f.Active = true;
            f.Rt.anchoredPosition = localPos;
            f.Rt.SetAsLastSibling();
            f.Rt.gameObject.SetActive(true);
        }

        public void SpawnChips(Vector2 localPos, Color color, int count)
        {
            for (int n = 0; n < count; n++)
            {
                Chip c = null;
                for (int i = 0; i < _chips.Count; i++) if (!_chips[i].Active) { c = _chips[i]; break; }
                if (c == null)
                {
                    if (_chips.Count < maxChips)
                    {
                        c = new Chip();
                        c.Img = Instantiate(chipPrefab, _rt);
                        c.Rt = c.Img.rectTransform;
                        _chips.Add(c);
                    }
                    else { c = _chips[_nextChip]; _nextChip = (_nextChip + 1) % _chips.Count; }
                }
                float size = Random.Range(12f, 24f);
                c.Rt.sizeDelta = new Vector2(size, size);
                c.Img.color = Color.Lerp(color, Color.white, Random.Range(0f, 0.35f));
                c.Pos = localPos;
                float ang = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float spd = Random.Range(350f, 800f);
                c.Vel = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd);
                c.Spin = Random.Range(-720f, 720f);
                c.Age = 0; c.Life = Random.Range(0.45f, 0.75f); c.Active = true;
                c.Rt.anchoredPosition = localPos;
                c.Rt.gameObject.SetActive(true);
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _floaters.Count; i++)
            {
                var f = _floaters[i];
                if (!f.Active) continue;
                f.Age += dt;
                float t = f.Age / f.Life;
                if (t >= 1f) { f.Active = false; f.Rt.gameObject.SetActive(false); continue; }
                float ease = 1f - (1f - t) * (1f - t);
                f.Rt.anchoredPosition = f.Start + new Vector2(0, f.Rise * ease);
                float pop = t < 0.12f ? Mathf.Lerp(1.45f, 1f, t / 0.12f) : 1f;
                f.Rt.localScale = new Vector3(pop, pop, 1);
                var c = f.Text.color; c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f; f.Text.color = c;
            }

            for (int i = 0; i < _chips.Count; i++)
            {
                var c = _chips[i];
                if (!c.Active) continue;
                c.Age += dt;
                if (c.Age >= c.Life) { c.Active = false; c.Rt.gameObject.SetActive(false); continue; }
                c.Vel += new Vector2(0, -chipGravity) * dt;
                c.Pos += c.Vel * dt;
                c.Rt.anchoredPosition = c.Pos;
                c.Rt.localRotation = Quaternion.Euler(0, 0, c.Age * c.Spin);
                var col = c.Img.color; col.a = 1f - c.Age / c.Life; c.Img.color = col;
            }
        }
    }
}
