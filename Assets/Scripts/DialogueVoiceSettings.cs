using System;
using UnityEngine;

namespace YuDian
{
    [CreateAssetMenu(menuName = "余电/拟声配音配置")]
    public sealed class DialogueVoiceSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Voice
        {
            public string speaker;
            [Range(110f, 400f)] public float pitch = 200f;
            [Range(.035f, .16f)] public float secondsPerCharacter = .065f;
            [Range(0f, 1f)] public float volume = .24f;
            [Range(0f, 1f)] public float brightness = .5f;
        }
        public Voice[] voices = new Voice[0];
        public Voice Find(string speaker)
        {
            if (voices != null) foreach (Voice voice in voices)
                if (voice != null && voice.speaker == speaker) return voice;
            return null;
        }
    }
}
