using System;
using UnityEngine;

namespace YuDian
{
    /// <summary>
    /// 运行时程序集与编辑器程序集之间的窄接口。运行时程序集不能直接引用 UnityEditor，
    /// 由 YuDian.Editor 中的 EditorHooksInstaller 在编辑器启动时注入实现；打包后这些委托为空。
    /// </summary>
    public static class EditorHooks
    {
        /// <summary>结束 Play；替代 UnityEditor.EditorApplication.isPlaying = false。</summary>
        public static Action ExitPlayMode;

        /// <summary>Scene 视图调试绘制开关；替代 UnityEditor.EditorPrefs 直读。</summary>
        public static Func<bool> DebugDrawEnabled;

        /// <summary>在 Scene 视图写一段文本标签；替代 UnityEditor.Handles.Label。</summary>
        public static Action<Vector3, string, Color> DrawSceneLabel;

        public static bool ShouldDebugDraw => DebugDrawEnabled != null && DebugDrawEnabled();

        public static void Label(Vector3 position, string text, Color color) => DrawSceneLabel?.Invoke(position, text, color);

        public static void ExitPlay() => ExitPlayMode?.Invoke();
    }
}
