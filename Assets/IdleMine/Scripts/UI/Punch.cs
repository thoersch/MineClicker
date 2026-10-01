using UnityEngine;

namespace IdleMine
{
    /// <summary>Springy scale "pop". Punch.Play(transform) from anywhere; re-triggering restarts it.</summary>
    public class Punch : MonoBehaviour
    {
        Vector3 _baseScale = Vector3.one;
        float _t = 1f, _duration = 0.3f, _amount;

        public static void Play(Transform target, float amount = 0.15f, float duration = 0.3f)
        {
            if (target == null) return;
            var p = target.GetComponent<Punch>();
            if (p == null) p = target.gameObject.AddComponent<Punch>();
            if (!p.enabled || p._t >= 1f) p._baseScale = target.localScale; // re-capture when idle
            p._amount = amount;
            p._duration = Mathf.Max(0.05f, duration);
            p._t = 0f;
            p.enabled = true;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime / _duration;
            if (_t >= 1f)
            {
                transform.localScale = _baseScale;
                enabled = false;
                return;
            }
            // Up fast, small undershoot, settle.
            float s = 1f + _amount * Mathf.Sin(_t * Mathf.PI * 2f) * (1f - _t) * 1.6f;
            transform.localScale = _baseScale * s;
        }
    }
}
