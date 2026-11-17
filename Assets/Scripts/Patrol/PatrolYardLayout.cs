using UnityEngine;

namespace YuDian
{
    /// <summary>清障货场的场景对象及玩法位置引用。</summary>
    public sealed class PatrolYardLayout : MonoBehaviour
    {
        public Transform arrival;
        public Transform exit;
        public Transform leftSpawn;
        public Transform rightSpawn;
        public BoxCollider centralObstacle;
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            DrawPoint(arrival, "玩家进入点", new Color(.25f, 1f, .5f));
            DrawPoint(exit, "撤离交互点", new Color(.25f, 1f, .5f));
            DrawPoint(leftSpawn, "西侧怪物出生点", new Color(1f, .65f, .2f));
            DrawPoint(rightSpawn, "东侧怪物出生点", new Color(.3f, .7f, 1f));
        }
        private static void DrawPoint(Transform point, string label, Color color)
        {
            if (point == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(point.position + Vector3.up * .15f, .3f);
            EditorHooks.Label(point.position + Vector3.up * .65f, label, color);
        }
#endif
    }
}
