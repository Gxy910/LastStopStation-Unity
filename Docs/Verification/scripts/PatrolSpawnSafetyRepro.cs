using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolSpawnSafetyRepro{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static bool started;
 static PatrolSpawnSafetyRepro(){if(SessionState.GetBool("PatrolSpawnSafetyRepro",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolSpawnSafetyRepro",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolSpawnSafetyRepro",false);try{
 var main=UnityEngine.Object.FindObjectOfType<StationPrototype>();var mission=G(main,"patrol");var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);C(mission,"Begin",story);C(mission,"DebugJumpToWave",80);
 Component boss=null,normal=null;foreach(Component e in (IEnumerable)G(mission,"Enemies")){if(G(e,"EnemyKind").ToString()=="Charger")boss=e;else if(normal==null)normal=e;}
 var phase=boss.GetType().GetField("chargePhase",F);phase.SetValue(boss,Enum.Parse(phase.FieldType,"Windup"));S(boss,"chargeTimer",1.5f);C(normal,"TakeHit",int.MaxValue,false,Vector3.forward);S(mission,"secondEliteWarning",0f);S(mission,"nextSpawnDelay",0f);C(mission,"Tick",.01f);
 Component second=null;foreach(Component e in (IEnumerable)G(mission,"Enemies"))if(e!=boss&&(bool)G(e,"Alive")&&G(e,"EnemyKind").ToString()=="Charger")second=e;
 if(second==null)Debug.Log("SPAWN REPRO second pending or unavailable");else{float distance=Vector3.Distance(boss.transform.position,second.transform.position);float required=boss.GetComponent<CharacterController>().radius+second.GetComponent<CharacterController>().radius;Debug.Log("SPAWN REPRO bossDistance="+distance.ToString("F4")+" colliderRadiusSum="+required.ToString("F4")+" overlap="+(distance<required));}
 C(mission,"Finish",false);EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}}
}
