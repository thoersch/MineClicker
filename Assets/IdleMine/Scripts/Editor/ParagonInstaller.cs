using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static IdleMine.EditorTools.UiKit;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Idle Mine > Paragon > Install In Open Scene.
    /// Adds the full-screen ascension celebration, the Paragon tree panel, the PARAGON TREE button on the skill
    /// tree, and a Haptics toggle in Settings (if Settings is installed). Safe to run again: pieces that already
    /// exist are left alone.
    ///
    /// Idle Mine > Install All UI In Open Scene runs every installer (monetization, audio, Paragon) in one go.
    /// </summary>
    public static class ParagonInstaller
    {
        static readonly Color Night = Palette.Hex("150D24");
        static readonly Color NightPanel = Palette.Hex("1E1430");
        static readonly Color Purple = Palette.Hex("8E5BFF");

        [MenuItem("Idle Mine/Install All UI In Open Scene", priority = 0)]
        public static void InstallAll()
        {
            Quiet = true;
            try
            {
                MonetizationInstaller.Install();
                AudioInstaller.Install();
                Install();
                DeepCoreInstaller.Install();
            }
            finally { Quiet = false; }
            const string msg = "All Idle Mine UI is installed. Save the scene (Ctrl+S) to keep any new pieces.";
            Debug.Log("[IdleMine] " + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Idle Mine", msg, "OK");
        }

        [MenuItem("Idle Mine/Paragon/Install In Open Scene")]
        public static void Install()
        {
            LoadAssets();
            var game = Object.FindObjectOfType<GameManager>(true);
            var safeArea = Object.FindObjectOfType<SafeArea>(true);
            var tree = Object.FindObjectOfType<SkillTreeView>(true);
            FxLayer mainFx = null;
            foreach (var f in Object.FindObjectsOfType<FxLayer>(true))
                if (f.transform.parent == safeArea.transform) mainFx = f;
            if (game == null || safeArea == null || tree == null || mainFx == null)
            {
                EditorUtility.DisplayDialog("Idle Mine", "Open Assets/IdleMine/Scenes/Main.unity first: couldn't find the game's UI in the open scene.", "OK");
                return;
            }

            int built = 0;
            if (Object.FindObjectOfType<AscendCelebration>(true) == null) { BuildCelebration(safeArea.transform, game, mainFx); built++; }

            var paragonTree = Object.FindObjectOfType<ParagonTreeView>(true);
            if (paragonTree == null) { paragonTree = BuildParagonTree(safeArea.transform, game, tree, mainFx); built++; }
            if (Object.FindObjectOfType<ParagonTreeButton>(true) == null) { BuildFlipButton(tree.transform, game, paragonTree); built++; }

            var settings = Object.FindObjectOfType<SettingsPopup>(true);
            if (settings != null && settings.transform.Find("Card/Haptics") == null) { AddHapticsToggle(settings); built++; }

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            string msg = built == 0
                ? "Everything is already installed. Delete a piece and run this again to rebuild it."
                : "Installed " + built + " piece(s). Save the scene (Ctrl+S) to keep them.";
            Debug.Log("[IdleMine] Paragon: " + msg);
            if (!Application.isBatchMode && !Quiet) EditorUtility.DisplayDialog("Idle Mine", msg, "OK");
        }

        // ================================================================== celebration

        static void BuildCelebration(Transform safeArea, GameManager game, FxLayer mainFx)
        {
            var root = Stretch("Ascend Celebration", safeArea);
            var celebration = root.gameObject.AddComponent<AscendCelebration>();

            var overlay = Stretch("Overlay", root);
            Img(overlay, null, Palette.WithAlpha(Night, 0.94f), false);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            var tap = overlay.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            Wire(tap, celebration.Dismiss);

            var raysSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/IdleMine/Art/Sprites/Rays.png");
            var center = new Vector2(0, 220);
            var raysBack = Node("Rays Back", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(2000, 2000));
            Img(raysBack, raysSprite, Palette.WithAlpha(Purple, 0.35f), false).raycastTarget = false;
            var rays = Node("Rays", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(1500, 1500));
            Img(rays, raysSprite, Palette.WithAlpha(Palette.Gold, 0.55f), false).raycastTarget = false;
            var glow = Node("Glow", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(720, 720));
            Img(glow, Circle, Palette.WithAlpha(Palette.Gold, 0.22f), false).raycastTarget = false;

            var titleGroup = Node("Title", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(1000, 360));
            var kicker = Label("Kicker", titleGroup, "ASCENDED", 46, Palette.Text, Centered(new Vector2(0, 110), new Vector2(1000, 70), 0.5f), true);
            var level = Label("Level", titleGroup, "PARAGON 1", 132, Palette.Gold, Centered(new Vector2(0, -10), new Vector2(1000, 170), 0.5f), true);
            var mult = Label("Multiplier", overlay, "×1  ›  ×1.25", 64, Palette.Text, Centered(new Vector2(0, -170), new Vector2(1000, 160), 0.5f), true);
            var points = Label("Points", overlay, "+1 Paragon Point  ·  your perks are kept  ·  1 to spend", 34, Palette.Hex("C9A8FF"), Centered(new Vector2(0, -330), new Vector2(980, 60), 0.5f), false);
            var hint = Label("Tap Hint", overlay, "TAP TO CONTINUE", 38, Palette.Text, Bottom(150, new Vector2(800, 60)), false);

            var fxRoot = Stretch("Celebration Fx", overlay);
            var fx = fxRoot.gameObject.AddComponent<FxLayer>();
            CopyFxPrefabs(mainFx, fx);

            Set(celebration, "game", game);
            Set(celebration, "overlay", group);
            Set(celebration, "fx", fx);
            Set(celebration, "rays", rays);
            Set(celebration, "raysBack", raysBack);
            Set(celebration, "titleGroup", titleGroup);
            Set(celebration, "kickerText", kicker);
            Set(celebration, "levelText", level);
            Set(celebration, "multiplierText", mult);
            Set(celebration, "pointsText", points);
            Set(celebration, "tapHint", hint);
            overlay.gameObject.SetActive(false);
        }

        static void CopyFxPrefabs(FxLayer from, FxLayer to)
        {
            var src = new SerializedObject(from);
            Set(to, "floatingTextPrefab", src.FindProperty("floatingTextPrefab").objectReferenceValue);
            Set(to, "chipPrefab", src.FindProperty("chipPrefab").objectReferenceValue);
        }

        // ================================================================== Paragon tree panel

        static ParagonTreeView BuildParagonTree(Transform safeArea, GameManager game, SkillTreeView skillTree, FxLayer mainFx)
        {
            var root = Stretch("ParagonTree", safeArea);
            Img(root, null, Night, false);
            var view = root.gameObject.AddComponent<ParagonTreeView>();

            // Same footprint as the skill tree's viewport: between the header and the details sheet.
            var board = Node("Board", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(0, -500));
            var edges = Node("Edges", board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var nodes = Node("Nodes", board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var selection = Node("Selection", board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            Img(selection, Ring, Palette.Gold, false).raycastTarget = false;
            selection.gameObject.SetActive(false);

            // Header
            var header = Node("Header", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 170));
            Img(header, null, NightPanel, false);
            var title = Label("Title", header, "PARAGON TREE", 56, Palette.Gold, Corner(new Vector2(36, -22), new Vector2(520, 70)), true);
            title.alignment = TextAnchor.UpperLeft;
            var sub = Label("Subtitle", header, "Perks stay when you ascend  ·  respec free", 26, Palette.TextDim, Corner(new Vector2(38, -98), new Vector2(620, 40)), false);
            sub.alignment = TextAnchor.UpperLeft;
            var pts = Label("Points", header, "0 / 0 POINTS", 48, Palette.Gold, new Layout { AnchorMin = new Vector2(1, 0.5f), AnchorMax = new Vector2(1, 0.5f), Pivot = new Vector2(1, 0.5f), Pos = new Vector2(-160, 0), Size = new Vector2(380, 80) }, true);
            pts.alignment = TextAnchor.MiddleRight;
            var close = PlainButton("Close", header, "X", Palette.PanelLight, Palette.Text, null, 54);
            Place(close.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(110, 110));
            Wire(close, view.CloseAll);

            // Details sheet
            var details = Node("Details", root, Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 330));
            Img(details, null, NightPanel, false);
            var nameT = Label("Name", details, "Extra Hands", 44, Palette.Gold, Corner(new Vector2(36, -24), new Vector2(660, 56)), true);
            nameT.alignment = TextAnchor.UpperLeft;
            var kind = Label("Kind", details, "PERK", 24, Palette.TextDim, Corner(new Vector2(38, -80), new Vector2(660, 34)), false);
            kind.alignment = TextAnchor.UpperLeft;
            var desc = Label("Description", details, "+2 Miners", 34, Palette.Text, Corner(new Vector2(38, -124), new Vector2(640, 130)), false);
            desc.alignment = TextAnchor.UpperLeft;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var status = Label("Status", details, "", 26, Palette.TextDim, new Layout { AnchorMin = Vector2.zero, AnchorMax = Vector2.zero, Pivot = Vector2.zero, Pos = new Vector2(38, 24), Size = new Vector2(660, 40) }, false);
            status.alignment = TextAnchor.LowerLeft;
            var buy = PlainButton("Buy", details, "UNLOCK", Palette.Gold, Palette.Panel, null, 40);
            Place(buy.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(320, 180));
            Wire(buy, view.Buy);

            // Flip back
            var back = PlainButton("Skill Tree Button", root, "SKILL TREE", Palette.PanelLight, Palette.Text, null, 36);
            Place(back.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 350), new Vector2(250, 90));
            Wire(back, view.FlipBack);

            var fxRoot = Stretch("Paragon Fx", root);
            var fx = fxRoot.gameObject.AddComponent<FxLayer>();
            CopyFxPrefabs(mainFx, fx);

            Set(view, "game", game);
            Set(view, "skillTree", skillTree);
            Set(view, "fx", fx);
            Set(view, "font", LabelFont);
            Set(view, "rounded", Rounded);
            Set(view, "ring", Ring);
            Set(view, "board", board);
            Set(view, "edgesLayer", edges);
            Set(view, "nodesLayer", nodes);
            Set(view, "selection", selection);
            Set(view, "pointsText", pts);
            Set(view, "nameText", nameT);
            Set(view, "kindText", kind);
            Set(view, "descriptionText", desc);
            Set(view, "statusText", status);
            Set(view, "buyButton", buy);
            Set(view, "buyButtonImage", buy.GetComponent<Image>());
            Set(view, "buyLabel", buy.GetComponentInChildren<Text>());

            // Sits right after the skill tree so it covers it when flipped in.
            root.SetSiblingIndex(skillTree.transform.GetSiblingIndex() + 1);
            root.gameObject.SetActive(false);
            return view;
        }

        static Layout Corner(Vector2 pos, Vector2 size)
        {
            return new Layout { AnchorMin = new Vector2(0, 1), AnchorMax = new Vector2(0, 1), Pivot = new Vector2(0, 1), Pos = pos, Size = size };
        }

        static void BuildFlipButton(Transform skillTree, GameManager game, ParagonTreeView paragonTree)
        {
            // Bottom-left of the tree area, just above the details sheet. Container stays active for its script.
            var root = Node("Paragon Tree Button", skillTree, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 350), new Vector2(290, 100));
            var comp = root.gameObject.AddComponent<ParagonTreeButton>();
            var button = PlainButton("Button", root, "PARAGON TREE\n<size=26>1 point to spend</size>", Purple, Palette.Text, null, 34);
            Wire(button, paragonTree.FlipIn);
            Set(comp, "game", game);
            Set(comp, "button", button.gameObject);
            Set(comp, "label", button.GetComponentInChildren<Text>());
        }

        // ================================================================== settings

        static void AddHapticsToggle(SettingsPopup settings)
        {
            var card = settings.transform.Find("Card");
            var music = card.Find("Music");
            var sound = card.Find("Sound");
            if (music == null || sound == null) return;
            var size = new Vector2(230, 160);
            Place(music, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-250, -300), size);
            Place(sound, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -300), size);
            music.GetComponentInChildren<Text>().fontSize = 40;
            sound.GetComponentInChildren<Text>().fontSize = 40;

            var haptics = PlainButton("Haptics", card, "HAPTICS\n<size=30>ON</size>", Palette.Green, Palette.Panel, Centered(new Vector2(250, -300), size, 1), 40);
            haptics.transform.SetSiblingIndex(sound.GetSiblingIndex() + 1);
            Wire(haptics, settings.ToggleHaptics);
            Set(settings, "hapticsImage", haptics.GetComponent<Image>());
            Set(settings, "hapticsLabel", haptics.GetComponentInChildren<Text>());
        }
    }
}
