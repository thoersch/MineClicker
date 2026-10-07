using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Dynamite (Deep Core, Demolition branch). Press and hold a layer: after a short beat the fuse lights, a ring
    /// fills under your finger and sparks fly. Release to blast: a white flash, a shockwave, a storm of rubble,
    /// screen shake, a boom and a heavy haptic. Short presses stay normal taps; dragging cancels (so scrolling
    /// still works). Shows a small "TNT" status line while dynamite is unlocked.
    /// LayerRowView forwards its pointer down/up here.
    /// </summary>
    public class DynamiteView : MonoBehaviour
    {
        enum State { Idle, Pending, Charging }

        [SerializeField] GameManager game;
        [SerializeField] FxLayer fx;
        [SerializeField] RectTransform shakeTarget;
        [SerializeField] Image fuseRing;
        [SerializeField] Image fuseGlow;
        [SerializeField] Image flash;
        [SerializeField] Image shockwave;
        [SerializeField] GameObject statusRoot;
        [SerializeField] Text statusText;
        [SerializeField] float cancelDistance = 40f;

        public static DynamiteView Instance { get; private set; }

        static readonly Color[] Rubble = { Palette.Orange, Palette.Gold, Palette.Hex("FF5A36"), Palette.Text, Palette.Hex("8D6E63") };

        State _state;
        int _pointerId, _row;
        Vector2 _downScreen;
        float _t, _charge, _sparkTimer, _tickTimer;
        float _fxT = -1f, _shakeT = -1f;
        Vector2 _shakeBase, _blastAt;

        void Awake() { Instance = this; }

        void Start()
        {
            game.Blasted += OnBlasted;
            fuseRing.gameObject.SetActive(false);
            fuseGlow.gameObject.SetActive(false);
            flash.gameObject.SetActive(false);
            shockwave.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (game != null) game.Blasted -= OnBlasted;
            if (Instance == this) Instance = null;
        }

        // ================================================================== input (from LayerRowView)

        public void Begin(LayerRowView row, PointerEventData e)
        {
            if (!game.DynamiteReady || _state != State.Idle || row.Layer == null) return;
            _state = State.Pending;
            _pointerId = e.pointerId;
            _row = row.Layer.Index;
            _downScreen = e.position;
            _t = 0f;
            _charge = 0f;
        }

        /// <summary>Returns true if this release detonated (so the row should not also count it as a tap).</summary>
        public bool Release(LayerRowView row)
        {
            if (_state == State.Charging)
            {
                float charge = Mathf.Max(0.25f, _charge);
                Cancel();
                _blastAt = fx.ScreenToLocal(_downScreen, CanvasCamera);
                game.Blast(_row, charge);
                return true;
            }
            Cancel();
            return false;
        }

        void Cancel()
        {
            _state = State.Idle;
            fuseRing.gameObject.SetActive(false);
            fuseGlow.gameObject.SetActive(false);
        }

        // Null for an overlay canvas (the game's normal setup), the canvas camera otherwise.
        Camera CanvasCamera
        {
            get
            {
                var canvas = fx.GetComponentInParent<Canvas>().rootCanvas;
                return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }
        }

        Vector2 PointerScreen()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_pointerId < 0) return Input.mousePosition;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.fingerId == _pointerId) return touch.position;
            }
#endif
            return _downScreen;
        }

        // ================================================================== frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateStatus();

            if (_state != State.Idle)
            {
                if ((PointerScreen() - _downScreen).magnitude > cancelDistance) Cancel(); // it's a scroll, not a fuse
            }
            if (_state == State.Pending)
            {
                _t += dt;
                if (_t >= game.Config.dynamiteHoldDelay) StartFuse();
            }
            else if (_state == State.Charging)
            {
                _charge = Mathf.Min(1f, _charge + dt / Mathf.Max(0.05f, game.DynamiteChargeSeconds));
                Vector2 at = fx.ScreenToLocal(_downScreen, CanvasCamera);
                fuseRing.rectTransform.anchoredPosition = at;
                fuseGlow.rectTransform.anchoredPosition = at;
                fuseRing.fillAmount = _charge;
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * (10f + 20f * _charge));
                fuseGlow.rectTransform.localScale = Vector3.one * (0.6f + 0.8f * _charge) * pulse;
                fuseRing.color = Color.Lerp(Palette.Gold, Palette.Hex("FF5A36"), _charge);

                _sparkTimer -= dt;
                if (_sparkTimer <= 0f) { _sparkTimer = 0.05f; fx.SpawnChips(at, Random.value < 0.5f ? Palette.Gold : Palette.Orange, 2); }
                _tickTimer -= dt;
                if (_tickTimer <= 0f) { _tickTimer = _charge >= 1f ? 0.12f : 0.25f; Haptics.Play(_charge >= 1f ? Haptic.Rigid : Haptic.Light); }
            }

            AnimateBlast(dt);
        }

        void StartFuse()
        {
            _state = State.Charging;
            _charge = 0f;
            _sparkTimer = 0f;
            _tickTimer = 0f;
            fuseRing.gameObject.SetActive(true);
            fuseGlow.gameObject.SetActive(true);
            fuseRing.transform.SetAsLastSibling();
            Feedback.Play(Sfx.Fuse);
        }

        void UpdateStatus()
        {
            bool show = game.DynamiteUnlocked;
            var root = statusRoot != null ? statusRoot : statusText.gameObject;
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;
            float left = game.DynamiteCooldownLeft;
            statusText.text = left > 0f ? "TNT  " + Mathf.CeilToInt(left) + "s" : "TNT READY  <size=24>hold a layer</size>";
            statusText.color = left > 0f ? Palette.TextDim : Palette.Orange;
        }

        // ================================================================== boom

        void OnBlasted(BlastResult r)
        {
            if (!r.Detonated) return;
            _fxT = 0f;
            _shakeT = 0f;
            _shakeBase = shakeTarget.anchoredPosition;
            flash.rectTransform.anchoredPosition = _blastAt;
            shockwave.rectTransform.anchoredPosition = _blastAt;
            flash.gameObject.SetActive(true);
            shockwave.gameObject.SetActive(true);
            flash.transform.SetAsLastSibling();
            shockwave.transform.SetAsLastSibling();

            float big = 0.6f + 0.4f * r.Charge;
            for (int i = 0; i < 5; i++)
                fx.SpawnChips(_blastAt + Random.insideUnitCircle * 60f, Rubble[i % Rubble.Length], Mathf.RoundToInt(18 * big));
            for (int i = 0; i < r.Layers.Count; i++)
            {
                if (r.Layers[i] == r.Center) continue;
                float dy = (r.Center - r.Layers[i]) * 260f; // rows below the blast are further down the screen
                fx.SpawnChips(_blastAt + new Vector2(Random.Range(-200f, 200f), dy), Rubble[i % Rubble.Length], 10);
            }
            fx.SpawnText(_blastAt + new Vector2(0, 120), "BOOM!  +" + NumberFormat.Money(r.Money), Palette.Orange, Mathf.RoundToInt(60 + 24 * r.Charge), 1.6f, 260f);
            Feedback.Play(Sfx.Boom);
            Haptics.Play(Haptic.Heavy);
        }

        void AnimateBlast(float dt)
        {
            if (_fxT >= 0f)
            {
                _fxT += dt;
                float a = Mathf.Clamp01(_fxT / 0.35f);
                flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 3.2f, 1f - (1f - a) * (1f - a));
                flash.color = new Color(1f, 0.95f, 0.8f, (1f - a) * 0.9f);
                float b = Mathf.Clamp01(_fxT / 0.55f);
                shockwave.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 5f, b);
                shockwave.color = Palette.WithAlpha(Palette.Orange, (1f - b) * 0.8f);
                if (_fxT > 0.6f) { flash.gameObject.SetActive(false); shockwave.gameObject.SetActive(false); _fxT = -1f; }
            }
            if (_shakeT >= 0f)
            {
                _shakeT += dt;
                float k = 1f - _shakeT / 0.45f;
                if (k <= 0f) { shakeTarget.anchoredPosition = _shakeBase; _shakeT = -1f; return; }
                shakeTarget.anchoredPosition = _shakeBase + Random.insideUnitCircle * 30f * k * k;
            }
        }
    }
}
