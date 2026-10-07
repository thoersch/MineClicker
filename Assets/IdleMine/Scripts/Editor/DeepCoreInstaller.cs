using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static IdleMine.EditorTools.UiKit;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Idle Mine > Deep Core > Install In Open Scene.
    /// Adds the Deep Core panel (a molten-themed copy of the skill tree panel, hexagon nodes), BUY ALL and DEEP CORE
    /// buttons on the skill tree, and the views for the Deep Core mechanics: dynamite, overclock, gem veins, power
    /// swings and drill rigs. Also adds the new sounds to the AudioManager. Safe to run again.
    /// </summary>
    public static class DeepCoreInstaller
    {
        const string Sprites = "Assets/IdleMine/Art/Sprites/";
        static readonly Color Obsidian = Palette.Hex("140B08");
        static readonly Color ObsidianPanel = Palette.Hex("26130C");
        static readonly Color Molten = Palette.Hex("FF7A3D");
        static readonly Color Overdrive = Palette.Hex("FF4F8B");
        static readonly Color Cyan = Palette.Hex("4DE1E8");

        [MenuItem("Idle Mine/Deep Core/Install In Open Scene")]
        public static void Install()
        {
            LoadAssets();
            var game = Object.FindObjectOfType<GameManager>(true);
            var safeArea = Object.FindObjectOfType<SafeArea>(true);
            var mine = Object.FindObjectOfType<MineView>(true);
            SkillTreeView skillTree = null, deepCore = null;
            foreach (var v in Object.FindObjectsOfType<SkillTreeView>(true))
            {
                if (new SerializedObject(v).FindProperty("deepCore").boolValue) deepCore = v; else skillTree = v;
            }
            FxLayer fx = null;
            foreach (var f in Object.FindObjectsOfType<FxLayer>(true))
                if (f.transform.parent == safeArea.transform) fx = f;
            if (game == null || safeArea == null || mine == null || skillTree == null || fx == null)
            {
                EditorUtility.DisplayDialog("Idle Mine", "Open Assets/IdleMine/Scenes/Main.unity first: couldn't find the game's UI in the open scene.", "OK");
                return;
            }
            var paragon = Object.FindObjectOfType<ParagonView>(true);
            var offers = Object.FindObjectOfType<OfferPopup>(true);

            int built = 0;
            if (deepCore == null) { deepCore = BuildDeepCorePanel(skillTree); built++; }

            var extras = Object.FindObjectOfType<SkillTreeExtras>(true);
            if (extras == null) { BuildExtras(safeArea.transform, game, skillTree, deepCore, fx); built++; }

            // Mine overlays sit just above the mine (and the bonus visitors), below the fx layer and popups.
            Transform after = safeArea.transform.Find("Bonus Visitors") ?? mine.transform;
            var drills = Object.FindObjectOfType<DrillRigsView>(true);
            if (drills == null) { PlaceAfter(BuildDrills(safeArea.transform, game, fx), after); built++; }
            else if (UpdateDrillArt(drills, fx)) built++; // swap the placeholder gear for the animated drill
            if (Object.FindObjectOfType<GemVeins>(true) == null) { PlaceAfter(BuildGems(safeArea.transform, game, fx, skillTree, deepCore, paragon, offers), after); built++; }

            // The power swing was redesigned; replace the first version's layout rather than leaving it.
            var swing = Object.FindObjectOfType<PowerSwingView>(true);
            if (swing != null && IsLegacySwing(swing))
            {
                var slot = swing.transform.GetSiblingIndex();
                Object.DestroyImmediate(swing.gameObject);
                BuildSwing(safeArea.transform, game, fx, skillTree, deepCore, paragon, offers, (RectTransform)mine.transform).SetSiblingIndex(slot);
                built++;
            }
            else if (swing == null) { PlaceAfter(BuildSwing(safeArea.transform, game, fx, skillTree, deepCore, paragon, offers, (RectTransform)mine.transform), after); built++; }
            if (Object.FindObjectOfType<OverclockButton>(true) == null) { PlaceAfter(BuildOverclock(safeArea.transform, game), after); built++; }
            if (Object.FindObjectOfType<DynamiteView>(true) == null) { PlaceAfter(BuildDynamite(safeArea.transform, game, fx, (RectTransform)mine.transform), after); built++; }

            var audio = Object.FindObjectOfType<AudioManager>(true);
            if (audio != null && AudioInstaller.EnsureSounds(audio) > 0) built++;

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            string msg = built == 0
                ? "Everything is already installed. Delete a piece and run this again to rebuild it."
                : "Installed " + built + " piece(s). Save the scene (Ctrl+S) to keep them.";
            Debug.Log("[IdleMine] Deep Core: " + msg);
            if (!Application.isBatchMode && !Quiet) EditorUtility.DisplayDialog("Idle Mine", msg, "OK");
        }

        static void PlaceAfter(Transform t, Transform after)
        {
            t.SetSiblingIndex(after.GetSiblingIndex() + 1);
        }

        static Layout MineRect()
        {
            return new Layout { AnchorMin = Vector2.zero, AnchorMax = Vector2.one, Pivot = new Vector2(0.5f, 0.5f), Pos = new Vector2(0, -30), Size = new Vector2(0, -440) };
        }

        static Image Center(string name, Transform parent, Sprite sprite, Color color, float size)
        {
            var rt = Node(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var img = Img(rt, sprite, color, false);
            img.raycastTarget = false;
            return img;
        }

        // ================================================================== Deep Core panel

        static SkillTreeView BuildDeepCorePanel(SkillTreeView skillTree)
        {
            var go = (GameObject)Object.Instantiate(skillTree.gameObject, skillTree.transform.parent);
            Undo.RegisterCreatedObjectUndo(go, "Install Deep Core");
            go.name = "DeepCoreTree";
            go.transform.SetSiblingIndex(skillTree.transform.GetSiblingIndex() + 1);

            // The copy shouldn't carry the normal tree's own extras.
            foreach (var b in go.GetComponentsInChildren<ParagonTreeButton>(true)) Object.DestroyImmediate(b.gameObject);
            foreach (string n in new[] { "Buy All", "Deep Core Button" })
            {
                var t = go.transform.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            var view = go.GetComponent<SkillTreeView>();
            var so = new SerializedObject(view);
            so.FindProperty("deepCore").boolValue = true;
            so.FindProperty("nodeShape").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Hex.png");
            so.FindProperty("nodeRingShape").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "HexRing.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Molten theme.
            var bg = go.GetComponent<Image>();
            if (bg != null) bg.color = Obsidian;
            foreach (string n in new[] { "Header", "Details" })
            {
                var t = go.transform.Find(n);
                if (t != null && t.GetComponent<Image>() != null) t.GetComponent<Image>().color = ObsidianPanel;
            }
            var title = go.transform.Find("Header/Title");
            if (title != null)
            {
                var text = title.GetComponent<Text>();
                text.text = "DEEP CORE";
                text.color = Molten;
            }
            go.SetActive(false);
            return view;
        }

        static void BuildExtras(Transform safeArea, GameManager game, SkillTreeView skillTree, SkillTreeView deepCore, FxLayer fx)
        {
            // Lives outside both panels so its flip animation survives the panels switching off.
            var host = Node("Skill Tree Extras", safeArea, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var extras = host.gameObject.AddComponent<SkillTreeExtras>();

            var buyAll = PlainButton("Buy All", skillTree.transform, "BUY ALL\n<size=26>every affordable skill</size>", Palette.Gold, Palette.Panel, null, 36);
            Place(buyAll.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 350), new Vector2(280, 100));
            Wire(buyAll, extras.BuyAll);

            var deep = PlainButton("Deep Core Button", skillTree.transform, "DEEP CORE", Molten, Palette.Panel, null, 34);
            Place(deep.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(334, 350), new Vector2(290, 100));
            Wire(deep, extras.OpenDeepCore);

            var back = PlainButton("Skill Tree Button", deepCore.transform, "SKILL TREE", Palette.PanelLight, Palette.Text, null, 36);
            Place(back.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 350), new Vector2(250, 100));
            Wire(back, extras.BackToSkillTree);

            Set(extras, "game", game);
            Set(extras, "skillTree", skillTree);
            Set(extras, "deepCore", deepCore);
            Set(extras, "fx", fx);
            Set(extras, "buyAllButton", buyAll.gameObject);
            Set(extras, "deepCoreButton", deep.gameObject);
            Set(extras, "deepCoreLabel", deep.GetComponentInChildren<Text>());
        }

        // ================================================================== mechanics

        static Transform BuildDynamite(Transform safeArea, GameManager game, FxLayer fx, RectTransform mine)
        {
            var root = Stretch("Dynamite", safeArea);
            var view = root.gameObject.AddComponent<DynamiteView>();
            var glow = Center("Fuse Glow", root, Circle, Palette.WithAlpha(Palette.Orange, 0.55f), 150);
            var ring = Center("Fuse Ring", root, Ring, Palette.Gold, 180);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            var flash = Center("Flash", root, Circle, Color.white, 320);
            var shock = Center("Shockwave", root, Ring, Palette.Orange, 320);
            var pill = Node("Status", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24, 214), new Vector2(340, 60));
            Img(pill, Rounded, Palette.WithAlpha(Palette.Panel, 0.92f), true).raycastTarget = false;
            var status = Label("Label", pill, "TNT READY", 30, Palette.Orange, Fill(), false);

            Set(view, "game", game);
            Set(view, "fx", fx);
            Set(view, "shakeTarget", mine);
            Set(view, "fuseRing", ring);
            Set(view, "fuseGlow", glow);
            Set(view, "flash", flash);
            Set(view, "shockwave", shock);
            Set(view, "statusRoot", pill.gameObject);
            Set(view, "statusText", status);
            return root;
        }

        static Transform BuildOverclock(Transform safeArea, GameManager game)
        {
            var glowRt = Stretch("Overclock Glow", safeArea);
            var glow = Img(glowRt, Rounded, Palette.WithAlpha(Overdrive, 0.5f), true);
            glow.fillCenter = false;
            glow.raycastTarget = false;
            glowRt.gameObject.SetActive(false);

            var root = Node("Overclock", safeArea, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 320), new Vector2(150, 150));
            var comp = root.gameObject.AddComponent<OverclockButton>();
            var buttonRt = Stretch("Button", root);
            var bg = Img(buttonRt, Circle, Palette.PanelLight, false);
            var button = MakeButton(buttonRt.gameObject, bg);
            Wire(button, comp.Activate);
            var meter = Center("Meter", buttonRt, Ring, Overdrive, 150);
            meter.type = Image.Type.Filled;
            meter.fillMethod = Image.FillMethod.Radial360;
            meter.fillOrigin = (int)Image.Origin360.Top;
            var label = Label("Label", buttonRt, "OVER\nCLOCK\n<size=24>0%</size>", 26, Palette.Text, Fill(), false);
            label.lineSpacing = 0.85f;

            Set(comp, "game", game);
            Set(comp, "root", buttonRt.gameObject);
            Set(comp, "button", button);
            Set(comp, "background", bg);
            Set(comp, "meter", meter);
            Set(comp, "label", label);
            Set(comp, "edgeGlow", glow);
            glowRt.SetSiblingIndex(root.GetSiblingIndex());
            return root;
        }

        static Transform BuildGems(Transform safeArea, GameManager game, FxLayer fx, SkillTreeView tree, SkillTreeView deep, ParagonView paragon, OfferPopup offers)
        {
            var root = Node("Gem Veins", safeArea, MineRect());
            var comp = root.gameObject.AddComponent<GemVeins>();
            var gem = Node("Gem", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            var hit = Img(gem, null, new Color(0, 0, 0, 0), false);
            var button = gem.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            Wire(button, comp.Collect);
            var glint = Center("Glint", gem, AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Rays.png"), Palette.WithAlpha(Color.white, 0.6f), 240);
            var diamond = Center("Diamond", gem, Rounded, Cyan, 86);
            diamond.type = Image.Type.Sliced;
            diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            var shine = Center("Shine", gem, Circle, Palette.WithAlpha(Color.white, 0.85f), 26);
            shine.rectTransform.anchoredPosition = new Vector2(-14, 16);

            Set(comp, "game", game);
            Set(comp, "fx", fx);
            Set(comp, "gem", gem);
            Set(comp, "glint", glint.rectTransform);
            Set(comp, "skillTree", tree);
            Set(comp, "deepCore", deep);
            Set(comp, "paragon", paragon);
            Set(comp, "offers", offers);
            return root;
        }

        static Image Free(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size, bool sliced)
        {
            var rt = Node(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = Img(rt, sprite, color, sliced);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>True for the first power swing layout (before the catcher, zones and payoff were added).</summary>
        static bool IsLegacySwing(PowerSwingView view)
        {
            var p = new SerializedObject(view).FindProperty("catcher");
            return p == null || p.objectReferenceValue == null;
        }

        static Transform BuildSwing(Transform safeArea, GameManager game, FxLayer fx, SkillTreeView tree, SkillTreeView deep, ParagonView paragon, OfferPopup offers, RectTransform mine)
        {
            var root = Node("Power Swing", safeArea, MineRect());
            var comp = root.gameObject.AddComponent<PowerSwingView>();
            var pick = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Pick.png");

            // While a swing is live, tapping anywhere on the mine swings.
            var catcherRt = Stretch("Catcher", root);
            var catcherImg = Img(catcherRt, null, new Color(0, 0, 0, 0.25f), false);
            var catcher = catcherRt.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.targetGraphic = catcherImg;
            Wire(catcher, comp.Strike);

            var panel = Node("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(920, 400));
            var bg = Img(panel, Rounded, Palette.WithAlpha(Palette.Panel, 0.97f), true);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            var button = panel.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;
            Wire(button, comp.Strike);
            var outline = Free("Glow Edge", panel, Rounded, Palette.WithAlpha(Palette.Gold, 0.6f), Vector2.zero, new Vector2(928, 408), true);
            outline.fillCenter = false;
            outline.transform.SetAsFirstSibling();

            var title = Label("Title", panel, "POWER SWING!", 58, Palette.Gold, TopRow(-20, 76), true);
            var barRt = Node("Bar", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(800, 84));
            var barImg = Img(barRt, Rounded, Palette.PanelLight, true);
            barImg.raycastTarget = false;
            var good = Free("Good Zone", barRt, Rounded, Palette.WithAlpha(Palette.Orange, 0.55f), Vector2.zero, new Vector2(160, 84), true);
            var perfect = Free("Perfect Zone", good.rectTransform, Rounded, Palette.Gold, Vector2.zero, new Vector2(80, 84), true);
            var trail = new RectTransform[3];
            for (int i = trail.Length - 1; i >= 0; i--)
                trail[i] = Free("Trail " + (i + 1), barRt, pick, Palette.WithAlpha(Palette.Text, 0.32f - 0.09f * i), new Vector2(0, 14), new Vector2(104, 104), false).rectTransform;
            var marker = Free("Pick", barRt, pick, Color.white, new Vector2(0, 14), new Vector2(116, 116), false).rectTransform;

            var hint = Label("Hint", panel, "TAP ANYWHERE when the pick is in the GOLD", 30, Palette.Text, Bottom(54, new Vector2(880, 56)), false);
            var timerBg = Free("Timer", panel, Rounded, Palette.WithAlpha(Palette.PanelLight, 0.9f), new Vector2(0, -170), new Vector2(800, 16), true);
            var timer = Free("Fill", timerBg.rectTransform, Rounded, Palette.Gold, Vector2.zero, Vector2.zero, false);
            Place(timer.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            timer.type = Image.Type.Filled;
            timer.fillMethod = Image.FillMethod.Horizontal;

            // Payoff layer above the panel.
            var flashRt = Stretch("Flash", root);
            var flash = Img(flashRt, null, Color.white, false);
            flash.raycastTarget = false;
            var burst = Free("Burst", root, AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Rays.png"), Palette.Gold, Vector2.zero, new Vector2(900, 900), false);
            var ring = Free("Ring", root, Ring, Palette.Text, Vector2.zero, new Vector2(360, 360), false);
            var result = Label("Result", root, "PERFECT!", 132, Palette.Gold, Centered(new Vector2(0, 330), new Vector2(1000, 170), 0.5f), true);
            var sub = Label("Result Amount", root, "+$0", 64, Palette.Text, Centered(new Vector2(0, 220), new Vector2(1000, 90), 0.5f), true);

            Set(comp, "game", game);
            Set(comp, "fx", fx);
            Set(comp, "shakeTarget", mine);
            Set(comp, "catcher", catcherRt.gameObject);
            Set(comp, "panel", group);
            Set(comp, "bar", barRt);
            Set(comp, "barImage", barImg);
            Set(comp, "goodZone", good.rectTransform);
            Set(comp, "perfectZone", perfect.rectTransform);
            Set(comp, "perfectImage", perfect);
            Set(comp, "marker", marker);
            var so = new SerializedObject(comp);
            var arr = so.FindProperty("trail");
            arr.arraySize = trail.Length;
            for (int i = 0; i < trail.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = trail[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(comp, "timerFill", timer);
            Set(comp, "titleText", title);
            Set(comp, "hintText", hint);
            Set(comp, "flash", flash);
            Set(comp, "burst", burst.rectTransform);
            Set(comp, "burstImage", burst);
            Set(comp, "ring", ring.rectTransform);
            Set(comp, "ringImage", ring);
            Set(comp, "resultText", result);
            Set(comp, "resultSub", sub);
            Set(comp, "skillTree", tree);
            Set(comp, "deepCore", deep);
            Set(comp, "paragon", paragon);
            Set(comp, "offers", offers);
            return root;
        }

        static Transform BuildDrills(Transform safeArea, GameManager game, FxLayer fx)
        {
            var root = Node("Drill Rigs", safeArea, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var comp = root.gameObject.AddComponent<DrillRigsView>();
            Set(comp, "game", game);
            UpdateDrillArt(comp, fx);
            return root;
        }

        /// <summary>Gives a drill view the animated drill frames (replacing the first, gear-based placeholder).
        /// Returns true if anything changed.</summary>
        static bool UpdateDrillArt(DrillRigsView comp, FxLayer fx)
        {
            var so = new SerializedObject(comp);
            var frames = so.FindProperty("frames");
            if (frames.arraySize == 4 && frames.GetArrayElementAtIndex(0).objectReferenceValue != null) return false;
            frames.arraySize = 4;
            for (int i = 0; i < 4; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Drill_" + i + ".png");
            so.FindProperty("fx").objectReferenceValue = fx;
            so.FindProperty("drillColor").colorValue = Color.white;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
