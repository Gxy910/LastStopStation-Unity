using UnityEngine;

namespace YuDian
{
    /// <summary>第四章的场景摆放点。移动带中文名称的父物体，而不是它的“编辑示意”子物体。</summary>
    public sealed class EpilogueLayout : MonoBehaviour
    {
        [Header("第四章回站")]
        public Transform returnSpawn;
        public Transform alanAtDispatch;
        public Transform alanAtShelter;
        [Header("暖棚")]
        public Transform shelterLeader;
        public Transform shelterWorkbench;
        public Transform shelterSign;
        public Transform supplyCrate;
        public Transform alanEmptyDeskSign;
        [Header("路哨")]
        public Transform disciplineNotice;
        public Transform disciplineLabel;
        public Transform punishedDriver;
        public Transform punishedScout;
        public Transform fareSign;

        /// <summary>
        /// 兼容旧场景：此前选中“编辑示意”移动时，位移被保存在子物体。
        /// 在运行时把相对默认值的变化转到父摆放点，不改写用户保存的场景。
        /// </summary>
        public void ApplyPreviewEdits(EpilogueLayout template)
        {
            if (template == null || template == this) return;
            Transform[] current = { returnSpawn, alanAtDispatch, alanAtShelter, shelterLeader,
                shelterWorkbench, shelterSign, supplyCrate, alanEmptyDeskSign, disciplineNotice,
                disciplineLabel, punishedDriver, punishedScout, fareSign };
            Transform[] defaults = { template.returnSpawn, template.alanAtDispatch, template.alanAtShelter, template.shelterLeader,
                template.shelterWorkbench, template.shelterSign, template.supplyCrate, template.alanEmptyDeskSign, template.disciplineNotice,
                template.disciplineLabel, template.punishedDriver, template.punishedScout, template.fareSign };
            for (int i = 0; i < current.Length; i++)
            {
                Transform marker = current[i], original = defaults[i];
                if (marker == null || original == null) continue;
                Transform preview = marker.Find("编辑示意"), reference = original.Find("编辑示意");
                if (preview == null || reference == null) continue;
                Vector3 delta = preview.localPosition - reference.localPosition;
                Quaternion rotationDelta = preview.localRotation * Quaternion.Inverse(reference.localRotation);
                if (delta.sqrMagnitude < .00000001f && Quaternion.Angle(rotationDelta, Quaternion.identity) < .001f) continue;
                Vector3 corrected = marker.TransformPoint(delta);
                Quaternion correctedRotation = marker.rotation * rotationDelta;
                marker.SetPositionAndRotation(corrected, correctedRotation);
                preview.localPosition = reference.localPosition;
                preview.localRotation = reference.localRotation;
            }
        }

        public void HidePreviews()
        {
            foreach (Transform marker in transform)
            {
                Transform preview = marker.Find("编辑示意");
                if (preview != null) preview.gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            foreach (Transform marker in transform)
            {
                EditorHooks.Label(marker.position + Vector3.up * 1.9f, marker.name, new Color(1f, .78f, .30f));
            }
        }
#endif
    }
}
