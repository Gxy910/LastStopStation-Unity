using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolSideResumeChecks{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static bool started;
 static PatrolSideResumeChecks(){if(SessionState.GetBool("PatrolSideResumeChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Debug.Log("SIDE RESUME PASS "+text);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolSideResumeChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolSideResumeChecks",false);try{
  var mission=G(UnityEngine.Object.FindObjectOfType<StationPrototype>(),"patrol");var walker=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);C(mission,"Begin",story);C(mission,"DebugJumpToWave",105);
  var yard=G(mission,"yard");var root=((GameObject)G(yard,"Root")).transform;var side=(ICollection)G(mission,"sidePending");Check(side.Count==2&&(float)G(mission,"sideWarning")==3f,"side entrance starts with two queued and three second warning");
  S(mission,"secondEliteWarning",100f);foreach(Component e in (IEnumerable)G(mission,"Enemies"))if((bool)G(e,"Alive"))C(e,"TakeHit",int.MaxValue,false,Vector3.forward);
  S(mission,"nextSpawnDelay",0f);C(mission,"Tick",1f);Check(side.Count==2,"side monsters cannot enter before warning");S(mission,"nextSpawnDelay",0f);C(mission,"Tick",2.1f);Check(side.Count==1&&(int)C(mission,"AliveCount")<=6,"side monster enters after warning and under limit");
  C(mission,"DebugJumpToWave",105);side=(ICollection)G(mission,"sidePending");S(mission,"secondEliteWarning",100f);foreach(Component e in (IEnumerable)G(mission,"Enemies"))if((bool)G(e,"Alive"))C(e,"TakeHit",int.MaxValue,false,Vector3.forward);
  var point=new GameObject("side test point").transform;point.SetPositionAndRotation(root.TransformPoint(new Vector3(8.8f,.05f,10.5f)),root.rotation);walker.MoveTo(point);UnityEngine.Object.Destroy(point.gameObject);Physics.SyncTransforms();
  S(mission,"sideWarning",0f);S(mission,"nextSpawnDelay",0f);C(mission,"Tick",.01f);Check((bool)G(mission,"sideFallbackAnnounced")&&side.Count==2&&(float)G(mission,"sideWarning")>2.9f,"unsafe side triggers new fallback warning");
  S(mission,"nextSpawnDelay",0f);C(mission,"Tick",2f);Check(side.Count==2,"fallback waits for warning");S(mission,"nextSpawnDelay",0f);C(mission,"Tick",1.1f);Check(side.Count==1,"fallback spawns through original entrance");
  Component spawned=null;foreach(Component e in (IEnumerable)G(mission,"Enemies"))if(e&&(bool)G(e,"Alive"))spawned=e;var local=root.InverseTransformPoint(spawned.transform.position);Check(Mathf.Abs(local.x)>5f&&local.z>13f,"fallback spawn stays at original entrance");
  Debug.Log("SIDE RESUME CHECKS PASSED");C(mission,"Finish",false);EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
