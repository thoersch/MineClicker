using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static IdleMine.EditorTools.UiKit;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Idle Mine > Monetization > Install UI In Open Scene.
    /// Builds the ad and Foreman Pass UI into Main.unity in the project's style and wires every Inspector
    /// reference and button, so afterwards it's ordinary scene UI you can move and restyle. Safe to run
    /// again: pieces that already exist are left alone, so delete one first if you want it rebuilt.
    /// </summary>
    public static class MonetizationInstaller
    {

        [MenuItem("Idle Mine/Monetization/Install UI In Open Scene")]
        public static void Install()
        {
            UiKit.LoadAssets();

            var game = Object.FindObjectOfType<GameManager>(true);
            var safeArea = Object.FindObjectOfType<SafeArea>(true);
            var hud = Object.FindObjectOfType<HudView>(true);
            var offline = Object.FindObjectOfType<OfflinePopup>(true);
            var tree = Object.FindObjectOfType<SkillTreeView>(true);
            var paragon = Object.FindObjectOfType<ParagonView>(true);
            var toasts = Object.FindObjectOfType<Toasts>(true);
            var bottomBar = Object.FindObjectOfType<BottomBar>(true);
            FxLayer fx = null;
            foreach (var f in Object.FindObjectsOfType<FxLayer>(true))
                if (f.transform.parent == safeArea.transform) fx = f;

            if (game == null || safeArea == null || hud == null || offline == null || tree == null || fx == null)
            {
                EditorUtility.DisplayDialog("Idle Mine", "Open Assets/IdleMine/Scenes/Main.unity first: couldn't find the game's UI in the open scene.", "OK");
                return;
            }

            int built = 0;
            var ads = Object.FindObjectOfType<AdManager>(true);
            if (ads == null)
            {
                var go = new GameObject("AdManager");
                Undo.RegisterCreatedObjectUndo(go, "Install monetization");
                go.transform.SetSiblingIndex(game.transform.GetSiblingIndex() + 1);
                ads = go.AddComponent<AdManager>();
                Set(ads, "game", game);
                built++;
            }

            var pass = Object.FindObjectOfType<ForemanPassPopup>(true);
            var offers = Object.FindObjectOfType<OfferPopup>(true);
            if (offers == null) { offers = BuildOfferPopup(safeArea.transform, ads); built++; }
            if (pass == null) { pass = BuildPassPopup(safeArea.transform, game, ads); built++; }
            Set(offers, "passPopup", pass);
            if (offers.transform.GetSiblingIndex() > pass.transform.GetSiblingIndex())
                pass.transform.SetAsLastSibling(); // the pass popup opens on top of an offer

            if (Object.FindObjectOfType<BoostButton>(true) == null) { BuildBoost(hud.transform, game, ads, offers, toasts); built++; }
            if (Object.FindObjectOfType<PassButton>(true) == null) { BuildPassButton(hud.transform, ads, pass); built++; }

            if (Object.FindObjectOfType<BonusBubble>(true) == null)
            {
                BuildBonusBubble(safeArea.transform, bottomBar != null ? bottomBar.transform : null, game, ads, offers, fx, tree, paragon);
                built++;
            }

            if (Object.FindObjectOfType<SkillAdAssist>(true) == null)
            {
                var details = tree.transform.Find("Details");
                if (details != null) { BuildSkillAssist(details, game, ads, tree); built++; }
                else Debug.LogWarning("[IdleMine] Skill tree has no 'Details' child; skipped the skill assist button.");
            }

            if (offline.transform.Find("Dim/Card/Collect x2") == null)
            {
                if (BuildOfflineDouble(offline, ads)) built++;
            }

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            string msg = built == 0
                ? "Everything is already installed. Delete a piece and run this again to rebuild it."
                : "Installed " + built + " piece(s). Save the scene (Ctrl+S) to keep them.";
            Debug.Log("[IdleMine] Monetization: " + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Idle Mine", msg, "OK");
        }

        // ================================================================== pieces

        static OfferPopup BuildOfferPopup(Transform parent, AdManager ads)
        {
            var root = Stretch("OfferPopup", parent);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.7f);
            var popup = root.gameObject.AddComponent<OfferPopup>();

            var card = Card(root, new Vector2(900, 900));
            var title = Label("Title", card, "ORE CART!", 64, Palette.Text, TopRow(-50, 90), true);
            var body = Label("Body", card, "Take the cash, or watch an ad for a much bigger haul.", 34, Palette.TextDim, TopRow(-150, 130), false);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.rectTransform.sizeDelta = new Vector2(-100, 130);
            var glow = Node("Glow", card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -380), new Vector2(170, 170));
            Img(glow, Circle, Palette.WithAlpha(Palette.Gold, 0.18f), false);
            var reward = Label("Reward", card, "$1.23M", 84, Palette.Gold, Centered(new Vector2(0, -380), new Vector2(800, 110), 1), true);
            var status = Label("Status", card, "", 28, Palette.Orange, TopRow(-470, 44), false);

            var watch = AdButton("Watch", card, "WATCH AD", Palette.Gold, Bottom(220, new Vector2(620, 140)), 52);
            Wire(watch.Button, popup.Watch);
            var alt = PlainButton("Alt", card, "NO THANKS", Palette.PanelLight, Palette.TextDim, Bottom(110, new Vector2(520, 90)), 36);
            Wire(alt, popup.Alt);
            var link = PlainButton("Pass Link", card, "No ads? Get the Foreman Pass", Palette.WithAlpha(Palette.Panel, 0), Palette.Sky, Bottom(36, new Vector2(700, 60)), 30);
            Wire(link, popup.OpenPass);

            Set(popup, "ads", ads);
            Set(popup, "card", card);
            Set(popup, "titleText", title);
            Set(popup, "bodyText", body);
            Set(popup, "rewardText", reward);
            Set(popup, "statusText", status);
            Set(popup, "watchButton", watch);
            Set(popup, "altLabel", alt.GetComponentInChildren<Text>());
            Set(popup, "passLink", link.gameObject);
            root.gameObject.SetActive(false);
            return popup;
        }

        static ForemanPassPopup BuildPassPopup(Transform parent, GameManager game, AdManager ads)
        {
            var root = Stretch("ForemanPassPopup", parent);
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            var popup = root.gameObject.AddComponent<ForemanPassPopup>();

            var card = Card(root, new Vector2(900, 860));
            var close = PlainButton("Close", card, "X", Palette.PanelLight, Palette.TextDim, null, 48);
            Place(close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(88, 88));
            Wire(close, popup.Close);

            Label("Title", card, "FOREMAN PASS", 64, Palette.Gold, TopRow(-56, 100), true);
            Label("Subtitle", card, "One-time purchase", 32, Palette.TextDim, TopRow(-150, 50), false);
            var perks = Label("Perks", card, "Skip every ad: rewards are instant\n+50% offline earnings, forever", 38, Palette.Text, TopRow(-240, 200), false);
            perks.lineSpacing = 1.25f;
            var status = Label("Status", card, "", 28, Palette.TextDim, TopRow(-470, 44), false);

            var buy = PlainButton("Buy", card, "GET IT\n<size=34>$4.99</size>", Palette.Gold, Palette.Panel, Bottom(150, new Vector2(620, 160)), 50);
            Wire(buy, popup.Buy);
            var restore = PlainButton("Restore", card, "Restore purchases", Palette.WithAlpha(Palette.Panel, 0), Palette.TextDim, Bottom(50, new Vector2(520, 80)), 30);
            Wire(restore, popup.Restore);

            Set(popup, "game", game);
            Set(popup, "ads", ads);
            Set(popup, "card", card);
            Set(popup, "perksText", perks);
            Set(popup, "statusText", status);
            Set(popup, "buyButton", buy);
            Set(popup, "buyButtonImage", buy.GetComponent<Image>());
            Set(popup, "buyLabel", buy.GetComponentInChildren<Text>());
            Set(popup, "restoreButton", restore.gameObject);
            root.gameObject.SetActive(false);
            return popup;
        }

        static void BuildBoost(Transform hud, GameManager game, AdManager ads, OfferPopup offers, Toasts toasts)
        {
            // Container stays active so its script keeps running while the button itself hides.
            var root = Node("Boost", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -22), new Vector2(230, 116));
            var boost = root.gameObject.AddComponent<BoostButton>();
            var view = AdButton("Button", root, "×2\n<size=34>BOOST</size>", Palette.Gold, null, 46);
            Place(view.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Wire(view.Button, boost.Open);

            Set(boost, "game", game);
            Set(boost, "ads", ads);
            Set(boost, "offers", offers);
            Set(boost, "toasts", toasts);
            Set(boost, "view", view);
            Set(boost, "background", view.GetComponent<Image>());
        }

        static void BuildPassButton(Transform hud, AdManager ads, ForemanPassPopup pass)
        {
            // Just left of the boost button. Container stays active so its script can show/hide the button.
            var root = Node("Pass", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-270, -22), new Vector2(150, 116));
            var passButton = root.gameObject.AddComponent<PassButton>();
            var button = PlainButton("Button", root, "PASS\n<size=24>NO ADS</size>", Palette.Sky, Palette.Panel, null, 40);
            Wire(button, pass.Open);

            Set(passButton, "ads", ads);
            Set(passButton, "button", button.gameObject);
        }

        static void BuildBonusBubble(Transform safeArea, Transform bottomBar, GameManager game, AdManager ads, OfferPopup offers,
                                     FxLayer fx, SkillTreeView tree, ParagonView paragon)
        {
            // Same rect as the Mine scroll view, so visitors stay between the HUD and the bottom bar.
            var root = Node("Bonus Visitors", safeArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(0, -440));
            if (bottomBar != null) root.SetSiblingIndex(bottomBar.GetSiblingIndex() + 1);
            var bubbles = root.gameObject.AddComponent<BonusBubble>();

            var bubble = Node("Bubble", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 180));
            var img = Img(bubble, Circle, Palette.Gold, false);
            var button = MakeButton(bubble.gameObject, img);
            Wire(button, bubbles.Tapped);
            var ring = Node("Ring", bubble, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(204, 204));
            Img(ring, Ring, Palette.WithAlpha(Color.white, 0.7f), false).raycastTarget = false;
            var label = Label("Label", bubble, "ORE\nCART", 40, Palette.Panel, Fill(), false);
            label.lineSpacing = 0.85f;

            Set(bubbles, "game", game);
            Set(bubbles, "ads", ads);
            Set(bubbles, "offers", offers);
            Set(bubbles, "fx", fx);
            Set(bubbles, "skillTree", tree);
            Set(bubbles, "paragon", paragon);
            Set(bubbles, "bubble", bubble);
            Set(bubbles, "bubbleImage", img);
            Set(bubbles, "bubbleLabel", label);
        }

        static void BuildSkillAssist(Transform details, GameManager game, AdManager ads, SkillTreeView tree)
        {
            var root = Stretch("Ad Assist", details);
            var assist = root.gameObject.AddComponent<SkillAdAssist>();
            // Sits exactly over the BUY button, which is greyed out whenever this shows.
            var view = AdButton("Button", root, "GET IT NOW\n<size=30>$15</size>", Palette.Sky, null, 40);
            Place(view.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(320, 180));
            Wire(view.Button, assist.Watch);
            view.gameObject.SetActive(false);

            Set(assist, "game", game);
            Set(assist, "ads", ads);
            Set(assist, "tree", tree);
            Set(assist, "view", view);
        }

        static bool BuildOfflineDouble(OfflinePopup offline, AdManager ads)
        {
            var card = offline.transform.Find("Dim/Card") as RectTransform;
            var collect = offline.transform.Find("Dim/Card/Collect") as RectTransform;
            if (card == null || collect == null)
            {
                Debug.LogWarning("[IdleMine] OfflinePopup has no Dim/Card/Collect; skipped the x2 button.");
                return false;
            }
            Undo.RecordObject(card, "Install monetization");
            card.sizeDelta = new Vector2(card.sizeDelta.x, card.sizeDelta.y + 170);

            var view = AdButton("Collect x2", card, "COLLECT ×2", Palette.Gold, Bottom(collect.anchoredPosition.y + collect.sizeDelta.y + 30, new Vector2(620, 140)), 52);
            view.transform.SetSiblingIndex(collect.GetSiblingIndex());
            Wire(view.Button, offline.CollectDouble);
            view.gameObject.SetActive(false);

            Set(offline, "ads", ads);
            Set(offline, "doubleButton", view);
            return true;
        }
    }
}
