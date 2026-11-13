using UnityEngine;

namespace YuDian
{
    /// <summary>只根据角色碰撞结算后的地面速度表现步伐，不读取移动按键。</summary>
    public sealed class CameraBobbing : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float cyclesPerMeter = .48f;
        [SerializeField] private float maxSpeed = 6.2f;
        [SerializeField] private float horizontalAmplitude = .012f;
        [SerializeField] private float verticalAmplitude = .018f;
        [SerializeField] private float smoothing = 10f;
        private Vector3 restPosition;
        private float phase;
        private bool ready;

        public float Phase => phase;
        public void Configure(Camera camera)
        {
            viewCamera = camera;
            if (viewCamera == null) { ready = false; return; }
            restPosition = viewCamera.transform.localPosition;
            ready = true;
        }

        public void ResetMotion()
        {
            phase = 0f;
            if (ready) viewCamera.transform.localPosition = restPosition;
        }

        public void Tick(float groundSpeed, float dt)
        {
            if (!ready || dt <= 0f) return;
            float speed = Mathf.Max(0f, groundSpeed);
            phase += speed * cyclesPerMeter * 2f * Mathf.PI * dt;
            float strength = Mathf.Clamp01(speed / Mathf.Max(.01f, maxSpeed));
            Vector3 target = restPosition + new Vector3(
                Mathf.Sin(phase) * horizontalAmplitude * strength,
                Mathf.Sin(phase * 2f) * verticalAmplitude * strength, 0f);
            float blend = 1f - Mathf.Exp(-smoothing * dt);
            viewCamera.transform.localPosition = Vector3.Lerp(viewCamera.transform.localPosition, target, blend);
        }
    }
}
