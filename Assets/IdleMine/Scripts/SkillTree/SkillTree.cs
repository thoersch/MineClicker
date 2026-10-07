using System.Collections.Generic;
using UnityEngine;

namespace IdleMine
{
    public enum NodeKind { Root, Spine, Side, Keystone, Bridge }

    public class SkillNode
    {
        public string Id;
        public string Name;
        public string Flavor;
        public NodeKind Kind;
        public int Branch = -1;          // -1 for the root
        public int Tier;
        public Vector2 Position;         // canvas units, root at (0,0)
        public double BaseCost;
        public bool RequireAll;          // true: every parent needed. false: any one parent.
        public string Glyph;             // short label drawn on the node

        public readonly List<SkillEffect> Effects = new List<SkillEffect>();
        public readonly List<SkillNode> Parents = new List<SkillNode>();
        public readonly List<SkillNode> Children = new List<SkillNode>();

        public bool Unlocked;
        public SkillTree Owner;          // the tree this node belongs to (set by SkillTree)

        public string Description
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Effects.Count; i++)
                {
                    if (i > 0) sb.Append('\n');
                    sb.Append(StatText.Describe(Effects[i]));
                }
                return sb.ToString();
            }
        }
    }

    public class BranchInfo
    {
        public string Name;
        public Color Color;
        public float Angle; // radians
    }

    /// <summary>Runtime container: lookup, availability rules, ownership. Knows nothing about money.</summary>
    public class SkillTree
    {
        public readonly List<SkillNode> Nodes;
        public readonly List<BranchInfo> Branches;
        public readonly SkillNode Root;
        public readonly Rect Bounds;
        public int UnlockedCount { get; private set; }
        public bool IsComplete { get { return UnlockedCount >= Nodes.Count; } }

        readonly Dictionary<string, SkillNode> _byId = new Dictionary<string, SkillNode>();

        public SkillTree(List<SkillNode> nodes, List<BranchInfo> branches, SkillNode root)
        {
            Nodes = nodes;
            Branches = branches;
            Root = root;

            float minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (var n in nodes)
            {
                _byId[n.Id] = n;
                n.Owner = this;
                minX = Mathf.Min(minX, n.Position.x); maxX = Mathf.Max(maxX, n.Position.x);
                minY = Mathf.Min(minY, n.Position.y); maxY = Mathf.Max(maxY, n.Position.y);
            }
            foreach (var n in nodes)
                foreach (var p in n.Parents)
                    p.Children.Add(n);

            const float pad = 200f;
            Bounds = Rect.MinMaxRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
        }

        public SkillNode Get(string id)
        {
            SkillNode n;
            return _byId.TryGetValue(id, out n) ? n : null;
        }

        /// <summary>Can be bought right now if you have the cash.</summary>
        public bool IsAvailable(SkillNode n)
        {
            if (n.Unlocked) return false;
            if (n.Parents.Count == 0) return true;
            if (n.RequireAll)
            {
                foreach (var p in n.Parents) if (!p.Unlocked) return false;
                return true;
            }
            foreach (var p in n.Parents) if (p.Unlocked) return true;
            return false;
        }

        /// <summary>Visible with full details (owned, or touching something owned). Everything else is fog.</summary>
        public bool IsRevealed(SkillNode n)
        {
            if (n.Unlocked || n.Parents.Count == 0) return true;
            foreach (var p in n.Parents) if (p.Unlocked) return true;
            return false;
        }

        public void Unlock(SkillNode n)
        {
            if (n.Unlocked) return;
            n.Unlocked = true;
            UnlockedCount++;
        }

        public void ResetOwnership()
        {
            foreach (var n in Nodes) n.Unlocked = false;
            UnlockedCount = 0;
        }

        public string DescribeRequirement(SkillNode n)
        {
            if (n.Parents.Count == 0) return "";
            var names = new List<string>();
            foreach (var p in n.Parents) if (!p.Unlocked) names.Add(p.Name);
            if (names.Count == 0) return "";
            return (n.RequireAll ? "Requires " : "Requires one of: ") + string.Join(", ", names.ToArray());
        }
    }
}
