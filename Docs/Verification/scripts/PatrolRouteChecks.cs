using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YuDian;
[InitializeOnLoad] public static class PatrolRouteChecks {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 const string Key="YuDian.Patrol.BestEvacuatedWave";static bool started;
 static PatrolRouteChecks(){if(SessionState.GetBool("PatrolRouteChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Test(bool v,string s){if(!v)throw new Exception(s);Debug.Log("ROUTE PASS "+s);}
 static void FirstWave(object mission){S(mission,"tutorial",false);C(mission,"StartWave");foreach(Component enemy in (IEnumerable)G(mission,"Enemies")) C(enemy,"TakeHit",int.MaxValue,false,Vector3.forward);C(mission,"Tick",.1f);Test((bool)G(mission,"BetweenWaves")&&(int)G(mission,"Cleared")==1,"first wave cleared");}
 static void ExitAtDoor(object mission,FirstPersonWalker walker){var yard=G(mission,"yard");walker.MoveTo((Transform)G(yard,"Exit"));Physics.SyncTransforms();Test((bool)C(mission,"TryExit"),"green door accepts E request");}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolRouteChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolRouteChecks",false);
  bool had=PlayerPrefs.HasKey(Key);int old=PlayerPrefs.GetInt(Key,0),result=0;
  try{
   PlayerPrefs.DeleteKey(Key);var main=UnityEngine.Object.FindObjectOfType<StationPrototype>();var mission=G(main,"patrol");var walker=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();
   var story=new StationStory(3);Test(!(bool)C(mission,"CanStart",story),"contract locked until repair");story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);Test((bool)C(mission,"CanStart",story),"contract opens after repair");
   var upgrade=mission.GetType().Assembly.GetType("YuDian.PatrolWeapon+Upgrade");
   Test((bool)C(mission,"Begin",story),"take contract");Test((bool)G(mission,"LocksPlayer")&&(int)G(mission,"Wave")==0,"briefing locks first wave");FirstWave(mission);
   Test((bool)C(mission,"ChooseUpgrade",Enum.Parse(upgrade,"Magazine")),"choose magazine");ExitAtDoor(mission,walker);Test((int)G(mission,"Best")==1,"normal extraction records cleared wave");
   Test((bool)C(mission,"Begin",story),"new contract");var weapon=G(mission,"Weapon");Test((int)G(weapon,"Capacity")==8&&(int)G(weapon,"Damage")==2,"new contract resets pistol");FirstWave(mission);
   Test((bool)C(mission,"ChooseUpgrade",Enum.Parse(upgrade,"Magazine")),"choose upgrade before wave 2");C(mission,"StartWave");Test((int)G(mission,"Wave")==2&&!(bool)G(mission,"BetweenWaves"),"second wave active");ExitAtDoor(mission,walker);Test((int)G(mission,"Best")==1&&(int)G(mission,"Cleared")==1,"emergency exit records only completed wave");
   Test((bool)C(mission,"Begin",story),"contract after emergency exit");S(mission,"tutorial",false);C(mission,"StartWave");for(int i=0;i<3;i++){S(mission,"invulnerability",0f);C(mission,"DamagePlayer");}C(mission,"Tick",1f);Test(!(bool)G(mission,"IsRunning")&&(int)G(mission,"Best")==1,"failure keeps successful record");
   Debug.Log("ROUTE CHECKS PASSED");
  }catch(Exception e){Debug.LogException(e);result=1;}
  finally{if(had)PlayerPrefs.SetInt(Key,old);else PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();}
  EditorApplication.Exit(result);
 }
}
