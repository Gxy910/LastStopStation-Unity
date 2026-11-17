using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolHeadMotionRepro {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static bool started;
 static PatrolHeadMotionRepro(){if(SessionState.GetBool("PatrolHeadMotionRepro",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolHeadMotionRepro",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolHeadMotionRepro",false);
 try{
  var main=UnityEngine.Object.FindObjectOfType<StationPrototype>();var mission=G(main,"patrol");var walker=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();
  var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);C(mission,"Begin",story);S(mission,"tutorial",false);S(mission,"wave",2);S(mission,"betweenWaves",true);S(mission,"upgradeChosen",true);C(mission,"StartWave");
  Component fast=null;foreach(Component e in (IEnumerable)G(mission,"Enemies")){if(G(e,"EnemyKind").ToString()=="Fast")fast=e;else e.gameObject.SetActive(false);}if(!fast)throw new Exception("fast missing");
  var yard=G(mission,"yard");var root=((GameObject)G(yard,"Root")).transform;var weapon=G(mission,"Weapon");var animator=fast.GetComponentInChildren<AssetAnimationPlayer>();var model=(GameObject)G(animator,"model");var collider=(SphereCollider)G(fast,"headHitBox");Transform bone=null;foreach(var t in model.GetComponentsInChildren<Transform>(true))if(t.name=="Head"){bone=t;break;}if(!bone)throw new Exception("head bone missing");
  Vector3 initialCenter=collider.transform.TransformPoint(collider.center);Vector3 offset=bone.InverseTransformPoint(initialCenter);var clip=Array.Find(Resources.LoadAll<AnimationClip>("Quaternius/Zombie_Legacy"),x=>x.name=="Zombie|ZombieRun");if(!clip)throw new Exception("run clip missing");
  S(fast,"health",10000);weapon.GetType().GetField("<Loaded>k__BackingField",F).SetValue(weapon,1000);int pass=0,fail=0;
  foreach(float distance in new[]{2f,6f,12f})foreach(float time in new[]{0f,.15f,.30f,.45f,.60f})foreach(float lateral in new[]{-.18f,0f,.18f}){
   var spawn=new GameObject("probe player").transform;spawn.SetPositionAndRotation(root.TransformPoint(new Vector3(-6.5f,.05f,3)),root.rotation);walker.MoveTo(spawn);UnityEngine.Object.Destroy(spawn.gameObject);
   fast.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,3+distance));fast.transform.rotation=root.rotation*Quaternion.Euler(0,180,0);
   clip.SampleAnimation(model,time);var sync=fast.GetType().GetMethod("SyncHeadHitBox",F);if(sync!=null)sync.Invoke(fast,null);Vector3 aimPoint=bone.TransformPoint(offset)+root.right*lateral;Vector3 center=collider.transform.TransformPoint(collider.center);
   walker.ViewCamera.transform.LookAt(aimPoint);Physics.SyncTransforms();C(weapon,"Tick",1f);string shot=C(weapon,"Attack").ToString();if(shot=="Headshot")pass++;else fail++;
   Debug.Log("HEAD REPRO dist="+distance+" time="+time+" lateral="+lateral+" headOffset="+Vector3.Distance(aimPoint,center).ToString("F3")+" result="+shot);
  }
  Debug.Log("HEAD REPRO SUMMARY pass="+pass+" fail="+fail);C(mission,"Finish",false);EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
