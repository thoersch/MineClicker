using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Small settings card opened from the gear button in the HUD: music and sound on/off, plus Restore
    /// Purchases. Its GameObject starts inactive in the scene. Buttons are wired in the Inspector:
    /// Gear -> Open, Close -> Close, Music -> ToggleMusic, Sound -> ToggleSound, Restore -> Restore.
    /// </summary>
    public class SettingsPopup : MonoBehaviour
    {
        [SerializeField] AdManager ads;
        [SerializeField] RectTransform card;
        [SerializeField] Image musicImage;
        [SerializeField] Text musicLabel;
        [SerializeField] Image soundImage;
        [SerializeField] Text soundLabel;
        [SerializeField] Text statusText;
        [SerializeField] Text versionText;

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            statusText.text = "";
            versionText.text = "Version " + Application.version;
            Punch.Play(card, 0.15f, 0.3f);
            Refresh();
        }

        public void Close() { gameObject.SetActive(false); }

        void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) Close(); // Android back button
#endif
        }

        public void ToggleMusic()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            audio.MusicOn = !audio.MusicOn;
            Refresh();
        }

        public void ToggleSound()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            audio.SoundOn = !audio.SoundOn;
            Refresh();
            AudioManager.Play(Sfx.Click); // audible confirmation when turning it back on
        }

        public void Restore()
        {
            if (ads == null) return;
            statusText.text = "Restoring...";
            ads.RestorePurchases(ok =>
            {
                statusText.text = ads.HasForemanPass ? "Foreman Pass restored." :
                                  ok ? "No previous purchase found on this account." : "Couldn't reach the store. Try again later.";
            });
        }

        void Refresh()
        {
            var audio = AudioManager.Instance;
            bool music = audio != null && audio.MusicOn, sound = audio != null && audio.SoundOn;
            Set(musicImage, musicLabel, "MUSIC", music);
            Set(soundImage, soundLabel, "SOUND", sound);
        }

        static void Set(Image bg, Text label, string name, bool on)
        {
            label.text = name + "\n<size=30>" + (on ? "ON" : "OFF") + "</size>";
            bg.color = on ? Palette.Green : Palette.PanelLight;
            label.color = on ? Palette.Panel : Palette.TextDim;
        }
    }
}
