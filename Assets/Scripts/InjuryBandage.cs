using UnityEngine;
using System.Collections.Generic;

namespace YuDian
{
    /// <summary>在动画采样后收起受伤手部的手指，让包扎状态持续可见。</summary>
    public sealed class InjuryBandage : MonoBehaviour
    {
        private Transform[] fingers;
        private Transform landmark;
        private Transform forearm;
        private Transform bandage;
        private Mesh injuredMesh;
        public void Configure(Transform[] value, Transform arm, Transform hand, Transform wrap, SkinnedMeshRenderer body)
        {
            fingers = value;
            forearm = arm;
            landmark = hand;
            bandage = wrap;
            RemoveHandTriangles(body);
        }

        private void RemoveHandTriangles(SkinnedMeshRenderer body)
        {
            if (body == null || body.sharedMesh == null || !body.sharedMesh.isReadable) return;
            Mesh source = body.sharedMesh;
            BoneWeight[] weights = source.boneWeights;
            var handBones = new HashSet<int>();
            for (int i = 0; i < body.bones.Length; i++)
            {
                string name = body.bones[i].name;
                if (name.EndsWith(".R") && (name.StartsWith("Index") || name.StartsWith("Middle") ||
                    name.StartsWith("Pinky") || name.StartsWith("Thumb"))) handBones.Add(i);
            }
            injuredMesh = Instantiate(source);
            injuredMesh.name = source.name + " · 右手包扎";
            for (int part = 0; part < source.subMeshCount; part++)
            {
                int[] triangles = source.GetTriangles(part);
                var kept = new List<int>(triangles.Length);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    float handWeight = Weight(weights[triangles[i]], handBones) + Weight(weights[triangles[i + 1]], handBones) + Weight(weights[triangles[i + 2]], handBones);
                    if (handWeight > .55f) continue;
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                }
                injuredMesh.SetTriangles(kept, part, false);
            }
            body.sharedMesh = injuredMesh;
        }

        private static float Weight(BoneWeight w, HashSet<int> bones)
        {
            return (bones.Contains(w.boneIndex0) ? w.weight0 : 0) + (bones.Contains(w.boneIndex1) ? w.weight1 : 0) +
                (bones.Contains(w.boneIndex2) ? w.weight2 : 0) + (bones.Contains(w.boneIndex3) ? w.weight3 : 0);
        }
        private void LateUpdate()
        {
            if (fingers == null) return;
            if (landmark != null && bandage != null)
            {
                Vector3 direction = forearm != null ? (landmark.position - forearm.position).normalized : Vector3.zero;
                bandage.position = landmark.position - direction * .02f;
            }
            foreach (Transform finger in fingers)
                if (finger != null) finger.localScale = Vector3.zero;
        }
        private void OnDestroy() { if (injuredMesh != null) Destroy(injuredMesh); }
    }
}
