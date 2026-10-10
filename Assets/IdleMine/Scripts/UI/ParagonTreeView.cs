using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// The Paragon tree: the flip side of the skill tree. Deep purple and gold, diamond nodes, and Paragon
    /// Points instead of cash. Three endless lanes scroll vertically; tier N opens at Paragon N, every perk costs
    /// one point, and a glowing line marks how far your level reaches. Perks are kept when you ascend; RESPEC
    /// (tap twice) refunds them all for free.
    ///
    /// Its GameObject starts inactive in the scene and lays out on top of the skill tree. The skill tree's
    /// PARAGON TREE button calls FlipIn (the panels turn over like a card); this view's SKILL TREE button calls
    /// FlipBack and X calls CloseAll. Scrolling, nodes, edges, lane headers and the RESPEC button are built in code.
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

        const float TopPad = 150f, BottomPad = 260f;
        static readonly Color Purple = Palette.Hex("8E5BFF");
        static readonly Color PurpleDim = Palette.Hex("3A2A5C");
        static readonly Color Locked = Palette.Hex("231A33");
        static readonly Color[] LaneColors = { Palette.Hex("7FD1FF"), Palette.Gold, Palette.Hex("FF9A4D") };

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
        float _refreshTimer, _respecArmed;
        RectTransform _content, _levelLine;
        Text _levelText;
        Image _levelImg;
        GameObject _respec;
        Text _respecLabel;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        // ================================================================== flip in / out

        public void FlipIn()
        {
            if (_flipping || IsOpen) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EnsureBuilt();
            BuildMissing();
            if (_selected == null || game.OwnsPerk(_selected)) _selected = DefaultSelection();
            _respecArmed = 0f;
            RefreshAll();
            ScrollTo(_selected);
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
            float dt = Time.unscaledDeltaTime;
            _refreshTimer -= dt;
            if (_refreshTimer <= 0f) { _refreshTimer = 0.25f; BuildMissing(); RefreshAll(); }

            if (_respecArmed > 0f)
            {
                _respecArmed -= dt;
                if (_respecArmed <= 0f) RefreshRespec();
            }

            // Perks you can buy right now breathe gently.
            float time = Time.unscaledTime;
            foreach (var v in _nodes)
            {
                float bob = v.Affordable ? 1f + 0.06f * Mathf.Sin(time * 4f + v.Perk.Position.y * 0.01f) : 1f;
                v.Diamond.localScale = new Vector3(bob, bob, 1f);
            }

            if (selection.gameObject.activeSelf)
            {
                float pulse = 1f + 0.06f * Mathf.Sin(time * 6f);
                selection.localScale = new Vector3(pulse, pulse, 1f);
            }
            if (_levelImg != null && _levelLine.gameObject.activeSelf)
                _levelImg.color = Palette.WithAlpha(Purple, 0.55f + 0.3f * Mathf.Sin(time * 3f));
        }

        // ================================================================== build

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            // The board scrolls: a mask, a tall content rect, and both layers hung from its top centre.
            if (board.GetComponent<RectMask2D>() == null) board.gameObject.AddComponent<RectMask2D>();
            if (board.GetComponent<Image>() == null) board.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // drag target
            _content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            _content.SetParent(board, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            foreach (var layer in new[] { edgesLayer, nodesLayer })
            {
                layer.SetParent(_content, false);
                layer.anchorMin = layer.anchorMax = new Vector2(0.5f, 1f);
                layer.pivot = new Vector2(0.5f, 0.5f);
                layer.anchoredPosition = new Vector2(0f, -TopPad);
                layer.sizeDelta = Vector2.zero;
                layer.localScale = Vector3.one;
            }
            var scroll = board.GetComponent<ScrollRect>();
            if (scroll == null) scroll = board.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = board;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            // Lane headers above tier 1.
            for (int lane = 0; lane < 3; lane++)
            {
                var h = NewText("Lane " + ParagonTree.LaneNames[lane], nodesLayer, ParagonTree.LaneNames[lane], 34, new Vector2((lane - 1) * ParagonTree.LaneX, 100f));
                h.color = LaneColors[lane];
                var o = h.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.6f);
                o.effectDistance = new Vector2(2f, -2f);
            }

            // The reach line: everything above it is unlocked by your Paragon level.
            _levelLine = (RectTransform)new GameObject("Level Line", typeof(RectTransform), typeof(Image)).transform;
            _levelLine.SetParent(edgesLayer, false);
            _levelLine.sizeDelta = new Vector2(1040f, 6f);
            _levelImg = _levelLine.GetComponent<Image>();
            _levelImg.raycastTarget = false;
            // Label in the left margin, clear of the lanes and their tags.
            _levelText = NewText("Level Text", _levelLine, "", 22, new Vector2(-520f + 4f, 18f));
            _levelText.alignment = TextAnchor.LowerLeft;
            _levelText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _levelText.color = Palette.Hex("C9A8FF");

            selection.SetParent(nodesLayer, false);
            BuildRespec();

            var sub = transform.Find("Header/Subtitle");
            if (sub != null) sub.GetComponent<Text>().text = "Perks stay when you ascend  ·  respec free";

            game.PerkPurchased += OnPerkChanged;
            game.PerksRespecced += OnRespecced;
        }

        void OnDestroy()
        {
            if (game == null || !_built) return;
            game.PerkPurchased -= OnPerkChanged;
            game.PerksRespecced -= OnRespecced;
        }

        void OnPerkChanged(ParagonPerk p) { RefreshAll(); }

        void OnRespecced()
        {
            _selected = DefaultSelection();
            RefreshAll();
            ScrollTo(_selected);
        }

        /// <summary>Adds nodes and edges for tiers the tree has grown since the view was built.</summary>
        void BuildMissing()
        {
            if (!_built) return;
            var tree = game.ParagonTree;
            if (_byPerk.Count == tree.Perks.Count) return;
            foreach (var p in tree.Perks)
            {
                if (_byPerk.ContainsKey(p)) continue;
                foreach (var parent in p.Parents) _edges.Add(MakeEdge(parent, p));
                MakeNode(p);
            }
            selection.SetAsLastSibling();
            _content.sizeDelta = new Vector2(0f, TopPad + (tree.Tiers - 1) * ParagonTree.TierStep + BottomPad);
        }

        EdgeView MakeEdge(ParagonPerk a, ParagonPerk b)
        {
            var go = new GameObject("Edge " + a.Id + "-" + b.Id, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(edgesLayer, false);
            Vector2 pa = a.Position, pb = b.Position, d = pb - pa;
            rt.anchoredPosition = (pa + pb) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, 10f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return new EdgeView { Img = img, A = a, B = b };
        }

        void MakeNode(ParagonPerk p)
        {
            float size = p.Capstone ? capstoneSize : nodeSize;
            var root = new GameObject("Perk " + p.Id, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)root.transform;
            rt.SetParent(nodesLayer, false);
            rt.anchoredPosition = p.Position;
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

            var glyph = NewText("Glyph", rt, p.Glyph, p.Capstone ? 36 : 28, Vector2.zero);
            var tag = NewText("Tag", rt, "", 24, new Vector2(0, -size * 0.72f));
            var view = new NodeView { Perk = p, Root = rt, Diamond = diamond.rectTransform, Fill = diamond, Ring = ringImg, Glyph = glyph, Tag = tag };
            _nodes.Add(view);
            _byPerk[p] = view;
        }

        void BuildRespec()
        {
            // Bottom-right of the board, mirroring the SKILL TREE button.
            var rt = (RectTransform)new GameObject("Respec Button", typeof(RectTransform), typeof(Image), typeof(Button)).transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 350f);
            rt.sizeDelta = new Vector2(250f, 90f);
            var img = rt.GetComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = Palette.PanelLight;
            rt.GetComponent<Button>().onClick.AddListener(OnRespecClicked);
            _respecLabel = NewText("Label", rt, "RESPEC", 34, Vector2.zero);
            var lr = _respecLabel.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.sizeDelta = Vector2.zero;
            _respec = rt.gameObject;
            var fxLayer = fx != null ? fx.transform : null;
            if (fxLayer != null && fxLayer.parent == transform) rt.SetSiblingIndex(fxLayer.GetSiblingIndex());
        }

        void OnRespecClicked()
        {
            if (_respecArmed <= 0f)
            {
                _respecArmed = 2.5f;
                Feedback.Play(Sfx.Click);
                Punch.Play(_respec.transform, 0.12f, 0.2f);
                RefreshRespec();
                return;
            }
            _respecArmed = 0f;
            int refunded = game.ParagonPointsSpent;
            if (!game.RespecPerks()) { RefreshRespec(); return; }
            Punch.Play(_respec.transform, 0.3f, 0.35f);
            fx.SpawnText(fx.WorldToLocal(_respec.transform.position) + new Vector2(-120f, 110f),
                         "+" + refunded + " POINT" + (refunded == 1 ? "" : "S") + " BACK", Purple, 44, 1.3f, 160f);
            RefreshRespec();
        }

        void RefreshRespec()
        {
            bool any = game.ParagonPointsSpent > 0;
            if (_respec.activeSelf != any) _respec.SetActive(any);
            bool armed = _respecArmed > 0f;
            _respecLabel.text = armed ? "TAP AGAIN\n<size=24>refund all perks</size>" : "RESPEC\n<size=24>free</size>";
            _respecLabel.color = armed ? Palette.Panel : Palette.Text;
            _respec.GetComponent<Image>().color = armed ? Purple : Palette.PanelLight;
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
            // The shallowest perk you can buy right now, else the shallowest you can't reach yet.
            foreach (var p in game.ParagonTree.Perks) if (game.CanBuyPerk(p)) return p;
            foreach (var p in game.ParagonTree.Perks) if (!game.OwnsPerk(p)) return p;
            return game.ParagonTree.Perks[0];
        }

        /// <summary>Scrolls the board so the perk sits a little above the middle.</summary>
        void ScrollTo(ParagonPerk p)
        {
            if (p == null || !_built) return;
            Canvas.ForceUpdateCanvases();
            float viewH = board.rect.height, contentH = _content.sizeDelta.y;
            float y = TopPad - p.Position.y - viewH * 0.4f;
            _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, contentH - viewH)));
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
                else if (levelOk) { fill = Locked; ringC = Purple; glyphC = Palette.TextDim; }
                else { fill = Locked; ringC = PurpleDim; glyphC = Palette.WithAlpha(Palette.TextDim, 0.5f); }
                v.Fill.color = fill;
                v.Ring.color = ringC;
                v.Glyph.color = glyphC;
                v.Tag.text = owned ? "OWNED" : !levelOk ? "PARAGON " + p.RequiredLevel : "1 PT";
                v.Tag.color = owned ? Palette.Gold : affordable ? Palette.Text : Palette.TextDim;
                v.Affordable = affordable;
            }

            foreach (var e in _edges)
            {
                bool lit = game.OwnsPerk(e.A) && game.OwnsPerk(e.B), half = game.OwnsPerk(e.A);
                e.Img.color = lit ? Palette.Gold : half ? Palette.WithAlpha(Purple, 0.7f) : Palette.WithAlpha(PurpleDim, 0.8f);
            }

            // Reach line between your level's tier and the next one.
            int level = game.ParagonLevel;
            bool showLine = level < game.ParagonTree.Tiers;
            if (_levelLine.gameObject.activeSelf != showLine) _levelLine.gameObject.SetActive(showLine);
            if (showLine)
            {
                // Just under tier `level`'s tags, above the next tier's diamonds.
                _levelLine.anchoredPosition = new Vector2(0f, level == 0 ? 60f : -(level - 1) * ParagonTree.TierStep - 102f);
                _levelText.text = level == 0 ? "ASCEND" : "PARAGON " + level;
            }

            RefreshRespec();
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

            int lane = Mathf.Clamp(Mathf.RoundToInt(p.Position.x / ParagonTree.LaneX) + 1, 0, 2);
            nameText.text = p.Name;
            kindText.text = (p.Capstone ? "MAJOR PERK" : "PERK") + "  ·  " + ParagonTree.LaneNames[lane] + "  ·  NEEDS PARAGON " + p.RequiredLevel + "  ·  1 POINT";
            descriptionText.text = p.Description + "\n<size=26><color=#A99FB8><i>" + p.Flavor + "</i></color></size>";

            if (game.OwnsPerk(p))
            {
                statusText.text = "Owned  ·  kept when you ascend";
                SetBuy("OWNED", Palette.PanelLight, Palette.Gold, false);
            }
            else if (game.ParagonLevel < p.RequiredLevel)
            {
                statusText.text = "Reach Paragon " + p.RequiredLevel + " to unlock";
                SetBuy("LOCKED", Palette.PanelLight, Palette.TextDim, false);
            }
            else if (!game.IsPerkAvailable(p))
            {
                statusText.text = "Needs the perk above it";
                SetBuy("LOCKED", Palette.PanelLight, Palette.TextDim, false);
            }
            else if (!game.CanBuyPerk(p))
            {
                statusText.text = "No points left  ·  ascend for another, or respec";
                SetBuy("UNLOCK\n<size=30>1 PT</size>", Palette.PanelLight, Palette.TextDim, false);
            }
            else
            {
                statusText.text = "Tap the perk again to unlock instantly";
                SetBuy("UNLOCK\n<size=30>1 PT</size>", Palette.Gold, Palette.Panel, true);
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
            // Move on to the next perk down the same lane, so tapping keeps climbing.
            foreach (var next in game.ParagonTree.Perks)
                if (next.Parents.Contains(p) && game.CanBuyPerk(next)) { _selected = next; break; }
            RefreshAll();
        }
    }
}
