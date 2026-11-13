using UnityEngine;

namespace YuDian
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonWalker : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float walkingSpeed = 4.1f;
        [SerializeField] private float runningSpeed = 6.2f;
        [SerializeField] private float speedChangeRate = 18f;
        [SerializeField] private float lookSensitivity = 2.1f;

        private CharacterController body;
        private float pitch;
        private float verticalSpeed;
        private float currentSpeed;
        private CameraBobbing cameraMotion;

        public Camera ViewCamera => viewCamera;
        public bool InputEnabled { get; set; }
        public bool IsSprinting { get; private set; }
        public float HorizontalSpeed { get; private set; }

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            cameraMotion = GetComponent<CameraBobbing>();
            if (cameraMotion == null) cameraMotion = gameObject.AddComponent<CameraBobbing>();
            cameraMotion.Configure(viewCamera);
        }

        public void SetCamera(Camera camera)
        {
            viewCamera = camera;
            if (cameraMotion != null) cameraMotion.Configure(viewCamera);
        }

        public void MoveTo(Transform destination)
        {
            body.enabled = false;
            transform.SetPositionAndRotation(destination.position, destination.rotation);
            body.enabled = true;
            verticalSpeed = 0f;
            pitch = 0f;
            currentSpeed = 0f;
            HorizontalSpeed = 0f;
            IsSprinting = false;
            if (cameraMotion != null) cameraMotion.ResetMotion();
            viewCamera.transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                currentSpeed = 0f;
                HorizontalSpeed = 0f;
                IsSprinting = false;
                cameraMotion.Tick(0f, Time.deltaTime);
                return;
            }

            transform.Rotate(0f, Input.GetAxisRaw("Mouse X") * lookSensitivity, 0f);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * lookSensitivity, -82f, 82f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            float horizontal = (Input.GetKey(KeyCode.D) ? 1f : 0f) -
                (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) -
                (Input.GetKey(KeyCode.S) ? 1f : 0f);
            Vector3 direction = Vector3.ClampMagnitude(
                transform.right * horizontal + transform.forward * forward, 1f);

            bool moving = direction.sqrMagnitude > .001f;
            bool runRequested = moving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            MovePlayer(direction, runRequested, Time.deltaTime);
        }

        private void MovePlayer(Vector3 direction, bool runRequested, float dt)
        {
            bool moving = direction.sqrMagnitude > .001f;
            float targetSpeed = moving ? (runRequested ? runningSpeed : walkingSpeed) : 0f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedChangeRate * dt);
            verticalSpeed = body.isGrounded ? -2f : verticalSpeed - 18f * dt;
            Vector3 before = transform.position;
            body.Move((direction * currentSpeed + Vector3.up * verticalSpeed) * dt);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            HorizontalSpeed = dt > 0f ? moved.magnitude / dt : 0f;
            IsSprinting = moving && HorizontalSpeed > walkingSpeed + .1f;
            cameraMotion.Tick(body.isGrounded ? HorizontalSpeed : 0f, dt);
        }
    }
}
