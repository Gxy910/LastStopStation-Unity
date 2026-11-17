using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolSpawnSafetyChecks{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static bool started;
 static PatrolSpawnSafetyChecks(){if(SessionState.GetBool("PatrolSpawnSafetyChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Check(bool b,string t){if(!b)throw new Exception(t);Debug.Log("SPAWN SAFETY PASS "+t);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolSpawnSafetyChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolSpawnSafetyChecks",false);try{
  var mission=G(UnityEngine.Object.FindObjectOfType<StationPrototype>(),"patrol");var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);C(mission,"Begin",story);C(mission,"DebugJumpToWave",80);
  var yard=G(mission,"yard");var root=((GameObject)G(yard,"Root")).transform;Component first=null;
  foreach(Component e in (IEnumerable)G(mission,"Enemies")){if((bool)G(e,"Alive")&&G(e,"EnemyKind").ToString()=="Charger"&&first==null)first=e;else if((bool)G(e,"Alive"))C(e,"TakeHit",int.MaxValue,false,Vector3.forward);}
  if(!first)throw new Exception("first elite absent");var phase=first.GetType().GetField("chargePhase",F);phase.SetValue(first,Enum.Parse(phase.FieldType,"Windup"));S(first,"chargeTimer",100f);
  int i=(int)G(mission,"spawnedThisWave")/2;var left=GameObject.CreatePrimitive(PrimitiveType.Cube);var right=GameObject.CreatePrimitive(PrimitiveType.Cube);
  left.transform.position=(Vector3)C(yard,"SpawnPosition",0,i)+Vector3.up*1.2f;right.transform.position=(Vector3)C(yard,"SpawnPosition",1,i)+Vector3.up*1.2f;left.transform.localScale=right.transform.localScale=new Vector3(2f,2.4f,2f);Physics.SyncTransforms();
  S(mission,"secondEliteWarning",0f);S(mission,"nextSpawnDelay",0f);C(mission,"Tick",.01f);
  Check((bool)G(mission,"secondElitePending"),"second elite waits when both entries blocked");Check(!(bool)G(mission,"BetweenWaves"),"blocked entry cannot clear wave");
  first.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,10));left.SetActive(false);Physics.SyncTransforms();S(mission,"nextSpawnDelay",0f);C(mission,"Tick",.01f);
  Check(!(bool)G(mission,"secondElitePending"),"second elite enters available original gate");Component second=null;foreach(Component e in (IEnumerable)G(mission,"Enemies"))if(e!=first&&(bool)G(e,"Alive")&&G(e,"EnemyKind").ToString()=="Charger")second=e;
  Check(second!=null,"second elite alive");Check(Vector3.Distance(first.transform.position,second.transform.position)>=first.GetComponent<CharacterController>().radius+second.GetComponent<CharacterController>().radius,"elites do not overlap");
  var player=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();var d=second.transform.position-player.transform.position;d.y=0;Check(d.magnitude>=4f,"elite does not spawn beside player");
  left.SetActive(false);right.SetActive(false);UnityEngine.Object.Destroy(left);UnityEngine.Object.Destroy(right);C(mission,"Finish",false);Debug.Log("SPAWN SAFETY CHECKS PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
