using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IdleMine
{
    /// <summary>Fires on press, then repeats (and speeds up) while the finger stays down.
    /// Lets players shovel dozens of miners around without tapping + fifty times.</summary>
    public class HoldRepeater : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action OnFire;
        public float firstDelay = 0.35f;
        public float startInterval = 0.12f;
        public float minInterval = 0.03f;

        bool _held;
        float _timer, _interval;

        public void OnPointerDown(PointerEventData e)
        {
            _held = true;
            _timer = firstDelay;
            _interval = startInterval;
            if (OnFire != null) OnFire();
        }

        public void OnPointerUp(PointerEventData e) { _held = false; }
        public void OnPointerExit(PointerEventData e) { _held = false; }
        void OnDisable() { _held = false; }

        void Update()
        {
            if (!_held) return;
            _timer -= Time.unscaledDeltaTime;
            while (_timer <= 0f && _held)
            {
                if (OnFire != null) OnFire();
                _interval = Mathf.Max(minInterval, _interval * 0.85f);
                _timer += _interval;
            }
        }
    }
}
