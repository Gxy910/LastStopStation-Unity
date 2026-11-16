using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YuDian;

public static class PatrolPostingMenu
{
    [MenuItem("余电/清障委托/在车站添加委托板摆放点")]
    public static void AddPostingMarker()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("请先退出运行", "停止 Play 后，再添加或移动委托板摆放点。", "知道了"); return; }
        var controller = Object.FindObjectOfType<StationPrototype>(true);
        if (controller == null)
        { EditorUtility.DisplayDialog("未找到车站", "请先打开 OldStation 场景。", "知道了"); return; }
        var station = new SerializedObject(controller).FindProperty("station").objectReferenceValue as GameObject;
        if (station == null)
        { EditorUtility.DisplayDialog("未找到车站", "场景控制器没有配置车站。", "知道了"); return; }
        var marker = GetOrCreate(station, out bool created);
        Selection.activeGameObject = marker.gameObject;
        EditorUtility.DisplayDialog(created ? "已添加委托板摆放点" : "已有委托板摆放点",
            "在 Hierarchy 选中“清障委托板_摆放点”，在 Scene 中按 W 移动，按 E 旋转。完成后按 Ctrl+S 保存场景。运行时委托板和灯会出现在这个位置。", "知道了");
    }

    internal static PatrolPostingMarker GetOrCreate(GameObject station, out bool created)
    {
        var marker = station.GetComponentInChildren<PatrolPostingMarker>(true);
        created = false;
        if (marker != null) return marker;
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "清障委托板_摆放点";
        obj.transform.SetParent(station.transform, false);
        var dispatch = station.transform.Find("路哨站长");
        Vector3 offset = dispatch != null ? dispatch.localPosition : new Vector3(0, 0, 44);
        obj.transform.localPosition = offset + new Vector3(1.85f, 1.62f, -4.52f);
        obj.transform.localScale = new Vector3(1.38f, .96f, .12f);
        obj.GetComponent<Collider>().enabled = false;
        marker = obj.AddComponent<PatrolPostingMarker>();
        Undo.RegisterCreatedObjectUndo(obj, "添加清障委托板摆放点");
        EditorSceneManager.MarkSceneDirty(station.scene);
        created = true;
        return marker;
    }
}
