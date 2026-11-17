using UnityEngine;

namespace YuDian
{
    /// <summary>按战斗状态播放资源自带的骨骼动作，不负责移动角色。</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class AssetAnimationPlayer : MonoBehaviour
    {
        private GameObject model;
        private AnimationClip[] clips;
        private AnimationClip current;
        private float clock;
        private float speed = 1;
        private float lean;
        private Quaternion baseRotation;
        private bool looping;
        public event System.Action PoseUpdated;

        public void Configure(GameObject animatedModel, string resourcePath)
        {
            model = animatedModel;
            // 非 Legacy 动画在独立游戏里需要 Animator 才能采样；编辑器不一定暴露此问题。
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            baseRotation = model.transform.localRotation;
            clips = Resources.LoadAll<AnimationClip>(resourcePath);
        }

        public void Play(string name, bool loop, float playbackSpeed = 1f)
        {
            speed = playbackSpeed;
            string wanted = name.Contains("|") ? name : "CharacterArmature|" + name;
            if (current != null && current.name == wanted && looping == loop) return;
            foreach (AnimationClip clip in clips)
            {
                if (clip.name != wanted) continue;
                current = clip;
                looping = loop;
                clock = 0;
                current.SampleAnimation(model, 0);
                PoseUpdated?.Invoke();
                return;
            }
        }

        public void SetLean(float degrees) => lean = degrees;

        private void Update()
        {
            if (model == null || current == null) return;
            clock += Time.deltaTime * speed;
            float time = looping && current.length > .001f
                ? clock % current.length : Mathf.Min(clock, current.length);
            current.SampleAnimation(model, time);
            model.transform.localRotation = Quaternion.Euler(lean, 0, 0) * baseRotation;
            PoseUpdated?.Invoke();
        }
    }
}
