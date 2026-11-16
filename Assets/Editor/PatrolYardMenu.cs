using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YuDian;

public static class PatrolYardMenu
{
    const string PrefabPath = "Assets/Resources/PatrolYardEditable/清障货场.prefab";
    [MenuItem("余电/清障委托/在场景添加可编辑货场")]
    public static void AddYard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("请先退出运行", "停止 Play 后，再添加和编辑货场。", "知道了"); return; }
        var controller = Object.FindObjectOfType<StationPrototype>(true);
        if (controller == null)
        { EditorUtility.DisplayDialog("未找到车站", "请先打开 OldStation 场景。", "知道了"); return; }
        var layout = GetOrCreate(controller, out bool created);
        if (layout == null)
        { EditorUtility.DisplayDialog("缺少货场资源", "请等待资源导入完成后重试。", "知道了"); return; }
        Selection.activeGameObject = layout.gameObject;
        EditorUtility.DisplayDialog(created ? "已添加可编辑货场" : "场景已有货场",
            "在 Hierarchy 选择“清障委托 · 旧货场”，将鼠标移到 Scene 后按 F 定位。展开后选择物体，按 W 移动，按 Ctrl+S 保存。中央货箱和撤离门请移动标有“整体”的父物体。", "知道了");
    }

    internal static PatrolYardLayout GetOrCreate(StationPrototype controller, out bool created)
    {
        created = false;
        foreach (var existing in Object.FindObjectsOfType<PatrolYardLayout>(true))
            if (existing.gameObject.scene == controller.gameObject.scene) return existing;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return null;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(instance, "添加可编辑清障货场");
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        created = true;
        return instance.GetComponent<PatrolYardLayout>();
    }
}
