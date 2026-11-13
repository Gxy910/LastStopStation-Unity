using UnityEngine;

namespace YuDian
{
    /// <summary>可重复开关的门；门板和碰撞体使用同一个旋转节点。</summary>
    public sealed class InteractiveDoor : MonoBehaviour
    {
        [SerializeField] private Transform leaf;
        [SerializeField] private string doorName;
        [SerializeField] private float width = 2f;
        [SerializeField] private float openAngle = 90f;
        private bool isOpen;

        public string DisplayName => (isOpen ? "关闭" : "打开") + doorName;
        public bool IsOpen => isOpen;

        public void Configure(Transform doorLeaf, string label, float doorWidth, float angle)
        {
            leaf = doorLeaf;
            doorName = label;
            width = doorWidth;
            openAngle = angle;
        }

        public bool TryToggle(FirstPersonWalker walker)
        {
            if (leaf == null || walker == null) return false;
            CharacterController controller = walker.GetComponent<CharacterController>();
            float radius = controller != null ? controller.radius : .35f;
            Vector3 player = walker.transform.position;
            player.y = transform.position.y;
            // 检查门从当前角度到目标角度扫过的区域，避免门板穿过玩家。
            float from = isOpen ? openAngle : 0f;
            float to = isOpen ? 0f : openAngle;
            for (int i = 0; i <= 12; i++)
            {
                float angle = Mathf.Lerp(from, to, i / 12f);
                Vector3 end = transform.position + transform.rotation * Quaternion.Euler(0, angle, 0) * Vector3.right * width;
                Vector3 segment = end - transform.position;
                float t = Mathf.Clamp01(Vector3.Dot(player - transform.position, segment) / segment.sqrMagnitude);
                if (Vector3.Distance(player, transform.position + segment * t) < radius + .16f)
                    return false;
            }
            isOpen = !isOpen;
            leaf.localRotation = Quaternion.Euler(0, isOpen ? openAngle : 0f, 0);
            Physics.SyncTransforms();
            return true;
        }
    }
}
