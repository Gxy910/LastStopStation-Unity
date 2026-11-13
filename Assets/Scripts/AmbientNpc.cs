using UnityEngine;

namespace YuDian
{
    /// <summary>环境人物只提供闲聊和轻微动作，不修改主线任务状态。</summary>
    public sealed class AmbientNpc : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] [TextArea] private string[] lines;
        [SerializeField] private Transform gesture;
        [SerializeField] private float gestureDegrees = 8f;
        [SerializeField] private float gestureSpeed = 1.2f;
        private Quaternion restRotation;
        public string DisplayName => displayName;

        public void Configure(string label, string[] dialogueLines, Transform animatedPart, float degrees, float speed)
        {
            displayName = label;
            lines = dialogueLines;
            gesture = animatedPart;
            gestureDegrees = degrees;
            gestureSpeed = speed;
        }

        private void Awake()
        {
            if (gesture != null) restRotation = gesture.localRotation;
        }

        private void Update()
        {
            if (gesture != null)
                gesture.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(Time.time * gestureSpeed) * gestureDegrees, 0, 0);
        }

        public StationDialogue Talk() => new StationDialogue(displayName, lines);
    }
}
