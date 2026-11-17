using UnityEditor;
using UnityEngine;
using YuDian;

namespace YuDian.EditorTools
{
    /// <summary>编辑器启动时把 UnityEditor 能力注入运行时程序集的 EditorHooks。</summary>
    [InitializeOnLoad]
    internal static class EditorHooksInstaller
    {
        internal const string DebugDrawKey = "YuDian.Patrol.DebugDraw";

        static EditorHooksInstaller()
        {
            EditorHooks.ExitPlayMode = () => EditorApplication.isPlaying = false;
            EditorHooks.DebugDrawEnabled = () => EditorPrefs.GetBool(DebugDrawKey, false);
            EditorHooks.DrawSceneLabel = (position, text, color) =>
            {
                Color previous = Handles.color;
                Handles.color = color;
                Handles.Label(position, text);
                Handles.color = previous;
            };
        }
    }
}
