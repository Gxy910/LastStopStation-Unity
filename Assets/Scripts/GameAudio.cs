using System;
using System.Collections.Generic;
using UnityEngine;

namespace YuDian
{
    /// <summary>为车站原型提供轻量的环境声和即时反馈声，声音在启动时预生成。</summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public enum Cue { Interact, Door, Denied, Reload, ReloadComplete, Swing, Hit, Hurt, WaveStart, WaveClear, Evacuate, KatanaDraw, KatanaSwing }
        internal enum VolumeChannel { Master, Gunfire, Footsteps, Ambience, Dialogue, Effects }
        internal enum UiCue { MenuOpen, MenuClose, Subtitle }

        private const int Rate = 24000;
        private const string VolumeKeyPrefix = "YuDian.AudioVolume.";
        // Old builds wrote out 100% master volume even when only a different slider had changed.
        private const string MasterVolumeKey = VolumeKeyPrefix + "Master.v2";
        private static readonly VolumeChannel[] VolumeChannels = (VolumeChannel[])Enum.GetValues(typeof(VolumeChannel));
        private readonly float[] volumeLevels = new float[VolumeChannels.Length];
        private readonly List<(AudioSource Source, VolumeChannel Channel, float BaseVolume)> volumeRoutes =
            new List<(AudioSource, VolumeChannel, float)>();
        private float originalMasterVolume;
        private bool volumesDirty;
        private readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        private AudioClip[] footsteps;
        private bool usesGeneratedFootsteps;
        private readonly Dictionary<UiCue, AudioClip> uiClips = new Dictionary<UiCue, AudioClip>();
        private AudioSource effects, ambient, footstepSource, reloadEffects, uiSource;
        private AudioClip recordedKatanaDraw, recordedKatanaSwing;
        private FirstPersonWalker walker;
        private CharacterController body;
        private AudioClip wind;
        private float stepDistance = .8f;
        private int footIndex;

        private void Awake()
        {
            originalMasterVolume = AudioListener.volume;
            foreach (VolumeChannel channel in VolumeChannels)
            {
                float defaultVolume = channel == VolumeChannel.Master ? .7f : 1f;
                volumeLevels[(int)channel] = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey(channel), defaultVolume));
            }
            AudioListener.volume = originalMasterVolume * GetVolume(VolumeChannel.Master);
        }

        private static string VolumeKey(VolumeChannel channel) =>
            channel == VolumeChannel.Master ? MasterVolumeKey : VolumeKeyPrefix + channel;

        internal float GetVolume(VolumeChannel channel) => volumeLevels[(int)channel];

        internal void SetVolume(VolumeChannel channel, float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, GetVolume(channel))) return;
            volumeLevels[(int)channel] = value;
            volumesDirty = true;
            if (channel == VolumeChannel.Master)
                AudioListener.volume = originalMasterVolume * value;

            for (int i = volumeRoutes.Count - 1; i >= 0; i--)
            {
                var route = volumeRoutes[i];
                if (route.Source == null) volumeRoutes.RemoveAt(i);
                else if (route.Channel == channel) route.Source.volume = route.BaseVolume * value;
            }
        }

        internal void SaveVolumes()
        {
            if (!volumesDirty) return;
            foreach (VolumeChannel channel in VolumeChannels)
                PlayerPrefs.SetFloat(VolumeKey(channel), GetVolume(channel));
            PlayerPrefs.Save();
            volumesDirty = false;
        }

        internal void RouteVolume(AudioSource source, VolumeChannel channel)
        {
            float baseVolume = source.volume;
            volumeRoutes.RemoveAll(route => route.Source == null);
            volumeRoutes.Add((source, channel, baseVolume));
            source.volume = baseVolume * GetVolume(channel);
        }

        internal AudioSource CreateSource(GameObject owner, float volume, VolumeChannel? channel = null,
            int priority = 128, float spatialBlend = 0f)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.volume = volume;
            source.priority = priority;
            if (channel.HasValue) RouteVolume(source, channel.Value);
            return source;
        }

        public void Configure(FirstPersonWalker player)
        {
            walker = player; body = player.GetComponent<CharacterController>();
            effects = CreateSource(gameObject, .65f, VolumeChannel.Effects, priority: 90);
            footstepSource = CreateSource(gameObject, .65f, VolumeChannel.Footsteps, priority: 90);
            reloadEffects = CreateSource(gameObject, .65f, priority: 90);
            uiSource = CreateSource(gameObject, .30f, VolumeChannel.Effects, priority: 75);
            uiSource.ignoreListenerPause = true;
            foreach (UiCue cue in Enum.GetValues(typeof(UiCue)))
                uiClips[cue] = Resources.Load<AudioClip>("YudianAudio/Interface/" + cue);
            ambient = CreateSource(gameObject, .025f, VolumeChannel.Ambience, priority: 180);
            ambient.loop = true;
            footsteps = Resources.LoadAll<AudioClip>("YudianAudio/Footsteps");
            usesGeneratedFootsteps = footsteps.Length == 0;
            if (usesGeneratedFootsteps)
            {
                footsteps = new AudioClip[4];
                for (int i = 0; i < footsteps.Length; i++) footsteps[i] = MakeFootstep(i);
            }
            clips[Cue.Interact] = MakeTone("交互", .12f, 540, .35f, 11);
            clips[Cue.Door] = MakeNoise("铁门", .34f, 95, .35f, 13);
            clips[Cue.Denied] = MakeTone("受阻", .14f, 150, .28f, 14);
            clips[Cue.Reload] = MakeNoise("换弹开始", .16f, 880, .30f, 15);
            clips[Cue.ReloadComplete] = MakeNoise("换弹完成", .12f, 1120, .28f, 16);
            clips[Cue.Swing] = MakeNoise("挥动", .13f, 380, .26f, 25);
            recordedKatanaDraw = Resources.Load<AudioClip>("YudianAudio/Katana/Draw");
            recordedKatanaSwing = Resources.Load<AudioClip>("YudianAudio/Katana/Swing");
            clips[Cue.KatanaDraw] = recordedKatanaDraw != null ? recordedKatanaDraw : MakeKatanaSound(true);
            clips[Cue.KatanaSwing] = recordedKatanaSwing != null ? recordedKatanaSwing : MakeKatanaSound(false);
            clips[Cue.Hit] = MakeNoise("命中", .12f, 260, .34f, 17);
            clips[Cue.Hurt] = MakeNoise("受伤", .25f, 76, .36f, 18);
            clips[Cue.WaveStart] = MakeTone("来袭警示", .32f, 310, .22f, 19);
            clips[Cue.WaveClear] = MakeTone("清场提示", .26f, 460, .22f, 20);
            clips[Cue.Evacuate] = MakeTone("撤离提示", .24f, 370, .22f, 21);
            wind = MakeWind(); ambient.clip = wind; ambient.Play();
        }

        internal void PlayUi(UiCue cue)
        {
            if (uiSource == null || !uiClips.TryGetValue(cue, out AudioClip clip) || clip == null) return;
            uiSource.clip = clip;
            uiSource.Play();
        }

        public void Play(Cue cue)
        {
            AudioSource source = cue == Cue.Reload || cue == Cue.ReloadComplete ? reloadEffects : effects;
            if (source != null && clips.TryGetValue(cue, out AudioClip clip)) source.PlayOneShot(clip);
        }

        private void Update() { TickFootsteps(Time.deltaTime); }

        internal void TickFootsteps(float dt)
        {
            if (walker == null || body == null || !walker.InputEnabled || !body.isGrounded || walker.HorizontalSpeed < .18f)
            { stepDistance = Mathf.Min(stepDistance, .8f); return; }
            float speed = walker.HorizontalSpeed;
            stepDistance += speed * Mathf.Max(0, dt);
            float stride = walker.IsSprinting ? 1.85f : 1.65f;
            if (stepDistance < stride) return;
            stepDistance -= stride;
            footstepSource.PlayOneShot(footsteps[footIndex++ % footsteps.Length], walker.IsSprinting ? .4f : .25f);
        }

        private static AudioClip MakeKatanaSound(bool drawing)
        {
            float duration = drawing ? .23f : .27f;
            var samples = new float[Mathf.RoundToInt(duration * Rate)];
            var rng = new System.Random(drawing ? 913 : 917);
            float fast = 0f;
            float slow = 0f;
            float phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)Rate;
                float progress = time / duration;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                fast = Mathf.Lerp(fast, noise, drawing ? .28f : .19f + .25f * progress);
                slow = Mathf.Lerp(slow, noise, .04f);
                float sweep = Mathf.Sin(Mathf.PI * progress);
                if (drawing)
                {
                    phase += 2f * Mathf.PI * Mathf.Lerp(920f, 660f, progress) / Rate;
                    float scrape = (fast - slow) * .31f + Mathf.Sin(phase) * .039f;
                    samples[i] = scrape * sweep + slow * .25f * Mathf.Exp(-90f * Mathf.Abs(time - .166f)) * sweep;
                }
                else samples[i] = ((fast - slow) * .44f + slow * .27f) * sweep * sweep;
            }
            return MakeClip(drawing ? "武士刀出鞘" : "武士刀挥砍", samples);
        }

        private static AudioClip MakeFootstep(int seed)
        {
            int n = Mathf.RoundToInt(Rate * .17f); var samples = new float[n]; var rng = new System.Random(302 + seed);
            float filtered = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate; float noise = (float)rng.NextDouble() * 2 - 1;
                filtered = Mathf.Lerp(filtered, noise, .20f);
                float envelope = Mathf.Exp(-t * 23f) * Mathf.Min(1, t * 800f);
                samples[i] = (filtered * .56f + Mathf.Sin(2 * Mathf.PI * (78 + seed * 5) * t) * .32f) * envelope * .55f;
            }
            return MakeClip("脚步" + seed, samples);
        }

        private static AudioClip MakeNoise(string name, float duration, float frequency, float amplitude, int seed)
        {
            int n = Mathf.RoundToInt(Rate * duration); var samples = new float[n]; var rng = new System.Random(seed); float filtered = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, noise = (float)rng.NextDouble() * 2 - 1;
                filtered = Mathf.Lerp(filtered, noise, frequency > 400 ? .65f : .16f);
                float env = Mathf.Exp(-t * (name == "铁门" ? 9f : 22f)) * Mathf.Min(1, t * 900f);
                samples[i] = (filtered * .7f + Mathf.Sin(2 * Mathf.PI * frequency * t) * .3f) * env * amplitude;
            }
            return MakeClip(name, samples);
        }

        private static AudioClip MakeTone(string name, float duration, float frequency, float amplitude, int seed)
        {
            int n = Mathf.RoundToInt(Rate * duration); var samples = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate; float pulse = t < duration * .46f ? 1 : t > duration * .57f ? .8f : 0;
                float envelope = Mathf.Min(1, t * 160) * Mathf.Min(1, (duration - t) * 60) * pulse;
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * (t + .06f * t * t)) * envelope * amplitude;
            }
            return MakeClip(name, samples);
        }

        private static AudioClip MakeWind()
        {
            const float seconds = 8f; int n = Mathf.RoundToInt(Rate * seconds);
            var samples = new float[n]; var rng = new System.Random(88); float filtered = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, noise = (float)rng.NextDouble() * 2 - 1;
                filtered = Mathf.Lerp(filtered, noise, .012f);
                float envelope = .42f + .16f * Mathf.Sin(2 * Mathf.PI * t / seconds) + .09f * Mathf.Sin(4 * Mathf.PI * t / seconds);
                float edge = Mathf.Min(1f, Mathf.Min(t, seconds - t) * 3f);
                samples[i] = (filtered * 1.3f + Mathf.Sin(2 * Mathf.PI * 48 * t) * .12f) * envelope * edge;
            }
            return MakeClip("站外风声", samples);
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, Rate, false); clip.SetData(samples, 0); return clip;
        }

        private void OnDestroy()
        {
            SaveVolumes();
            AudioListener.volume = originalMasterVolume;
            foreach (AudioClip clip in clips.Values)
                if (clip != null && clip != recordedKatanaDraw && clip != recordedKatanaSwing) Destroy(clip);
            if (usesGeneratedFootsteps)
                foreach (AudioClip clip in footsteps) if (clip != null) Destroy(clip);
            if (wind != null) Destroy(wind);
        }
    }
}
