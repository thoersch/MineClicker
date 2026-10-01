using System;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    public enum NodeState { Hidden, Locked, Available, Affordable, Owned }

    /// <summary>
    /// One circle in the skill tree (SkillNode prefab). The root is a generous invisible hit area;
    /// the Visual child holds the art and is what gets punched when a node is bought or revealed.
    /// </summary>
    public class SkillNodeView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] RectTransform visual;
        [SerializeField] Image glow;
        [SerializeField] Image fill;
        [SerializeField] Image ring;
        [SerializeField] Text glyphText;
        [SerializeField] Text costText;
        [Tooltip("Extra invisible padding around the circle so small nodes are easy to hit.")]
        [SerializeField] float hitPadding = 36f;
        [SerializeField] int glyphSizeSmall = 28;
        [SerializeField] int glyphSizeLarge = 40;

        public SkillNode Node { get; private set; }
        public NodeState State { get; private set; }
        public RectTransform Visual { get { return visual; } }

        Color _branchColor;
        double _shownCost = -1;
        bool _first = true;

        public static float SizeFor(NodeKind k)
        {
            switch (k)
            {
                case NodeKind.Root: return 150;
                case NodeKind.Keystone: return 124;
                case NodeKind.Bridge: return 96;
                case NodeKind.Side: return 72;
                default: return 88;
            }
        }

        public void Setup(SkillNode node, Color branchColor, Action<SkillNodeView> onClick)
        {
            Node = node;
            _branchColor = branchColor;
            name = "Node " + node.Id;

            var rt = (RectTransform)transform;
            float size = SizeFor(node.Kind) + hitPadding;
            rt.anchoredPosition = node.Position;
            rt.sizeDelta = new Vector2(size, size);

            glyphText.text = node.Glyph;
            glyphText.fontSize = node.Kind == NodeKind.Keystone || node.Kind == NodeKind.Root ? glyphSizeLarge : glyphSizeSmall;
            glow.color = Palette.WithAlpha(branchColor, 0.25f);
            button.onClick.AddListener(() => onClick(this));
        }

        /// <summary>Returns true when the node just came out of the fog (caller can celebrate it).</summary>
        public bool Apply(NodeState state, double cost)
        {
            bool revealed = !_first && State == NodeState.Hidden && state != NodeState.Hidden;
            bool changed = _first || state != State;
            _first = false;
            State = state;

            if ((state == NodeState.Available || state == NodeState.Affordable) && cost != _shownCost)
            {
                _shownCost = cost;
                costText.text = NumberFormat.Money(cost);
            }
            if (!changed) return false;

            bool hidden = state == NodeState.Hidden;
            visual.localScale = hidden ? new Vector3(0.4f, 0.4f, 1f) : Vector3.one;
            glyphText.gameObject.SetActive(!hidden);
            costText.gameObject.SetActive(state == NodeState.Available || state == NodeState.Affordable);
            glow.gameObject.SetActive(state == NodeState.Owned || state == NodeState.Affordable);

            switch (state)
            {
                case NodeState.Hidden:
                    fill.color = new Color(0.32f, 0.29f, 0.38f, 0.6f);
                    ring.color = new Color(0, 0, 0, 0);
                    break;
                case NodeState.Locked:
                    fill.color = Palette.Panel;
                    ring.color = Palette.WithAlpha(_branchColor, 0.3f);
                    glyphText.color = Palette.WithAlpha(Palette.TextDim, 0.6f);
                    break;
                case NodeState.Available:
                    fill.color = Palette.Panel;
                    ring.color = Palette.WithAlpha(_branchColor, 0.85f);
                    glyphText.color = _branchColor;
                    costText.color = Palette.Red;
                    break;
                case NodeState.Affordable:
                    fill.color = Palette.PanelLight;
                    ring.color = Palette.Green;
                    glow.color = Palette.WithAlpha(Palette.Green, 0.3f);
                    glyphText.color = Color.white;
                    costText.color = Palette.Green;
                    break;
                case NodeState.Owned:
                    fill.color = _branchColor;
                    ring.color = Palette.WithAlpha(Color.white, 0.9f);
                    glow.color = Palette.WithAlpha(_branchColor, 0.3f);
                    glyphText.color = Palette.Panel;
                    break;
            }
            return revealed;
        }
    }
}
