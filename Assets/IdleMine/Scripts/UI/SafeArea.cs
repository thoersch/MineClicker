using UnityEngine;
using UnityEngine.UI;

namespace IdleMine
{
    /// <summary>
    /// Keeps this RectTransform inside Screen.safeArea (notches, rounded corners, home indicator).
    /// Optionally flips the CanvasScaler to match height in landscape, so the portrait layout stays
    /// usable if you test in a landscape Game view.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [SerializeField] CanvasScaler scaler;
        [SerializeField] bool adaptScalerToOrientation = true;

        Rect _lastSafe;
        Vector2Int _lastScreen;

        void OnEnable() { _lastScreen = Vector2Int.zero; }

        void Update()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea == _lastSafe && screen == _lastScreen) return;
            _lastSafe = Screen.safeArea;
            _lastScreen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;

            var rt = (RectTransform)transform;
            Vector2 min = _lastSafe.position, max = _lastSafe.position + _lastSafe.size;
            rt.anchorMin = new Vector2(min.x / screen.x, min.y / screen.y);
            rt.anchorMax = new Vector2(max.x / screen.x, max.y / screen.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (scaler != null && adaptScalerToOrientation) scaler.matchWidthOrHeight = screen.x > screen.y ? 1f : 0f;
        }
    }
}
