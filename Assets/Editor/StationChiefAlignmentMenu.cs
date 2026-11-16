using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YuDian;

public static class StationChiefAlignmentMenu
{
    [MenuItem("余电/场景修正/对齐路哨站长的身体和面部")]
    public static void AlignChief()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("请先退出运行", "先停止 Play，再对齐路哨站长。", "知道了"); return; }
        var controller=UnityEngine.Object.FindObjectOfType<StationPrototype>(true);
        var station=controller!=null?new SerializedObject(controller).FindProperty("station").objectReferenceValue as GameObject:null;
        var actor=station!=null?station.transform.Find("路哨站长"):null;
        if(actor==null){EditorUtility.DisplayDialog("没有找到站长", "请打开 OldStation 场景。", "知道了");return;}
        try {
            bool changed=Align(actor);
            Selection.activeGameObject=actor.gameObject;
            EditorUtility.DisplayDialog(changed?"已对齐站长":"站长已经对齐",changed?"已保持模型和手持道具的原位置，将站长的交互与面部位置移到一起。请检查效果并按 Ctrl+S 保存。":"模型位置已经与站长父物体对齐。", "知道了");
        }catch(Exception e){EditorUtility.DisplayDialog("未进行更改", e.Message, "知道了");}
    }
    internal static bool Align(Transform actor)
    {
        var model=actor.Find("低模外观");var prop=actor.Find("手持道具");
        if(model==null)throw new InvalidOperationException("没有找到站长的低模外观。");
        if(Quaternion.Angle(model.localRotation,Quaternion.identity)>.01f || Vector3.Distance(model.localScale,Vector3.one)>.001f)
            throw new InvalidOperationException("模型已旋转或缩放，请先保留当前场景并手动检查。");
        var offset=model.localPosition;
        if(offset.sqrMagnitude<.00000001f)return false;
        var objects=prop!=null?new UnityEngine.Object[]{actor,model,prop}:new UnityEngine.Object[]{actor,model};
        Undo.RecordObjects(objects,"对齐站长位置");
        Vector3 modelPosition=model.position; Quaternion modelRotation=model.rotation;
        Vector3 propPosition=prop!=null?prop.position:Vector3.zero;Quaternion propRotation=prop!=null?prop.rotation:Quaternion.identity;
        actor.position+=actor.TransformVector(offset);
        model.SetPositionAndRotation(modelPosition,modelRotation);
        if(prop!=null)prop.SetPositionAndRotation(propPosition,propRotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        if(prop!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(prop);
        EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
        return true;
    }
}
