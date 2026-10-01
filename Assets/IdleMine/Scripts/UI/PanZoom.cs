using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleMine
{
    /// <summary>
    /// One-finger pan, two-finger pinch zoom, mouse-wheel zoom, with inertia and soft bounds.
    /// Goes through EventSystem pointer events, so it works with either input system.
    /// Put it on the viewport (which needs a raycast-target Graphic); `content` is what moves.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PanZoom : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [SerializeField] RectTransform content;
        [SerializeField] float minZoom = 0.08f;
        [SerializeField] float maxZoom = 1.6f;
        [Tooltip("How much of the content must stay on screen at the edges.")]
        [SerializeField] float edgeMargin = 220f;
        [SerializeField] float wheelZoomStep = 1.12f;

        /// <summary>Content-space rectangle that must stay reachable (set by SkillTreeView).</summary>
        public Rect Bounds { get; set; }

        readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();
        RectTransform _viewport;
        Vector2 _velocity;
        float _prevPinchDist;
        Vector2 _prevPinchMid;
        bool _pinching;

        float _focusT = -1f;
        Vector2 _focusFromPan, _focusToPan;
        float _focusFromZoom, _focusToZoom;

        public float Zoom { get { return content.localScale.x; } }
        public Vector2 Pan { get { return content.anchoredPosition; } }

        void Awake() { _viewport = (RectTransform)transform; }

        Vector2 ToLocal(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, e.position, e.pressEventCamera, out local);
            return local;
        }

        public void OnPointerDown(PointerEventData e) { Track(e); _velocity = Vector2.zero; }
        public void OnBeginDrag(PointerEventData e) { Track(e); }
        public void OnPointerUp(PointerEventData e) { Untrack(e); }
        public void OnEndDrag(PointerEventData e) { Untrack(e); }

        void Track(PointerEventData e)
        {
            _pointers[e.pointerId] = ToLocal(e);
            _pinching = false;
            _focusT = -1f;
        }

        void Untrack(PointerEventData e)
        {
            _pointers.Remove(e.pointerId);
            _pinching = false;
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 now = ToLocal(e);
            Vector2 prev;
            if (!_pointers.TryGetValue(e.pointerId, out prev)) { _pointers[e.pointerId] = now; return; }
            _pointers[e.pointerId] = now;

            if (_pointers.Count >= 2)
            {
                Vector2 a = Vector2.zero, b = Vector2.zero; int k = 0;
                foreach (var kv in _pointers) { if (k == 0) a = kv.Value; else if (k == 1) b = kv.Value; k++; }
                float dist = Vector2.Distance(a, b);
                Vector2 mid = (a + b) * 0.5f;
                if (_pinching && _prevPinchDist > 1f)
                {
                    SetZoom(Zoom * dist / _prevPinchDist, mid);
                    SetPan(Pan + (mid - _prevPinchMid));
                }
                _pinching = true;
                _prevPinchDist = dist;
                _prevPinchMid = mid;
                _velocity = Vector2.zero;
            }
            else
            {
                Vector2 delta = now - prev;
                SetPan(Pan + delta);
                float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                _velocity = Vector2.Lerp(_velocity, delta / dt, 0.5f);
            }
        }

        public void OnScroll(PointerEventData e)
        {
            _focusT = -1f;
            SetZoom(Zoom * Mathf.Pow(wheelZoomStep, e.scrollDelta.y), ToLocal(e));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_focusT >= 0f)
            {
                _focusT += dt / 0.45f;
                float t = Mathf.Clamp01(_focusT);
                float e = 1f - Mathf.Pow(1f - t, 3f);
                content.localScale = Vector3.one * Mathf.Lerp(_focusFromZoom, _focusToZoom, e);
                SetPan(Vector2.Lerp(_focusFromPan, _focusToPan, e));
                if (t >= 1f) _focusT = -1f;
                return;
            }

            if (_pointers.Count == 0 && _velocity.sqrMagnitude > 1f)
            {
                SetPan(Pan + _velocity * dt);
                _velocity *= Mathf.Pow(0.05f, dt);
            }
        }

        /// <summary>Zoom so that `pivot` (viewport-local) stays under the finger.</summary>
        public void SetZoom(float z, Vector2 pivot)
        {
            float old = Zoom;
            z = Mathf.Clamp(z, minZoom, maxZoom);
            if (Mathf.Approximately(old, z)) return;
            content.localScale = new Vector3(z, z, 1f);
            SetPan(pivot - (pivot - Pan) * (z / old));
        }

        public void SetPan(Vector2 pan)
        {
            content.anchoredPosition = ClampPan(pan, Zoom);
        }

        Vector2 ClampPan(Vector2 pan, float z)
        {
            Rect v = _viewport.rect;
            Rect b = Bounds;
            float hw = v.width * 0.5f, hh = v.height * 0.5f;
            float minX = -hw + edgeMargin - b.xMax * z, maxX = hw - edgeMargin - b.xMin * z;
            float minY = -hh + edgeMargin - b.yMax * z, maxY = hh - edgeMargin - b.yMin * z;
            if (minX > maxX) minX = maxX = (minX + maxX) * 0.5f;
            if (minY > maxY) minY = maxY = (minY + maxY) * 0.5f;
            return new Vector2(Mathf.Clamp(pan.x, minX, maxX), Mathf.Clamp(pan.y, minY, maxY));
        }

        /// <summary>Bring a content-space point to the centre of the viewport, animated unless instant.</summary>
        public void FocusOn(Vector2 contentPoint, float zoom, bool instant = false)
        {
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            _velocity = Vector2.zero;
            if (instant)
            {
                content.localScale = new Vector3(zoom, zoom, 1f);
                SetPan(-contentPoint * zoom);
                _focusT = -1f;
                return;
            }
            _focusFromPan = Pan;
            _focusFromZoom = Zoom;
            _focusToZoom = zoom;
            _focusToPan = ClampPan(-contentPoint * zoom, zoom);
            _focusT = 0f;
        }

        public void ClearPointers() { _pointers.Clear(); _pinching = false; _velocity = Vector2.zero; }
    }
}
