using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YuDian;

public static class EpilogueLayoutMenu
{
    const string PrefabPath = "Assets/Resources/Epilogue/第四章_可视化布局.prefab";

    [MenuItem("余电/第四章/在当前车站添加可视化摆放点")]
    public static void AddLayoutToScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("请先退出运行", "先停止 Play，再添加或移动第四章摆放点。", "知道了"); return; }
        var controller = Object.FindObjectOfType<StationPrototype>(true);
        if (controller == null)
        { EditorUtility.DisplayDialog("未找到车站", "请先在 Unity 中打开 OldStation 场景。", "知道了"); return; }
        var serialized = new SerializedObject(controller);
        var station = serialized.FindProperty("station").objectReferenceValue as GameObject;
        if (station == null) { EditorUtility.DisplayDialog("未找到车站", "场景控制器没有配置车站。", "知道了"); return; }
        var instance = GetOrCreate(station, out bool created);
        if (instance == null) { EditorUtility.DisplayDialog("缺少布局资源", "请等待 Unity 完成资源导入后重试。", "知道了"); return; }
        Selection.activeGameObject = instance;
        EditorUtility.DisplayDialog(created ? "已添加第四章摆放点" : "已经添加", "在 Hierarchy 展开“第四章场景编辑_摆放点”，选择带中文名称的父物体，用 W 拖动。完成后按 Ctrl+S 保存场景。彩色模型仅用于标示位置；运行时会显示实际人物和道具。", "知道了");
    }

    internal static GameObject GetOrCreate(GameObject station, out bool created)
    {
        var existing = station.GetComponentInChildren<EpilogueLayout>(true);
        created = false;
        if (existing != null) return existing.gameObject;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return null;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, station.transform);
        Undo.RegisterCreatedObjectUndo(instance, "添加第四章摆放点");
        instance.name = "第四章场景编辑_摆放点";
        instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
        EditorSceneManager.MarkSceneDirty(station.scene); created = true; return instance;
    }
}
