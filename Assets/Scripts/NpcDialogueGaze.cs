using UnityEngine;

namespace YuDian
{
    /// <summary>对话时向玩家转头；停止对话后平滑复位。</summary>
    public sealed class NpcDialogueGaze : MonoBehaviour
    {
        private Transform target, headPivot, face;
        private Transform animatedHead;
        private MeshFilter meshFilter;
        private Mesh originalMesh, animatedMesh;
        private Vector3[] baseVertices, animatedVertices, baseNormals, animatedNormals;
        private Quaternion baseRotation;
        private Quaternion faceRotation;
        private Quaternion previousBoneResult, previousBoneOffset;
        private bool boneOffsetApplied;
        private Vector3 facePosition;
        private Vector3 pivotPosition, torsoPivot;
        private float headY, bodyAngle, headAngle, bodyVelocity, headVelocity;
        private bool built, usesMesh;
        private bool settled;
        public float HeadAngle => headAngle;
        public bool IsLooking => target != null;

        public void Focus(Transform player)
        {
            if (!built) BuildRig();
            target = player;
            settled = false;
        }
        public void Release() { target = null; }

        private void BuildRig()
        {
            built = true;
            baseRotation = transform.localRotation;
            bool alan = name == "阿岚";
            if (name.StartsWith("接送者·"))
                foreach (Transform bone in GetComponentsInChildren<Transform>(true))
                    if (bone.name == "Head" || bone.name == "head") { animatedHead = bone; break; }
            headY = alan ? 1.365f : (name == "暖棚休息者" ? 1.21f : 1.63f);
            torsoPivot = new Vector3(0, alan ? .65f : .85f, 0);
            var model = transform.Find("低模外观");
            meshFilter = model != null ? model.GetComponent<MeshFilter>() : null;
            if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.isReadable)
            {
                originalMesh = meshFilter.sharedMesh;
                animatedMesh = Instantiate(originalMesh);
                animatedMesh.name = originalMesh.name + " · 对话头部";
                animatedMesh.MarkDynamic();
                meshFilter.sharedMesh = animatedMesh;
                baseVertices = originalMesh.vertices;
                animatedVertices = new Vector3[baseVertices.Length];
                baseNormals = originalMesh.normals;
                animatedNormals = new Vector3[baseNormals.Length];
                usesMesh = true;
            }

            var pivot = new GameObject("对话头部转轴");
            headPivot = pivot.transform;
            headPivot.SetParent(transform, false);
            pivotPosition = new Vector3(0, headY, 0);
            headPivot.localPosition = pivotPosition;
            if (usesMesh)
            {
                face = transform.Find("角色面部细节");
                // 面部保持原来的层级，战斗中复制阿岚时仍能找到它。
                if (face != null) { facePosition = face.localPosition; faceRotation = face.localRotation; }
            }
            else
            {
                // 简单环境人物与暖棚领班的头、眼睛是独立对象。
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    Transform part = transform.GetChild(i);
                    if (part == headPivot) continue;
                    if (part.name == "头" || part.name == "眼" || part.name == "面部" || part.name == "头发" || part.name == "面部细节")
                        part.SetParent(headPivot, true);
                }
            }
        }

        private void Update()
        {
            if (!built || (target == null && settled)) return;
            float wantedBody = 0f, wantedHead = 0f;
            if (target != null)
            {
                Vector3 delta = target.position - transform.position; delta.y = 0;
                if (delta.sqrMagnitude > .001f)
                {
                    Quaternion worldBase = transform.parent != null ? transform.parent.rotation * baseRotation : baseRotation;
                    float angle = Vector3.SignedAngle(worldBase * Vector3.back, delta.normalized, Vector3.up);
                    wantedBody = Mathf.Sign(angle) * Mathf.Clamp(Mathf.Abs(angle) - 50f, 0f, 35f);
                    wantedHead = Mathf.Clamp(angle - wantedBody, -50f, 50f);
                }
            }
            bodyAngle = Mathf.SmoothDampAngle(bodyAngle, wantedBody, ref bodyVelocity, .18f);
            headAngle = Mathf.SmoothDampAngle(headAngle, wantedHead, ref headVelocity, .15f);
            if (target == null && Mathf.Abs(bodyAngle) < .02f && Mathf.Abs(headAngle) < .02f)
            {
                bodyAngle = headAngle = bodyVelocity = headVelocity = 0f;
                settled = true;
            }
            if (usesMesh) DeformMesh();
            else
            {
                transform.localRotation = baseRotation * Quaternion.Euler(0, bodyAngle, 0);
                headPivot.localRotation = Quaternion.Euler(0, headAngle, 0);
            }
        }

        private void DeformMesh()
        {
            Quaternion torso = Quaternion.Euler(0, bodyAngle, 0);
            Vector3 shiftedHead = torsoPivot + torso * (pivotPosition - torsoPivot);
            headPivot.localPosition = shiftedHead;
            headPivot.localRotation = torso * Quaternion.Euler(0, headAngle, 0);
            if (face != null)
            {
                face.localPosition = shiftedHead + torso * Quaternion.Euler(0, headAngle, 0) * (facePosition - pivotPosition);
                face.localRotation = torso * Quaternion.Euler(0, headAngle, 0) * faceRotation;
            }
            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 v = baseVertices[i];
                float upper = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(torsoPivot.y - .15f, headY - .25f, v.y));
                float head = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(headY - .20f, headY - .06f, v.y));
                Quaternion twist = Quaternion.Euler(0, bodyAngle * upper, 0);
                Vector3 bent = torsoPivot + twist * (v - torsoPivot);
                Quaternion turn = Quaternion.Euler(0, headAngle * head, 0);
                animatedVertices[i] = shiftedHead + turn * (bent - shiftedHead);
                if (i < animatedNormals.Length) animatedNormals[i] = turn * twist * baseNormals[i];
            }
            animatedMesh.vertices = animatedVertices;
            if (animatedNormals.Length == animatedVertices.Length) animatedMesh.normals = animatedNormals;
            animatedMesh.RecalculateBounds();
        }

        private void LateUpdate()
        {
            // 接送者使用骨骼动画；在动画采样后叠加头部朝向。
            if (animatedHead != null && built)
            {
                Quaternion sampled = animatedHead.localRotation;
                // 没有动画轨道覆盖头骨时，先撤销上一帧增量，避免角度逐帧累积。
                if (boneOffsetApplied && Quaternion.Angle(sampled, previousBoneResult) < .001f)
                    sampled *= Quaternion.Inverse(previousBoneOffset);
                previousBoneOffset = Quaternion.Euler(0, headAngle, 0);
                animatedHead.localRotation = sampled * previousBoneOffset;
                previousBoneResult = animatedHead.localRotation;
                boneOffsetApplied = true;
            }
        }

        private void OnDisable()
        {
            target = null;
            if (!built) return;
            if (animatedHead != null && boneOffsetApplied && Quaternion.Angle(animatedHead.localRotation, previousBoneResult) < .001f)
                animatedHead.localRotation *= Quaternion.Inverse(previousBoneOffset);
            boneOffsetApplied = false;
            bodyAngle = headAngle = bodyVelocity = headVelocity = 0;
            settled = true;
            transform.localRotation = baseRotation;
            if (usesMesh && animatedMesh != null)
            {
                animatedMesh.vertices = baseVertices;
                if (baseNormals.Length == baseVertices.Length) animatedMesh.normals = baseNormals;
                animatedMesh.RecalculateBounds();
                if (face != null) { face.localPosition = facePosition; face.localRotation = faceRotation; }
            }
            if (headPivot != null) { headPivot.localPosition = pivotPosition; headPivot.localRotation = Quaternion.identity; }
        }

        private void OnDestroy()
        {
            if (meshFilter != null && originalMesh != null) meshFilter.sharedMesh = originalMesh;
            if (animatedMesh != null) Destroy(animatedMesh);
        }
    }
}
