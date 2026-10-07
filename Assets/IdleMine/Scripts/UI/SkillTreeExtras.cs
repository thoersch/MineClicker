using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Late-game buttons on the normal skill tree, shown once the Deep Core is unlocked:
    ///   * BUY ALL buys every affordable skill, cheapest first, in one tap.
    ///   * DEEP CORE flips the panel over (like a card) to the Deep Core tree; its SKILL TREE button flips back.
    /// Buttons are wired in the Inspector: Buy All -> BuyAll, Deep Core -> OpenDeepCore, and the Deep Core
    /// panel's Skill Tree button -> BackToSkillTree.
    /// </summary>
    public class SkillTreeExtras : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] SkillTreeView skillTree;
        [SerializeField] SkillTreeView deepCore;
        [SerializeField] FxLayer fx;
        [SerializeField] GameObject buyAllButton;
        [SerializeField] GameObject deepCoreButton;
        [SerializeField] Text deepCoreLabel;

        float _timer;
        bool _flipping;

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.25f;

            bool show = game.DeepCoreUnlocked;
            if (buyAllButton.activeSelf != show) buyAllButton.SetActive(show);
            if (deepCoreButton.activeSelf != show) deepCoreButton.SetActive(show);
            if (!show) return;
            var deep = game.DeepTree;
            deepCoreLabel.text = "DEEP CORE\n<size=26>" + (!game.Tree.IsComplete ? "finish the tree to breach"
                : deep.UnlockedCount + " / " + deep.Nodes.Count) + "</size>";
        }

        /// <summary>Inspector-wired to BUY ALL.</summary>
        public void BuyAll()
        {
            int bought = game.BuyAllSkills();
            if (bought == 0) { Feedback.Play(Sfx.Deny); return; }
            Feedback.Play(Sfx.Keystone);
            var at = fx.WorldToLocal(buyAllButton.transform.position);
            fx.SpawnChips(at, Palette.Gold, 30);
            fx.SpawnText(at + new Vector2(0, 120), "+" + bought + " skill" + (bought == 1 ? "" : "s"), Palette.Gold, 56, 1.4f, 200f);
        }

        /// <summary>Inspector-wired to DEEP CORE.</summary>
        public void OpenDeepCore()
        {
            if (_flipping || !game.DeepCoreUnlocked) return;
            StartCoroutine(Flip(skillTree, deepCore));
        }

        /// <summary>Inspector-wired to the Deep Core panel's SKILL TREE button.</summary>
        public void BackToSkillTree()
        {
            if (_flipping) return;
            StartCoroutine(Flip(deepCore, skillTree));
        }

        IEnumerator Flip(SkillTreeView from, SkillTreeView to)
        {
            _flipping = true;
            Feedback.Play(Sfx.Click);
            const float half = 0.14f;
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                float x = t / half;
                from.transform.localScale = new Vector3(1f - x * x, 1f, 1f);
                yield return null;
            }
            from.transform.localScale = Vector3.one;
            from.CloseInstant();
            to.OpenInstant();
            for (float t = 0; t < half; t += Time.unscaledDeltaTime)
            {
                float x = t / half;
                to.transform.localScale = new Vector3(1f - (1f - x) * (1f - x), 1f, 1f);
                yield return null;
            }
            to.transform.localScale = Vector3.one;
            _flipping = false;
        }
    }
}
