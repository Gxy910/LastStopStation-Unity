using System;
using System.Collections.Generic;
using UnityEngine;

namespace YuDian
{
    /// <summary>按每句文字生成短促拟声音节；切句先停止上一句。</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class DialogueVoice : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private AudioSource source;
        private AudioClip activeClip;
        private DialogueVoiceSettings settings;
        public int SyllableCount { get; private set; }
        public float Duration { get; private set; }
        public AudioClip ActiveClip => activeClip;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            settings = Resources.Load<DialogueVoiceSettings>("DialogueVoiceSettings");
        }

        public void Speak(string speaker, string line, bool serious)
        {
            StopVoice();
            var profile = settings != null ? settings.Find(speaker) : null;
            if (profile == null || string.IsNullOrEmpty(line)) return;
            var samples = new List<float>();
            int syllables = 0;
            foreach (char ch in line)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    AddSyllable(samples, profile, ch, syllables++, serious);
                    AddSilence(samples, .008f);
                }
                else if ("，、；：,:;".IndexOf(ch) >= 0) AddSilence(samples, .12f);
                else if ("。！？.!?…".IndexOf(ch) >= 0) AddSilence(samples, .20f);
            }
            if (syllables == 0) return;
            SyllableCount = syllables;
            Duration = samples.Count / (float)SampleRate;
            activeClip = AudioClip.Create("拟声_" + speaker, samples.Count, 1, SampleRate, false);
            activeClip.SetData(samples.ToArray(), 0);
            source.clip = activeClip;
            source.Play();
        }

        private static void AddSyllable(List<float> samples, DialogueVoiceSettings.Voice p, char ch, int index, bool serious)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * p.secondsPerCharacter));
            float pitch = p.pitch * (1f + (((ch * 17 + index * 7) % 13) - 6) * (serious ? .004f : .012f));
            float amp = p.volume * (serious ? .48f : 1f);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * i / count), .7f);
                float phase = 2f * Mathf.PI * pitch * t;
                float voice = Mathf.Sin(phase) + .38f * Mathf.Sin(2f * phase) + (.15f + p.brightness * .18f) * Mathf.Sin(3f * phase);
                samples.Add(voice * envelope * amp * .48f);
            }
        }

        private static void AddSilence(List<float> samples, float seconds)
        {
            int count = Mathf.RoundToInt(seconds * SampleRate);
            for (int i = 0; i < count; i++) samples.Add(0f);
        }

        public void StopVoice()
        {
            if (source != null) { source.Stop(); source.clip = null; }
            if (activeClip != null) Destroy(activeClip);
            activeClip = null; SyllableCount = 0; Duration = 0f;
        }
        private void OnDestroy() { StopVoice(); }
    }
}
