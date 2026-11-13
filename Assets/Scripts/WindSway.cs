using UnityEngine;

namespace YuDian
{
    /// <summary>各片叶子使用独立相位和频率，绕根部轻微摆动。</summary>
    public sealed class WindSway : MonoBehaviour
    {
        [SerializeField] private float amplitude = 7f;
        [SerializeField] private float speed = 1.1f;
        [SerializeField] private float phase;
        private Quaternion original;
        public void Configure(float angle, float frequency, float offset) { amplitude=angle; speed=frequency; phase=offset; }
        private void Awake() { original = transform.localRotation; }
        private void Update()
        {
            float a = Mathf.Sin(Time.time * speed + phase) * amplitude;
            float b = Mathf.Sin(Time.time * speed * .71f + phase * 1.3f) * amplitude * .35f;
            transform.localRotation = original * Quaternion.Euler(a, 0, b);
        }
    }
}
