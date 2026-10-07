#if UNITY_EDITOR || DEVELOPMENT_BUILD || IDLEMINE_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Testing shortcuts: a small red DBG tab on the left edge opens a panel of buttons (free money, buy every
    /// skill, unlock the Deep Core, Paragon levels, spawn bonuses, skip cooldowns, fake time away, game speed,
    /// toggle the Foreman Pass, wipe the save).
    ///
    /// Only compiled into the editor, development builds and builds with the IDLEMINE_DEBUG define (the GitHub
    /// workflow's "debug tools" checkbox), so it can never ship to the App Store by accident. Built entirely in
    /// code on its own canvas at startup, so it needs nothing in the scene; it survives Wipe & Restart.
    /// </summary>
    public class DebugMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<DebugMenu>() != null) return;
            var go = new GameObject("Debug Menu");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugMenu>();
        }

        Font _font;
        Sprite _rounded;
        GameObject _panel;
        Text _status, _speedLabel;
        float _statusTimer;
        static readonly float[] Speeds = { 1f, 5f, 20f };
        int _speed;

        static GameManager Game { get { return GameManager.Instance; } }

        void Start()
        {
            foreach (var t in FindObjectsOfType<Text>(true))
                if (t.font != null && t.font.name.Contains("Lilita")) { _font = t.font; break; }
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var img in FindObjectsOfType<Image>(true))
                if (img.sprite != null && img.sprite.name == "Rounded") { _rounded = img.sprite; break; }
            Build();
        }

        // ================================================================== actions

        List<KeyValuePair<string, List<KeyValuePair<string, Action>>>> Sections()
        {
            var s = new List<KeyValuePair<string, List<KeyValuePair<string, Action>>>>();
            Action<string> section = name => s.Add(new KeyValuePair<string, List<KeyValuePair<string, Action>>>(name, new List<KeyValuePair<string, Action>>()));
            Action<string, Action> add = (label, act) => s[s.Count - 1].Value.Add(new KeyValuePair<string, Action>(label, act));

            section("MONEY");
            add("+1 HOUR\nOF INCOME", () => { var g = Game; g.DebugGrantMoney(g.IncomeForMinutes(60)); Say("+1 hour of income"); });
            add("CASH ×1000", () => { var g = Game; g.DebugGrantMoney(Math.Max(1000, g.Money * 999)); Say("Cash x1000"); });
            add("+$1e30", () => { Game.DebugGrantMoney(1e30); Say("+$1e30"); });

            section("SKILLS");
            add("BUY ALL\nSKILLS (FREE)", () => { Game.DebugUnlockTree(Game.Tree); Say("Every skill owned"); });
            add("UNLOCK\nDEEP CORE", () => { Game.DebugUnlockDeepCore(); Say("Deep Core unlocked"); });
            add("BUY ALL\nDEEP CORE (FREE)", () => { Game.DebugUnlockTree(Game.DeepTree); Say("Every Deep Core node owned"); });

            section("PARAGON");
            add("READY TO\nASCEND", () => { Game.DebugFillParagonProgress(); Say("Next Paragon level ready"); });
            add("ASCEND\nNOW", () => { Game.DebugFillParagonProgress(); Close(); Game.Ascend(); });
            add("+1 PARAGON\n(NO RESET)", () => { Game.DebugAddParagonLevels(1); Say("Paragon " + Game.ParagonLevel); });
            add("+5 PARAGON\n(NO RESET)", () => { Game.DebugAddParagonLevels(5); Say("Paragon " + Game.ParagonLevel); });

            section("DEEP CORE");
            add("SPAWN\nGEM", () => { var v = FindObjectOfType<GemVeins>(true); if (v != null && Game.GemsUnlocked) { v.ForceSpawn(); Close(); } else Say("Buy Gem Sense first"); });
            add("POWER\nSWING", () => { var v = FindObjectOfType<PowerSwingView>(true); if (v != null && Game.SwingsUnlocked) { v.ForceShow(); Close(); } else Say("Buy Power Swing first"); });
            add("FILL\nOVERCLOCK", () => { Game.DebugFillOverclock(); Say(Game.OverclockUnlocked ? "Overclock ready" : "Buy Overclock first"); });
            add("RESET TNT\nCOOLDOWN", () => { Game.DebugResetDynamite(); Say(Game.DynamiteUnlocked ? "Dynamite ready" : "Buy Light the Fuse first"); });

            section("WORLD");
            add("ORE\nCART", () => { var v = FindObjectOfType<BonusBubble>(true); if (v != null) { v.ForceSpawn(false); Close(); } });
            add("MOTHERLODE\nCHEST", () => { var v = FindObjectOfType<BonusBubble>(true); if (v != null) { v.ForceSpawn(true); Close(); } });
            add("1 HOUR\nAWAY", () => { Game.DebugSimulateOffline(3600); Close(); });
            add("SPEED", () => { _speed = (_speed + 1) % Speeds.Length; Time.timeScale = Speeds[_speed]; Say("Game speed x" + Speeds[_speed]); });

            section("META");
            add("TOGGLE\nFOREMAN PASS", () => { var a = FindObjectOfType<AdManager>(); if (a != null) { a.DebugTogglePass(); Say("Foreman Pass " + (Game.ForemanPass ? "on" : "off")); } });
            add("RESET AD CAPS\n& COOLDOWNS", () => { var a = FindObjectOfType<AdManager>(); if (a != null) { a.DebugResetCaps(); Say("Ad caps and cooldowns reset"); } });
            add("WIPE SAVE\n& RESTART", () => { Close(); Time.timeScale = 1f; _speed = 0; Game.DebugWipeAndRestart(); });
            return s;
        }

        void Say(string msg)
        {
            _status.text = msg;
            _statusTimer = 3f;
        }

        void Update()
        {
            if (_statusTimer > 0f)
            {
                _statusTimer -= Time.unscaledDeltaTime;
                if (_statusTimer <= 0f) _status.text = "";
            }
            if (_speedLabel != null) _speedLabel.text = "SPEED\n×" + Speeds[_speed];
        }

        void Open() { _panel.SetActive(true); _status.text = Summary(); }
        void Close() { _panel.SetActive(false); }

        string Summary()
        {
            var g = Game;
            if (g == null) return "";
            return "Paragon " + g.ParagonLevel + "  ·  skills " + g.Tree.UnlockedCount + "/" + g.Tree.Nodes.Count
                   + "  ·  Deep Core " + (g.DeepCoreUnlocked ? g.DeepTree.UnlockedCount + "/" + g.DeepTree.Nodes.Count : "locked");
        }

        // ================================================================== UI (built in code)

        void Build()
        {
            var canvasGo = new GameObject("Debug Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            var root = (RectTransform)canvasGo.transform;

            // The tab
            var tab = MakeButton(root, "DBG", Palette.WithAlpha(Palette.Red, 0.85f), Palette.Text, 26, Open);
            Place(tab, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 120), new Vector2(84, 64));

            // The panel
            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panel = (RectTransform)_panel.transform;
            panel.SetParent(root, false);
            Place(panel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _panel.GetComponent<Image>().color = new Color(0.04f, 0.03f, 0.06f, 0.93f);

            var title = MakeText(panel, "DEBUG TOOLS", 52, Palette.Red);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(0, 70));
            var close = MakeButton(panel, "X", Palette.PanelLight, Palette.Text, 44, Close);
            Place(close, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -140), new Vector2(96, 96));

            const float colW = 320f, rowH = 104f, gap = 14f;
            float y = -260f;
            foreach (var sec in Sections())
            {
                var head = MakeText(panel, sec.Key, 28, Palette.TextDim);
                head.alignment = TextAnchor.MiddleLeft;
                Place(head.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(colW * 3 + gap * 2, 40));
                y -= 46f;
                for (int i = 0; i < sec.Value.Count; i++)
                {
                    int col = i % 3, row = i / 3;
                    var item = sec.Value[i];
                    var btn = MakeButton(panel, item.Key, Palette.PanelLight, Palette.Text, 28, item.Value);
                    float x = (col - 1) * (colW + gap);
                    Place(btn, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, y - row * (rowH + gap)), new Vector2(colW, rowH));
                    if (item.Key == "SPEED") _speedLabel = btn.GetComponentInChildren<Text>();
                }
                y -= ((sec.Value.Count + 2) / 3) * (rowH + gap) + 16f;
            }

            _status = MakeText(panel, "", 30, Palette.Gold);
            Place(_status.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(-60, 90));
            _panel.SetActive(false);
        }

        static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        Text MakeText(Transform parent, string s, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = s;
            return t;
        }

        RectTransform MakeButton(Transform parent, string label, Color bg, Color fg, int size, Action onClick)
        {
            var go = new GameObject("Button " + label.Replace("\n", " "), typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _rounded;
            img.type = _rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = bg;
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                try { onClick(); }
                catch (Exception e) { Debug.LogException(e); if (_status != null) Say("Error: " + e.Message); }
            });
            var t = MakeText(rt, label, size, fg);
            Place(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }
    }
}
#endif
