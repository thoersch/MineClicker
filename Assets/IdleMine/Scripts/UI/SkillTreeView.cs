using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Full-screen pop-out skill tree. Slides up over the mine (which keeps running underneath).
    /// Drag to pan, pinch or mouse-wheel to zoom, tap a node to inspect, tap again (or BUY) to purchase.
    /// Unexplored nodes are fogged as faint dots so the tree always feels bigger than what you've seen.
    ///
    /// The panel starts inactive in the scene; nodes are instantiated from prefabs the first time it opens.
    /// Buttons are wired in the Inspector: Close -> Close, Next Best -> FocusNextBest, Buy -> Buy.
    /// </summary>
    public class SkillTreeView : MonoBehaviour
    {
        class Edge
        {
            public Image Img;
            public SkillNode A, B;
            public Color Color;
            public int State = -1;
        }

        [SerializeField] GameManager game;
        [SerializeField] CanvasGroup group;
        [SerializeField] PanZoom panZoom;
        [SerializeField] FxLayer fx;

        [Header("Tree content")]
        [SerializeField] RectTransform labelsLayer;
        [SerializeField] RectTransform edgesLayer;
        [SerializeField] RectTransform nodesLayer;
        [SerializeField] RectTransform selectionRing;
        [SerializeField] SkillNodeView nodePrefab;
        [SerializeField] Image edgePrefab;
        [SerializeField] Text branchLabelPrefab;
        [SerializeField] float initialZoom = 0.85f;

        [Header("Header")]
        [SerializeField] Text moneyText;
        [SerializeField] Text countText;

        [Header("Details sheet")]
        [SerializeField] Text nameText;
        [SerializeField] Text kindText;
        [SerializeField] Text descriptionText;
        [SerializeField] Text statusText;
        [SerializeField] Text hintText;
        [SerializeField] Button buyButton;
        [SerializeField] Image buyButtonImage;
        [SerializeField] Text buyLabel;

        readonly List<SkillNodeView> _views = new List<SkillNodeView>();
        readonly Dictionary<SkillNode, SkillNodeView> _byNode = new Dictionary<SkillNode, SkillNodeView>();
        readonly List<Edge> _edges = new List<Edge>();
        Image _selectionImage;
        SkillNode _selected;
        RectTransform _panel;
        bool _built, _open, _focusedOnce;
        float _openT, _refreshTimer;

        public bool IsOpen { get { return _open; } }
        public SkillNode Selected { get { return _selected; } }

        // ================================================================== build (first open)

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            _panel = (RectTransform)transform;
            _selectionImage = selectionRing.GetComponent<Image>();
            var tree = game.Tree;
            panZoom.Bounds = tree.Bounds;

            // Branch names out at the rim
            float rim = game.Config.treeInnerRadius + game.Config.tiersPerBranch * game.Config.treeTierSpacing + 160f;
            foreach (var b in tree.Branches)
            {
                var label = Instantiate(branchLabelPrefab, labelsLayer);
                label.text = b.Name.ToUpperInvariant();
                label.color = Palette.WithAlpha(b.Color, 0.55f);
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(b.Angle), Mathf.Sin(b.Angle)) * rim;
            }

            foreach (var n in tree.Nodes)
            {
                Color c = n.Branch >= 0 ? tree.Branches[n.Branch].Color : Palette.Gold;
                foreach (var p in n.Parents) _edges.Add(MakeEdge(p, n, c));
                var v = Instantiate(nodePrefab, nodesLayer);
                v.Setup(n, c, OnNodeClicked);
                _views.Add(v);
                _byNode[n] = v;
            }
            selectionRing.SetAsLastSibling();
        }

        Edge MakeEdge(SkillNode a, SkillNode b, Color c)
        {
            var img = Instantiate(edgePrefab, edgesLayer);
            var rt = img.rectTransform;
            Vector2 d = b.Position - a.Position;
            rt.anchoredPosition = (a.Position + b.Position) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, rt.sizeDelta.y);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return new Edge { Img = img, A = a, B = b, Color = c };
        }

        // ================================================================== open / close

        public void Open()
        {
            if (_open) return;
            gameObject.SetActive(true);
            EnsureBuilt();
            _open = true;
            transform.SetAsLastSibling();
            panZoom.ClearPointers();
            RefreshAll(false);

            if (!_focusedOnce)
            {
                // First visit: centre on "Add 1 Miner" and pre-select it for new players.
                _focusedOnce = true;
                panZoom.FocusOn(Vector2.zero, initialZoom, true);
                if (!game.Tree.Root.Unlocked) Select(game.Tree.Root);
            }
        }

        public void Close() { _open = false; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _openT = Mathf.MoveTowards(_openT, _open ? 1f : 0f, dt / 0.28f);
            float e = _open ? 1f - Mathf.Pow(1f - _openT, 3f) : _openT * _openT;
            _panel.anchoredPosition = new Vector2(0, -(1f - e) * _panel.rect.height);
            group.alpha = Mathf.Clamp01(e * 1.5f);
            group.blocksRaycasts = _open;
            if (!_open && _openT <= 0f) { gameObject.SetActive(false); return; }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (_open && Input.GetKeyDown(KeyCode.Escape)) Close(); // Android back button
#endif

            moneyText.text = NumberFormat.Money(game.Money);

            _refreshTimer -= dt;
            if (_refreshTimer <= 0f) { _refreshTimer = 0.2f; RefreshAll(true); }

            if (selectionRing.gameObject.activeSelf)
            {
                float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f);
                selectionRing.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        // ================================================================== state

        void RefreshAll(bool celebrateReveals)
        {
            var tree = game.Tree;
            foreach (var v in _views)
            {
                var n = v.Node;
                NodeState s;
                if (n.Unlocked) s = NodeState.Owned;
                else if (tree.IsAvailable(n)) s = game.CanAfford(n) ? NodeState.Affordable : NodeState.Available;
                else if (tree.IsRevealed(n)) s = NodeState.Locked;
                else s = NodeState.Hidden;

                if (v.Apply(s, game.GetCost(n)) && celebrateReveals) Punch.Play(v.Visual, 0.6f, 0.45f);
            }

            foreach (var edge in _edges)
            {
                int state = edge.A.Unlocked && edge.B.Unlocked ? 2 : (edge.A.Unlocked || edge.B.Unlocked ? 1 : 0);
                if (state == edge.State) continue;
                edge.State = state;
                var rt = edge.Img.rectTransform;
                if (state == 2) { edge.Img.color = Palette.WithAlpha(edge.Color, 0.9f); rt.sizeDelta = new Vector2(rt.sizeDelta.x, 12); }
                else if (state == 1) { edge.Img.color = Palette.WithAlpha(edge.Color, 0.4f); rt.sizeDelta = new Vector2(rt.sizeDelta.x, 8); }
                else { edge.Img.color = new Color(1, 1, 1, 0.06f); rt.sizeDelta = new Vector2(rt.sizeDelta.x, 5); }
            }

            countText.text = tree.UnlockedCount + " / " + tree.Nodes.Count + " skills";
            RefreshSheet();
        }

        void OnNodeClicked(SkillNodeView v)
        {
            // A second tap on the selected node buys it: fast for mass-buying.
            if (_selected == v.Node && game.CanPurchase(v.Node)) { Buy(); return; }
            Select(v.Node);
            Punch.Play(v.Visual, 0.12f, 0.2f);
        }

        void Select(SkillNode n)
        {
            _selected = n;
            if (n != null)
            {
                var v = _byNode[n];
                float size = SkillNodeView.SizeFor(n.Kind) + 34f;
                selectionRing.anchoredPosition = n.Position;
                selectionRing.sizeDelta = new Vector2(size, size);
                selectionRing.gameObject.SetActive(true);
                _selectionImage.color = v.State == NodeState.Hidden ? Palette.TextDim : Palette.Gold;
            }
            else selectionRing.gameObject.SetActive(false);
            RefreshSheet();
        }

        void RefreshSheet()
        {
            var n = _selected;
            bool has = n != null;
            hintText.gameObject.SetActive(!has);
            nameText.gameObject.SetActive(has);
            kindText.gameObject.SetActive(has);
            descriptionText.gameObject.SetActive(has);
            statusText.gameObject.SetActive(has);
            buyButton.gameObject.SetActive(has);
            if (!has) return;

            var tree = game.Tree;
            Color branchColor = n.Branch >= 0 ? tree.Branches[n.Branch].Color : Palette.Gold;
            string branch = n.Branch >= 0 ? tree.Branches[n.Branch].Name.ToUpperInvariant() : "";

            if (!tree.IsRevealed(n))
            {
                nameText.text = "Unknown Skill";
                nameText.color = Palette.TextDim;
                kindText.text = n.Branch >= 0 ? branch + "  \u00B7  TIER " + n.Tier : "";
                descriptionText.text = "Unlock a connected skill to reveal this one.";
                statusText.text = "";
                SetBuy("???", Palette.PanelLight, Palette.TextDim, false);
                return;
            }

            nameText.text = n.Name;
            nameText.color = branchColor;
            string kind = n.Kind == NodeKind.Root ? "THE FIRST STEP" : n.Kind.ToString().ToUpperInvariant();
            kindText.text = kind + (n.Branch >= 0 ? "  \u00B7  " + branch + "  \u00B7  TIER " + n.Tier : "");
            descriptionText.text = n.Description + (string.IsNullOrEmpty(n.Flavor) ? "" : "\n<size=26><color=#A99FB8><i>" + n.Flavor + "</i></color></size>");

            double cost = game.GetCost(n);
            if (n.Unlocked)
            {
                statusText.text = "Owned";
                SetBuy("OWNED", Palette.PanelLight, Palette.TextDim, false);
            }
            else if (tree.IsAvailable(n))
            {
                bool afford = game.CanAfford(n);
                statusText.text = afford ? "Tap the node again to buy instantly" : "Need " + NumberFormat.Money(cost - game.Money) + " more";
                SetBuy("BUY\n<size=34>" + NumberFormat.Money(cost) + "</size>", afford ? Palette.Green : Palette.PanelLight, afford ? Palette.Panel : Palette.TextDim, afford);
            }
            else
            {
                statusText.text = tree.DescribeRequirement(n);
                SetBuy("LOCKED\n<size=30>" + NumberFormat.Money(cost) + "</size>", Palette.PanelLight, Palette.TextDim, false);
            }
        }

        void SetBuy(string label, Color bg, Color fg, bool interactable)
        {
            buyLabel.text = label;
            buyLabel.color = fg;
            buyButtonImage.color = bg;
            buyButton.interactable = interactable;
        }

        // ================================================================== actions (also Inspector-wired)

        public void Buy()
        {
            var n = _selected;
            if (n == null) return;
            if (!game.TryPurchase(n))
            {
                Punch.Play(buyButton.transform, 0.08f, 0.2f);
                return;
            }

            var v = _byNode[n];
            Color c = n.Branch >= 0 ? game.Tree.Branches[n.Branch].Color : Palette.Gold;
            Punch.Play(v.Visual, 0.45f, 0.4f);
            Vector2 p = fx.WorldToLocal(v.transform.position);
            fx.SpawnChips(p, c, 18);
            fx.SpawnText(p + new Vector2(0, 90), n.Effects.Count > 0 ? StatText.Describe(n.Effects[0]) : n.Name, Palette.Gold, 44, 1.4f, 180f);
            RefreshAll(true);
        }

        public void FocusNextBest()
        {
            SkillNode best = null;
            bool bestAffordable = false;
            double bestCost = double.MaxValue;
            foreach (var n in game.Tree.Nodes)
            {
                if (!game.Tree.IsAvailable(n)) continue;
                double cost = game.GetCost(n);
                bool afford = cost <= game.Money;
                if ((afford && !bestAffordable) || (afford == bestAffordable && cost < bestCost))
                {
                    best = n; bestCost = cost; bestAffordable = afford;
                }
            }
            if (best == null) return;
            panZoom.FocusOn(best.Position, Mathf.Max(panZoom.Zoom, 0.8f));
            Select(best);
            Punch.Play(_byNode[best].Visual, 0.3f, 0.3f);
        }
    }
}
