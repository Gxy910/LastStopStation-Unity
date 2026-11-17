using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolMeleeChecks{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static bool started;
 static PatrolMeleeChecks(){if(SessionState.GetBool("PatrolMeleeChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Test(bool ok,string label){if(!ok)throw new Exception(label);Debug.Log("MELEE PASS "+label);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolMeleeChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolMeleeChecks",false);
 try{
  var mission=G(UnityEngine.Object.FindObjectOfType<StationPrototype>(),"patrol");var walker=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();
  var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);Test((bool)C(mission,"Begin",story),"begin");S(mission,"tutorial",false);
  var yard=G(mission,"yard");var root=((GameObject)G(yard,"Root")).transform;var weapon=G(mission,"Weapon");
  foreach(int wave in new int[]{1,10,20,50}){
   foreach(Component old in (IEnumerable)G(mission,"Enemies"))if(old)old.gameObject.SetActive(false);
   S(mission,"wave",wave-1);S(mission,"betweenWaves",wave>1);S(mission,"upgradeChosen",true);C(mission,"StartWave");Component target=null;
   foreach(Component e in (IEnumerable)G(mission,"Enemies")){e.transform.position=root.TransformPoint(new Vector3(8,.05f,15));if(target==null&&G(e,"EnemyKind").ToString()=="Normal")target=e;}
   Test(target!=null,"normal available wave "+wave);var spawn=new GameObject("melee test point").transform;spawn.SetPositionAndRotation(root.TransformPoint(new Vector3(-6.5f,.05f,4)),root.rotation);walker.MoveTo(spawn);UnityEngine.Object.Destroy(spawn.gameObject);C(weapon,"Equip",true);
   for(int hit=1;hit<=3;hit++){
    target.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,6));walker.ViewCamera.transform.LookAt(target.transform.position+Vector3.up*1.05f);Physics.SyncTransforms();C(weapon,"Tick",1f);
    Test(C(weapon,"Attack").ToString()=="Hit","stick hit "+hit+" wave "+wave);
    var blood=(GameObject[])G(weapon,"blood");int active=0;foreach(var d in blood)if(d.activeSelf)active++;
    Test(active>0,"blood remains visible after hit "+hit+" wave "+wave);
    Test((bool)G(target,"Alive")==(hit<3),"three hits to defeat wave "+wave+" hit "+hit);
   }
  }
  C(mission,"Finish",false);Debug.Log("MELEE CHECKS PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
