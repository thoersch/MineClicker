using System;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Stand-in ad network for the editor and development builds: a full-screen "TEST AD" with a countdown.
    /// Close it before the countdown ends to test the "skipped, no reward" path; lower the fill rate on
    /// AdManager to test the "no ad available" path.
    /// </summary>
    public class MockAdService : IRewardedAdService
    {
        readonly float _adSeconds, _loadSeconds, _fillRate;
        float _readyAt = -1f;
        bool _loaded, _showing;

        public MockAdService(float adSeconds, float loadSeconds, float fillRate)
        {
            _adSeconds = adSeconds; _loadSeconds = loadSeconds; _fillRate = fillRate;
        }

        public void Initialize() { Load(); }

        void Load()
        {
            _loaded = false;
            // A failed fill retries after a while, the way a real network backs off.
            bool fills = UnityEngine.Random.value < _fillRate;
            _readyAt = Time.unscaledTime + (fills ? _loadSeconds : 20f);
            _loaded = fills;
        }

        public bool IsReady
        {
            get
            {
                if (_showing) return false;
                if (!_loaded && Time.unscaledTime >= _readyAt) Load();
                return _loaded && Time.unscaledTime >= _readyAt;
            }
        }

        public void Show(AdPlacement placement, Action<AdResult> done)
        {
            if (!IsReady) { done(AdResult.Failed); return; }
            _showing = true;
            MockAdOverlay.Create(placement, _adSeconds, result =>
            {
                _showing = false;
                Load();
                done(result);
            });
        }
    }

    /// <summary>The fake ad's screen. Built in code because it only exists in the editor and dev builds.</summary>
    public class MockAdOverlay : MonoBehaviour
    {
        Text _countdown, _closeLabel;
        float _left;
        Action<AdResult> _done;

        public static void Create(AdPlacement placement, float seconds, Action<AdResult> done)
        {
            var root = new GameObject("Mock Ad");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            root.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var bg = NewRect("Background", root.transform, Vector2.zero, Vector2.one);
            bg.gameObject.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);

            var title = NewText("Title", root.transform, font, 90, "TEST AD");
            title.rectTransform.anchoredPosition = new Vector2(0, 220);
            var info = NewText("Info", root.transform, font, 44,
                "Placement: " + placement + "\n\nA real ad network shows a video here.\nClose early = no reward.");
            info.color = new Color(1, 1, 1, 0.6f);
            info.rectTransform.anchoredPosition = new Vector2(0, -20);

            var o = root.AddComponent<MockAdOverlay>();
            o._countdown = NewText("Countdown", root.transform, font, 60, "");
            o._countdown.rectTransform.anchoredPosition = new Vector2(0, -260);

            var close = NewRect("Close", root.transform, new Vector2(1, 1), new Vector2(1, 1));
            close.pivot = new Vector2(1, 1);
            close.sizeDelta = new Vector2(260, 120);
            close.anchoredPosition = new Vector2(-40, -80);
            close.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            close.gameObject.AddComponent<Button>().onClick.AddListener(o.OnClose);
            o._closeLabel = NewText("Label", close, font, 44, "");
            o._closeLabel.rectTransform.anchorMin = Vector2.zero;
            o._closeLabel.rectTransform.anchorMax = Vector2.one;
            o._closeLabel.rectTransform.sizeDelta = Vector2.zero;

            o._left = seconds;
            o._done = done;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        static Text NewText(string name, Transform parent, Font font, int size, string s)
        {
            var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rt.sizeDelta = new Vector2(1000, 400);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.text = s;
            return t;
        }

        void Update()
        {
            _left -= Time.unscaledDeltaTime;
            _countdown.text = _left > 0 ? "Reward in " + Mathf.CeilToInt(_left) : "Reward earned!";
            _closeLabel.text = _left > 0 ? "SKIP" : "CLOSE";
        }

        void OnClose()
        {
            var done = _done;
            _done = null;
            Destroy(gameObject);
            if (done != null) done(_left > 0 ? AdResult.Skipped : AdResult.Rewarded);
        }
    }

    /// <summary>Stand-in store for the editor and development builds: every purchase succeeds instantly.</summary>
    public class MockPurchaseService : IPurchaseService
    {
        public event Action<string> Owned;

        public void Initialize(string[] nonConsumableIds) { }
        public bool IsReady { get { return true; } }
        public string Price(string productId) { return "$4.99"; }

        public void Purchase(string productId, Action<bool> done)
        {
            Debug.Log("[IdleMine] Mock store: purchased " + productId);
            if (Owned != null) Owned(productId);
            done(true);
        }

        public void Restore(Action<bool> done)
        {
            Debug.Log("[IdleMine] Mock store: restore (nothing to restore in the editor).");
            done(true);
        }
    }
}
