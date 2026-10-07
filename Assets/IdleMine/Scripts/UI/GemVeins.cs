using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Gem veins (Deep Core, Gemcraft branch). Every so often a glowing gem pops out of the rock somewhere in the
    /// mine, glinting and bobbing. Tap it before it fades for GemValue minutes of income. Spawns pause while the
    /// skill trees or a popup cover the mine. The gem's Button is wired in the Inspector to Collect().
    /// </summary>
    public class GemVeins : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] FxLayer fx;
        [SerializeField] RectTransform gem;
        [SerializeField] RectTransform glint;
        [SerializeField] SkillTreeView skillTree;
        [SerializeField] SkillTreeView deepCore;
        [SerializeField] ParagonView paragon;
        [SerializeField] OfferPopup offers;

        static readonly Color Cyan = Palette.Hex("4DE1E8");
        RectTransform _area;
        float _next = -1f, _age;
        Vector2 _pos;

        void Start()
        {
            _area = (RectTransform)transform;
            gem.gameObject.SetActive(false);
        }

        bool Covered
        {
            get
            {
                return (skillTree != null && skillTree.IsOpen) || (deepCore != null && deepCore.IsOpen)
                       || (paragon != null && paragon.IsOpen) || (offers != null && offers.IsOpen);
            }
        }

        float Interval()
        {
            double rate = game.Stats.Get(StatType.GemRate);
            return rate <= 0 ? 999f : (float)(60.0 / rate) * Random.Range(0.7f, 1.3f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!game.GemsUnlocked) { if (gem.gameObject.activeSelf) gem.gameObject.SetActive(false); _next = -1f; return; }
            if (_next < 0f) _next = Interval();

            if (!gem.gameObject.activeSelf)
            {
                if (!Covered) _next -= dt;
                if (_next <= 0f) Spawn();
                return;
            }

            _age += dt;
            float life = game.Config.gemLifetimeSeconds;
            if (_age >= life) { gem.gameObject.SetActive(false); _next = Interval(); return; }
            float pop = Mathf.Clamp01(_age / 0.25f);
            float fade = Mathf.Clamp01((life - _age) / 1.5f);
            float s = (pop < 1f ? Mathf.Lerp(0.2f, 1.15f, pop) : 1f) * Mathf.Lerp(0.6f, 1f, fade);
            gem.localScale = new Vector3(s, s, 1f);
            gem.anchoredPosition = _pos + new Vector2(0, Mathf.Sin(_age * 3f) * 10f);
            glint.localRotation = Quaternion.Euler(0, 0, -_age * 90f);
            glint.localScale = Vector3.one * (0.9f + 0.2f * Mathf.Sin(_age * 7f));
        }

        /// <summary>Debug/testing: pop a gem now (only shows while gem veins are unlocked).</summary>
        public void ForceSpawn() { if (game.GemsUnlocked) Spawn(); }

        void Spawn()
        {
            Rect r = _area.rect;
            _pos = new Vector2(Random.Range(r.xMin + 120f, r.xMax - 120f), Random.Range(r.yMin + 220f, r.yMax - 160f));
            _age = 0f;
            gem.gameObject.SetActive(true);
            gem.SetAsLastSibling();
            Feedback.Play(Sfx.Gem, 0.5f);
        }

        /// <summary>Inspector-wired to the gem.</summary>
        public void Collect()
        {
            if (!gem.gameObject.activeSelf) return;
            Vector2 at = fx.WorldToLocal(gem.position);
            gem.gameObject.SetActive(false);
            _next = Interval();
            double amount = game.CollectGem();
            fx.SpawnChips(at, Cyan, 22);
            fx.SpawnChips(at, Palette.Text, 10);
            fx.SpawnText(at + new Vector2(0, 80), "GEM!  +" + NumberFormat.Money(amount), Cyan, 58, 1.4f, 220f);
            Feedback.Play(Sfx.Gem);
        }
    }
}
