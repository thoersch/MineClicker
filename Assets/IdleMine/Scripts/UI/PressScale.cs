using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleMine
{
    /// <summary>Squashes a button slightly while held. Cheap tactile feedback that makes every press feel good.</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressedScale = 0.93f;
        float _target = 1f, _current = 1f;

        public void OnPointerDown(PointerEventData e) { _target = pressedScale; enabled = true; }
        public void OnPointerUp(PointerEventData e) { _target = 1f; enabled = true; }
        public void OnPointerExit(PointerEventData e) { _target = 1f; enabled = true; }

        void Update()
        {
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            if (Mathf.Abs(_current - _target) < 0.001f) { _current = _target; if (_target == 1f) enabled = false; }
            transform.localScale = new Vector3(_current, _current, 1f);
        }
    }
}
