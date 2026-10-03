using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static IdleMine.EditorTools.UiKit;

namespace IdleMine.EditorTools
{
    /// <summary>
    /// Idle Mine > Audio > Install In Open Scene.
    /// Adds the Audio object (AudioManager with every clip from Assets/IdleMine/Audio, plus GameAudio), a
    /// gear button in the HUD and the settings popup with music/sound toggles. Safe to run again: pieces
    /// that already exist are left alone.
    /// </summary>
    public static class AudioInstaller
    {
        const string AudioRoot = "Assets/IdleMine/Audio/";

        struct Def
        {
            public Sfx Id; public string[] Files; public float Volume, Jitter, Interval;
            public Def(Sfx id, float volume, float jitter, float interval, params string[] files)
            { Id = id; Files = files; Volume = volume; Jitter = jitter; Interval = interval; }
        }

        // Starting mix. Tweak per sound on the AudioManager component afterwards.
        static readonly Def[] Defaults =
        {
            new Def(Sfx.Click, 0.35f, 0.05f, 0.03f, "sfx_click"),
            new Def(Sfx.Dig, 0.5f, 0.08f, 0.035f, "sfx_dig_1", "sfx_dig_2", "sfx_dig_3"),
            new Def(Sfx.Crit, 0.7f, 0.04f, 0.05f, "sfx_crit"),
            new Def(Sfx.Coins, 0.7f, 0f, 0.15f, "sfx_coins"),
            new Def(Sfx.Purchase, 0.55f, 0.06f, 0.05f, "sfx_purchase"),
            new Def(Sfx.Keystone, 0.8f, 0f, 0.3f, "sfx_keystone"),
            new Def(Sfx.Deny, 0.5f, 0f, 0.15f, "sfx_deny"),
            new Def(Sfx.Breakthrough, 0.7f, 0f, 1f, "sfx_breakthrough"),
            new Def(Sfx.Motherlode, 0.9f, 0f, 1f, "sfx_motherlode"),
            new Def(Sfx.Reward, 0.7f, 0f, 0.3f, "sfx_reward"),
            new Def(Sfx.Cart, 0.55f, 0f, 0.5f, "sfx_cart"),
            new Def(Sfx.Ascend, 0.9f, 0f, 1f, "sfx_ascend"),
        };

        [MenuItem("Idle Mine/Audio/Install In Open Scene")]
        public static void Install()
        {
            LoadAssets();
            var game = Object.FindObjectOfType<GameManager>(true);
            var ads = Object.FindObjectOfType<AdManager>(true);
            var safeArea = Object.FindObjectOfType<SafeArea>(true);
            var hud = Object.FindObjectOfType<HudView>(true);
            if (game == null || safeArea == null || hud == null)
            {
                EditorUtility.DisplayDialog("Idle Mine", "Open Assets/IdleMine/Scenes/Main.unity first: couldn't find the game's UI in the open scene.", "OK");
                return;
            }

            int built = 0;
            if (Object.FindObjectOfType<AudioManager>(true) == null) { BuildAudio(game, ads); built++; }

            var settings = Object.FindObjectOfType<SettingsPopup>(true);
            if (settings == null) { settings = BuildSettingsPopup(safeArea.transform, ads); built++; }
            if (hud.transform.Find("Settings") == null) { BuildGearButton(hud.transform, settings); built++; }

            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            string msg = built == 0
                ? "Everything is already installed. Delete a piece and run this again to rebuild it."
                : "Installed " + built + " piece(s). Save the scene (Ctrl+S) to keep them.";
            Debug.Log("[IdleMine] Audio: " + msg);
            if (!Application.isBatchMode && !Quiet) EditorUtility.DisplayDialog("Idle Mine", msg, "OK");
        }

        static void BuildAudio(GameManager game, AdManager ads)
        {
            var go = new GameObject("Audio");
            Undo.RegisterCreatedObjectUndo(go, "Install audio");
            go.transform.SetSiblingIndex(game.transform.GetSiblingIndex() + 1);
            var manager = go.AddComponent<AudioManager>();
            var hooks = go.AddComponent<GameAudio>();

            var so = new SerializedObject(manager);
            so.FindProperty("music").objectReferenceValue = Clip("Music/music_mine_loop");
            var list = so.FindProperty("sounds");
            list.arraySize = Defaults.Length;
            for (int i = 0; i < Defaults.Length; i++)
            {
                var d = Defaults[i];
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").enumValueIndex = (int)d.Id;
                e.FindPropertyRelative("volume").floatValue = d.Volume;
                e.FindPropertyRelative("pitchJitter").floatValue = d.Jitter;
                e.FindPropertyRelative("minInterval").floatValue = d.Interval;
                var clips = e.FindPropertyRelative("clips");
                clips.arraySize = d.Files.Length;
                for (int c = 0; c < d.Files.Length; c++)
                    clips.GetArrayElementAtIndex(c).objectReferenceValue = Clip(d.Files[c]);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            Set(hooks, "game", game);
            if (ads != null) Set(hooks, "ads", ads);
        }

        static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + name + ".wav");
            if (clip == null) Debug.LogWarning("[IdleMine] Missing audio clip " + AudioRoot + name + ".wav");
            return clip;
        }

        static SettingsPopup BuildSettingsPopup(Transform parent, AdManager ads)
        {
            var root = Stretch("SettingsPopup", parent);
            root.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            var popup = root.gameObject.AddComponent<SettingsPopup>();

            var card = Card(root, new Vector2(820, 760));
            var close = PlainButton("Close", card, "X", Palette.PanelLight, Palette.TextDim, null, 48);
            Place(close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(88, 88));
            Wire(close, popup.Close);
            Label("Title", card, "SETTINGS", 64, Palette.Text, TopRow(-56, 100), true);

            var music = PlainButton("Music", card, "MUSIC\n<size=30>ON</size>", Palette.Green, Palette.Panel, Centered(new Vector2(-185, -300), new Vector2(330, 160), 1), 46);
            Wire(music, popup.ToggleMusic);
            var sound = PlainButton("Sound", card, "SOUND\n<size=30>ON</size>", Palette.Green, Palette.Panel, Centered(new Vector2(185, -300), new Vector2(330, 160), 1), 46);
            Wire(sound, popup.ToggleSound);

            var restore = PlainButton("Restore", card, "Restore purchases", Palette.PanelLight, Palette.TextDim, Centered(new Vector2(0, -480), new Vector2(520, 90), 1), 32);
            Wire(restore, popup.Restore);
            var status = Label("Status", card, "", 28, Palette.TextDim, TopRow(-545, 44), false);
            var version = Label("Version", card, "Version 1.0.0", 26, Palette.TextDim, Bottom(28, new Vector2(600, 40)), false);

            Set(popup, "ads", ads);
            Set(popup, "card", card);
            Set(popup, "musicImage", music.GetComponent<Image>());
            Set(popup, "musicLabel", music.GetComponentInChildren<Text>());
            Set(popup, "soundImage", sound.GetComponent<Image>());
            Set(popup, "soundLabel", sound.GetComponentInChildren<Text>());
            Set(popup, "statusText", status);
            Set(popup, "versionText", version);
            root.gameObject.SetActive(false);
            return popup;
        }

        static void BuildGearButton(Transform hud, SettingsPopup settings)
        {
            // Left of the PASS button, vertically centred on the top row.
            var root = Node("Settings", hud, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-436, -36), new Vector2(88, 88));
            var bg = Img(root, Circle, Palette.PanelLight, false);
            var button = MakeButton(root.gameObject, bg);
            Wire(button, settings.Open);
            var icon = Node("Icon", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            Img(icon, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/IdleMine/Art/Sprites/Gear.png"), Palette.TextDim, false).raycastTarget = false;
        }
    }
}
