using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YuDian;
public sealed class PatrolDebugWindow : EditorWindow
{
    private int targetWave = 51;
    [MenuItem("余电/清障调试工具")]
    private static void OpenWindow() => GetWindow<PatrolDebugWindow>("清障调试");
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("先进入货场委托；跳波仅作用于当前测试委托，本次撤离不会更新最高纪录。", MessageType.Info);
        bool enabled = EditorPrefs.GetBool("YuDian.Patrol.DebugDraw", false);
        bool next = EditorGUILayout.Toggle("Scene 显示受击区和射线", enabled);
        if (next != enabled) { EditorPrefs.SetBool("YuDian.Patrol.DebugDraw", next); SceneView.RepaintAll(); }
        targetWave = EditorGUILayout.IntField("目标波数", targetWave);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("跳转当前委托"))
            {
                var station = FindObjectOfType<StationPrototype>();
                var field = typeof(StationPrototype).GetField("patrol", BindingFlags.Instance | BindingFlags.NonPublic);
                object mission = station != null ? field.GetValue(station) : null;
                var method = mission?.GetType().GetMethod("DebugJumpToWave", BindingFlags.Instance | BindingFlags.NonPublic);
                bool ok = method != null && (bool)method.Invoke(mission, new object[] { Mathf.Max(1, targetWave) });
                if (!ok) EditorUtility.DisplayDialog("清障调试", "请先接取清障委托并进入货场。", "知道了");
            }
        }
    }
}
