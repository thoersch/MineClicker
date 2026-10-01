using System;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>Top toolbar: cash (rolling counter), cash/sec, lifetime ore, miners, depth.</summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] GameManager game;
        [SerializeField] RectTransform coin;
        [SerializeField] Text moneyText;
        [SerializeField] Text incomeText;
        [SerializeField] Text oreText;
        [SerializeField] Text minersText;
        [SerializeField] Text depthText;
        [SerializeField] Color idleMinersColor = new Color(1f, 0.62f, 0.26f, 1f);

        double _shownMoney;
        float _slowTimer;
        string _idleHex;

        void Start()
        {
            _shownMoney = game.Money;
            _idleHex = ColorUtility.ToHtmlStringRGB(idleMinersColor);
            game.Tapped += OnTapped;
            game.NodePurchased += OnPurchased;
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.Tapped -= OnTapped;
            game.NodePurchased -= OnPurchased;
        }

        void OnTapped(TapResult r)
        {
            if (!r.Auto) Punch.Play(coin, r.Crit ? 0.35f : 0.12f, 0.25f);
        }

        void OnPurchased(SkillNode n)
        {
            _shownMoney = game.Money;
            Punch.Play(moneyText.transform, 0.08f, 0.3f);
        }

        void Update()
        {
            // Rolling counter: glide up toward the real value, snap down when spending.
            double target = game.Money;
            if (target < _shownMoney) _shownMoney = target;
            else
            {
                _shownMoney += (target - _shownMoney) * (1.0 - Math.Exp(-12.0 * Time.unscaledDeltaTime));
                if (target - _shownMoney < Math.Max(0.01, target * 0.0005)) _shownMoney = target;
            }
            moneyText.text = NumberFormat.Money(_shownMoney);

            _slowTimer -= Time.unscaledDeltaTime;
            if (_slowTimer > 0) return;
            _slowTimer = 0.1f;

            incomeText.text = "+" + NumberFormat.Money(game.IncomePerSecond) + "/s";
            oreText.text = NumberFormat.Format(game.LifetimeOre);
            int free = game.FreeMiners;
            minersText.text = free > 0
                ? "<color=#" + _idleHex + ">" + free + " idle</color> / " + game.TotalMiners
                : game.TotalMiners.ToString();
            depthText.text = game.Depth.ToString();
        }
    }
}
