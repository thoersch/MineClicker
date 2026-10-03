using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// The Paragon tree: the flip side of the skill tree. Deep purple and gold, diamond nodes, and Paragon
    /// Points instead of cash. Perks unlock at rising Paragon levels, need the perk above them, and last for
    /// this run only (ascending wipes them and refills the points).
    ///
    /// Its GameObject starts inactive in the scene and lays out on top of the skill tree. The skill tree's
    /// PARAGON TREE button calls FlipIn (the panels turn over like a card); this view's SKILL TREE button calls
    /// FlipBack and X calls CloseAll. Nodes and edges are built in code from the perk table.
    /// </summary>
    public class ParagonTreeView : MonoBehaviour
    {
        class NodeView
        {
            public ParagonPerk Perk;
            public RectTransform Root, Diamond;
            public Image Fill, Ring;
            public Text Glyph, Tag;
            public bool Affordable;
        }

        class EdgeView
        {
            public Image Img;
            public ParagonPerk A, B;
        }

        static readonly Color Purple = Palette.Hex("8E5BFF");
        static readonly Color PurpleDim = Palette.Hex("3A2A5C");
        static readonly Color Locked = Palette.Hex("231A33");

        [SerializeField] GameManager game;
        [SerializeField] SkillTreeView skillTree;
        [SerializeField] FxLayer fx;
        [SerializeField] Font font;
        [SerializeField] Sprite rounded;
        [SerializeField] Sprite ring;

        [Header("Layout")]
        [SerializeField] RectTransform board;
        [SerializeField] RectTransform edgesLayer;
        [SerializeField] RectTransform nodesLayer;
        [SerializeField] RectTransform selection;
        [SerializeField] float nodeSize = 104f;
        [SerializeField] float capstoneSize = 140f;

        [Header("Header")]
        [SerializeField] Text pointsText;

        [Header("Details sheet")]
        [SerializeField] Text nameText;
        [SerializeField] Text kindText;
        [SerializeField] Text descriptionText;
        [SerializeField] Text statusText;
        [SerializeField] Button buyButton;
        [SerializeField] Image buyButtonImage;
        [SerializeField] Text buyLabel;

        readonly List<NodeView> _nodes = new List<NodeView>();
        readonly List<EdgeView> _edges = new List<EdgeView>();
        readonly Dictionary<ParagonPerk, NodeView> _byPerk = new Dictionary<ParagonPerk, NodeView>();
        ParagonPerk _selected;
        bool _built, _flipping;
        float _refreshTimer;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        // ================================================================== flip in / out

        public void FlipIn()
        {
            if (_flipping || IsOpen) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EnsureBuilt();
            if (_selected == null) _selected = DefaultSelection();
            RefreshAll();
            StartCoroutine(Flip(skillTree.transform, transform, true));
        }

        public void FlipBack()
        {
            if (_flipping || !IsOpen) return;
            StartCoroutine(Flip(transform, skillTree.transform, false));
        }

        public void CloseAll()
        {
            StopAllCoroutines();
            _flipping = false;
            skillTree.transform.localScale = Vector3.one;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
            skillTree.Close();
        }

        IEnumerator Flip(Transform from, Transform to, bool opening)
        {
            _flipping = true;
            const float half = 0.14f;
            to.localScale = new Vector3(0f, 1f, 1f);
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                from.localScale = new Vector3(1f - EaseIn(t / half), 1f, 1f);
                yield return null;
            }
            from.localScale = new Vector3(0f, 1f, 1f);
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                to.localScale = new Vector3(EaseOut(t / half), 1f, 1f);
                yield return null;
            }
            to.localScale = Vector3.one;
            from.localScale = Vector3.one;
            _flipping = false;
            if (!opening) gameObject.SetActive(false);
        }

        static float EaseIn(float x) { return x * x; }
        static float EaseOut(float x) { return 1f - (1f - x) * (1f - x); }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) { FlipBack(); return; } // Android back button
#endif
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f) { _refreshTimer = 0.25f; RefreshAll(); }

            // Perks you can buy right now breathe gently.
            foreach (var v in _nodes)
            {
                float bob = v.Affordable ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 4f + v.Perk.Position.y) : 1f;
                v.Diamond.localScale = new Vector3(bob, bob, 1f);
            }

            if (selection.gameObject.activeSelf)
            {
                float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f);
                selection.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        // ================================================================== build

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            var tree = game.ParagonTree;

            // Fit the whole tree inside the board, whatever the screen shape.
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in tree.Perks) { minY = Mathf.Min(minY, p.Position.y); maxY = Mathf.Max(maxY, p.Position.y); }
            float needed = maxY - minY + capstoneSize + 90f;
            float scale = Mathf.Min(1f, board.rect.height / needed);
            nodesLayer.localScale = edgesLayer.localScale = new Vector3(scale, scale, 1f);
            var offset = new Vector2(0, -(maxY + minY) * 0.5f);

            foreach (var p in tree.Perks)
                foreach (var parent in p.Parents)
                    _edges.Add(MakeEdge(parent, p, offset));
            foreach (var p in tree.Perks) MakeNode(p, offset);

            selection.SetParent(nodesLayer, false);
            selection.SetAsLastSibling();
            game.PerkPurchased += OnPerkChanged;
            game.Ascended += OnAscended;
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.PerkPurchased -= OnPerkChanged;
            game.Ascended -= OnAscended;
        }

        void OnPerkChanged(ParagonPerk p) { RefreshAll(); }

        void OnAscended(int levels) { _selected = null; }

        EdgeView MakeEdge(ParagonPerk a, ParagonPerk b, Vector2 offset)
        {
            var go = new GameObject("Edge " + a.Id + "-" + b.Id, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(edgesLayer, false);
            Vector2 pa = a.Position + offset, pb = b.Position + offset, d = pb - pa;
            rt.anchoredPosition = (pa + pb) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, 10f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return new EdgeView { Img = img, A = a, B = b };
        }

        void MakeNode(ParagonPerk p, Vector2 offset)
        {
            float size = p.Capstone ? capstoneSize : nodeSize;
            var root = new GameObject("Perk " + p.Id, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)root.transform;
            rt.SetParent(nodesLayer, false);
            rt.anchoredPosition = p.Position + offset;
            rt.sizeDelta = new Vector2(size + 40f, size + 40f);
            root.GetComponent<Image>().color = new Color(0, 0, 0, 0); // generous invisible hit area
            var perk = p;
            root.GetComponent<Button>().onClick.AddListener(() => OnNodeClicked(perk));

            var diamond = NewImage("Diamond", rt, rounded, size * 0.78f);
            diamond.type = Image.Type.Sliced;
            diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            var ringImg = NewImage("Ring", diamond.rectTransform, rounded, 0f);
            ringImg.type = Image.Type.Sliced;
            ringImg.fillCenter = false;
            var rrt = ringImg.rectTransform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.sizeDelta = new Vector2(10, 10);

            var glyph = NewText("Glyph", rt, p.Glyph, p.Capstone ? 38 : 28, Vector2.zero);
            var tag = NewText("Tag", rt, "", 24, new Vector2(0, -size * 0.74f));
            var view = new NodeView { Perk = p, Root = rt, Diamond = diamond.rectTransform, Fill = diamond, Ring = ringImg, Glyph = glyph, Tag = tag };
            _nodes.Add(view);
            _byPerk[p] = view;
        }

        Image NewImage(string name, Transform parent, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        Text NewText(string name, Transform parent, string s, int size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(220, 60);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = s;
            return t;
        }

        // ================================================================== state

        ParagonPerk DefaultSelection()
        {
            foreach (var p in game.ParagonTree.Perks) if (game.CanBuyPerk(p)) return p;
            return game.ParagonTree.Perks[0];
        }

        void RefreshAll()
        {
            if (!_built) return;
            int pts = game.ParagonPointsAvailable;
            pointsText.text = pts + "<size=30> / " + game.ParagonPoints + " POINTS</size>";

            foreach (var v in _nodes)
            {
                var p = v.Perk;
                bool owned = game.OwnsPerk(p), levelOk = game.ParagonLevel >= p.RequiredLevel;
                bool available = game.IsPerkAvailable(p), affordable = game.CanBuyPerk(p);
                Color fill, ringC, glyphC;
                if (owned) { fill = Palette.Gold; ringC = Palette.Text; glyphC = Palette.Panel; }
                else if (affordable) { fill = Purple; ringC = Palette.Gold; glyphC = Palette.Text; }
                else if (available) { fill = PurpleDim; ringC = Purple; glyphC = Palette.TextDim; }
                else { fill = Locked; ringC = PurpleDim; glyphC = Palette.WithAlpha(Palette.TextDim, 0.5f); }
                v.Fill.color = fill;
                v.Ring.color = ringC;
                v.Glyph.color = glyphC;
                v.Tag.text = owned ? "OWNED" : !levelOk ? "PARAGON " + p.RequiredLevel : p.Cost + (p.Cost == 1 ? " PT" : " PTS");
                v.Tag.color = owned ? Palette.Gold : affordable ? Palette.Text : Palette.TextDim;
                v.Affordable = affordable;
            }

            foreach (var e in _edges)
            {
                bool lit = game.OwnsPerk(e.A) && game.OwnsPerk(e.B), half = game.OwnsPerk(e.A);
                e.Img.color = lit ? Palette.Gold : half ? Palette.WithAlpha(Purple, 0.7f) : Palette.WithAlpha(PurpleDim, 0.8f);
            }
            RefreshSheet();
        }

        void OnNodeClicked(ParagonPerk p)
        {
            if (_selected == p && game.CanBuyPerk(p)) { Buy(); return; }
            _selected = p;
            Punch.Play(_byPerk[p].Root, 0.12f, 0.2f);
            Feedback.Play(Sfx.Click);
            RefreshSheet();
        }

        void RefreshSheet()
        {
            var p = _selected;
            if (p == null) { selection.gameObject.SetActive(false); return; }
            var v = _byPerk[p];
            selection.gameObject.SetActive(true);
            selection.anchoredPosition = v.Root.anchoredPosition;
            float size = (p.Capstone ? capstoneSize : nodeSize) + 30f;
            selection.sizeDelta = new Vector2(size, size);

            nameText.text = p.Name;
            kindText.text = (p.Capstone ? "CAPSTONE" : "PERK") + "  ·  NEEDS PARAGON " + p.RequiredLevel + "  ·  " + p.Cost + (p.Cost == 1 ? " POINT" : " POINTS");
            descriptionText.text = p.Description + "\n<size=26><color=#A99FB8><i>" + p.Flavor + " This run only.</i></color></size>";

            if (game.OwnsPerk(p))
            {
                statusText.text = "Active until you ascend";
                SetBuy("OWNED", Palette.PanelLight, Palette.Gold, false);
            }
            else if (game.ParagonLevel < p.RequiredLevel)
            {
                statusText.text = "Reach Paragon " + p.RequiredLevel + " to unlock";
                SetBuy("LOCKED", Palette.PanelLight, Palette.TextDim, false);
            }
            else if (!game.IsPerkAvailable(p))
            {
                statusText.text = p.Capstone ? "Needs both perks above it" : "Needs the perk above it";
                SetBuy("LOCKED", Palette.PanelLight, Palette.TextDim, false);
            }
            else if (!game.CanBuyPerk(p))
            {
                statusText.text = "Not enough Paragon Points this run";
                SetBuy("UNLOCK\n<size=30>" + p.Cost + " PTS</size>", Palette.PanelLight, Palette.TextDim, false);
            }
            else
            {
                statusText.text = "Tap the perk again to unlock instantly";
                SetBuy("UNLOCK\n<size=30>" + p.Cost + (p.Cost == 1 ? " PT" : " PTS") + "</size>", Palette.Gold, Palette.Panel, true);
            }
        }

        void SetBuy(string label, Color bg, Color fg, bool interactable)
        {
            buyLabel.text = label;
            buyLabel.color = fg;
            buyButtonImage.color = bg;
            buyButton.interactable = interactable;
        }

        /// <summary>Inspector-wired to the UNLOCK button.</summary>
        public void Buy()
        {
            var p = _selected;
            if (p == null || !game.TryBuyPerk(p)) return;
            var v = _byPerk[p];
            Punch.Play(v.Root, 0.5f, 0.45f);
            Vector2 at = fx.WorldToLocal(v.Root.position);
            fx.SpawnChips(at, Palette.Gold, 22);
            fx.SpawnText(at + new Vector2(0, 100), StatText.Describe(p.Effects[0]), Palette.Gold, 46, 1.4f, 180f);
            RefreshAll();
        }
    }
}
