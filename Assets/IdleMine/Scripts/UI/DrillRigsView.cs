using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Drill rigs (Deep Core, Drillworks branch). Puts a drill on every mine row that has one (the deepest DrillCount
    /// layers): an orange motor housing over a steel bit whose spiral grooves cycle through frames so it spins,
    /// with a little vibration and rock dust kicking up from the tip. Faster while overclocked. The digging itself
    /// happens in GameManager.Tick. Icons are created in code on the pooled LayerRow views.
    /// </summary>
    public class DrillRigsView : MonoBehaviour
    {
        class Rig { public RectTransform Rt; public Image Img; }

        [SerializeField] GameManager game;
        [SerializeField] FxLayer fx;
        [Tooltip("Animation frames, played in a loop.")]
        [SerializeField] Sprite[] frames = new Sprite[0];
        [Tooltip("Older single-image setups; used when no frames are set.")]
        [SerializeField] Sprite drillSprite;
        [SerializeField] Color drillColor = Color.white;
        // Named apart from the old offsetFromRight field so a stale value saved in older scenes isn't picked up.
        [Tooltip("Drill position, measured from the middle of the row's right edge. Negative X = further left, negative Y = lower. " +
                 "Default sits in the rock of the row's last miner slot, clear of the slot counter and the progress bar. Live-editable in Play mode.")]
        [SerializeField] Vector2 drillOffset = new Vector2(-530f, -62f);
        [Tooltip("Drill width and height. Live-editable in Play mode.")]
        [SerializeField] Vector2 drillSize = new Vector2(88f, 122f);
        [SerializeField] float framesPerSecond = 16f;
        [SerializeField] Color dustColor = new Color(0.55f, 0.42f, 0.3f, 1f);

        readonly Dictionary<LayerRowView, Rig> _rigs = new Dictionary<LayerRowView, Rig>();
        LayerRowView[] _rows = new LayerRowView[0];
        float _scan, _dust;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _scan -= dt;
            if (_scan <= 0f) { _scan = 1f; _rows = FindObjectsOfType<LayerRowView>(); }

            bool fast = game.OverclockActive;
            float fps = framesPerSecond * (fast ? 2f : 1f);
            int frame = frames.Length > 0 ? (int)(Time.unscaledTime * fps) % frames.Length : 0;
            _dust -= dt;
            bool puff = _dust <= 0f;
            if (puff) _dust = fast ? 0.12f : 0.25f;

            foreach (var row in _rows)
            {
                if (row == null) continue;
                bool drilled = row.isActiveAndEnabled && row.Layer != null && game.HasDrill(row.Layer.Index);
                Rig rig;
                _rigs.TryGetValue(row, out rig);
                if (!drilled) { if (rig != null && rig.Rt.gameObject.activeSelf) rig.Rt.gameObject.SetActive(false); continue; }
                if (rig == null) rig = _rigs[row] = MakeRig(row.transform);
                if (!rig.Rt.gameObject.activeSelf) rig.Rt.gameObject.SetActive(true);

                if (frames.Length > 0) rig.Img.sprite = frames[frame];
                // Vibrate: a couple of pixels of jitter, more when overclocked.
                float j = fast ? 3f : 1.5f;
                if (rig.Rt.sizeDelta != drillSize) rig.Rt.sizeDelta = drillSize;
                rig.Rt.anchoredPosition = drillOffset + new Vector2(Random.Range(-j, j), Random.Range(-j, j));

                if (puff && fx != null)
                {
                    Vector3 tip = rig.Rt.TransformPoint(new Vector3(0f, -drillSize.y * 0.5f, 0f));
                    fx.SpawnChips(fx.WorldToLocal(tip), dustColor, fast ? 3 : 2);
                }
            }
        }

        Rig MakeRig(Transform row)
        {
            var go = new GameObject("Drill Rig", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(row, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.anchoredPosition = drillOffset;
            rt.sizeDelta = drillSize;
            var img = go.GetComponent<Image>();
            img.sprite = frames.Length > 0 ? frames[0] : drillSprite;
            img.color = drillColor;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return new Rig { Rt = rt, Img = img };
        }
    }
}
