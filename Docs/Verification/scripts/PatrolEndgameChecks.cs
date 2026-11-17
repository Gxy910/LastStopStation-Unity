using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolEndgameChecks {
 const BindingFlags I=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,SF=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static bool started;static int passes;
 static PatrolEndgameChecks(){if(SessionState.GetBool("PatrolEndgameChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,I);return p!=null?p.GetValue(o):o.GetType().GetField(n,I).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,I).SetValue(o,v);
 static void B(object o,string n,object v)=>o.GetType().GetField("<"+n+">k__BackingField",I).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,I).Invoke(o,a);
 static void Check(bool b,string text){if(!b)throw new Exception(text);passes++;Debug.Log("ENDGAME PASS "+text);}
 static IEnumerable Enemies(object m)=>(IEnumerable)G(m,"Enemies");
 static int Alive(object m)=>(int)C(m,"AliveCount");
 static Component Boss(object m,int index){foreach(Component e in Enemies(m))if(e&&(bool)G(e,"Alive")&&G(e,"EnemyKind").ToString()=="Charger"&&index--==0)return e;return null;}
 static Component Normal(object m){foreach(Component e in Enemies(m))if(e&&(bool)G(e,"Alive")&&G(e,"EnemyKind").ToString()=="Normal")return e;return null;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolEndgameChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolEndgameChecks",false);
  const string recordKey="YuDian.Patrol.BestEvacuatedWave";bool had=PlayerPrefs.HasKey(recordKey);int previous=PlayerPrefs.GetInt(recordKey,0);int code=0;
  try{
   var main=UnityEngine.Object.FindObjectOfType<StationPrototype>();var mission=G(main,"patrol");var walker=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();
   var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);Check((bool)C(mission,"Begin",story),"mission begins");
   var asm=mission.GetType().Assembly;var kind=asm.GetType("YuDian.PatrolEnemy+Kind");var bal=asm.GetType("YuDian.PatrolBalance");var hp=bal.GetMethod("Health",SF);var compose=mission.GetType().GetMethod("WaveComposition",SF);var dual=mission.GetType().GetMethod("IsDoubleEliteWave",SF);
   int[] w={51,60,61,70,71,80,81,90,91,100,101,201};int[] norm={44,44,46,46,48,48,50,50,52,52,54,54};int[] fast={24,24,25,25,26,26,27,27,28,28,28,28};
   for(int i=0;i<w.Length;i++){int a=(int)hp.Invoke(null,new object[]{Enum.Parse(kind,"Normal"),w[i]});int b=(int)hp.Invoke(null,new object[]{Enum.Parse(kind,"Fast"),w[i]});Check(a==norm[i]&&b==fast[i],"health wave "+w[i]);}
   int[] ew={5,10,20,25,40,55,75,80,95,201};int[] ev={28,49,90,120,210,249,300,305,320,320};
   for(int i=0;i<ew.Length;i++)Check((int)hp.Invoke(null,new object[]{Enum.Parse(kind,"Charger"),ew[i]})==ev[i],"elite health wave "+ew[i]);
   int[] cw={51,52,55,56,59,80,101,105,130,201,501};int[] cv={28,29,29,30,30,36,41,42,48,66,141};
   for(int i=0;i<cw.Length;i++){object[] a={cw[i],0,0,false};compose.Invoke(null,a);Check((int)a[1]==cv[i],"wave total "+cw[i]);}
   foreach(int n in new[]{80,105,130,155,180})Check((bool)dual.Invoke(null,new object[]{n}),"double elite wave "+n);
   foreach(int n in new[]{5,25,75,85,100,110})Check(!(bool)dual.Invoke(null,new object[]{n}),"single elite wave "+n);
   var weapon=G(mission,"Weapon");foreach(int n in new[]{1,10,20,51,80,101,501}){C(weapon,"SetWave",n);int h=(int)hp.Invoke(null,new object[]{Enum.Parse(kind,"Normal"),n});Check((int)G(weapon,"MeleeDamage")== (h+2)/3,"baton scales "+n);}
   Check((bool)C(mission,"DebugJumpToWave",80),"jump 80");Check(Alive(mission)==6&&(int)G(mission,"PendingCount")==30&&Boss(mission,0)!=null&&(bool)G(mission,"secondElitePending"),"first boss six alive thirty pending");
   S(mission,"secondEliteWarning",0f);S(mission,"nextSpawnDelay",0f);C(mission,"Tick",.01f);Check((bool)G(mission,"secondElitePending")&&Alive(mission)==6,"second elite waits while full");
   C(Normal(mission),"TakeHit",int.MaxValue,false,Vector3.forward);S(mission,"nextSpawnDelay",0f);C(mission,"Tick",.01f);Check(!(bool)G(mission,"secondElitePending")&&Alive(mission)==6&&Boss(mission,1)!=null,"two elites share six limit");
   var e0=Boss(mission,0);var e1=Boss(mission,1);var phase=e0.GetType().GetField("chargePhase",I);var wind=Enum.Parse(phase.FieldType,"Windup");phase.SetValue(e0,wind);phase.SetValue(e1,wind);S(e0,"chargeTimer",0f);S(e1,"chargeTimer",0f);C(e0,"Tick",.01f);C(e1,"Tick",.01f);Check(G(e0,"chargePhase").ToString()=="Charge"&&G(e1,"chargePhase").ToString()=="Charge","simultaneous charge allowed");
   Check((bool)C(mission,"DebugJumpToWave",105),"jump 105");Check((int)G(mission,"PendingCount")+Alive(mission)==42&&(bool)G(mission,"secondElitePending"),"all 105 enemies accounted including second boss");
   foreach(Component e in Enemies(mission))if(e&&(bool)G(e,"Alive"))C(e,"TakeHit",int.MaxValue,false,Vector3.forward);
   C(mission,"Tick",1f);Check(!(bool)G(mission,"BetweenWaves")&&(bool)G(mission,"secondElitePending"),"pending elite prevents early clear");
   Check((bool)C(mission,"DebugJumpToWave",5),"jump 5");Check((int)G(Boss(mission,0),"attackDamage")==1,"early elite one damage");
   Check((bool)C(mission,"DebugJumpToWave",25),"jump 25");Check((int)G(Boss(mission,0),"attackDamage")==2,"later elite two damage");
   S(mission,"health",3);S(mission,"armor",1);S(mission,"invulnerability",0f);C(mission,"EnemyDamagePlayer",2);Check((int)G(mission,"Health")==3&&(int)G(mission,"Armor")==0,"armor absorbs entire elite hit");
   S(mission,"invulnerability",0f);C(mission,"EnemyDamagePlayer",2);Check((int)G(mission,"Health")==1,"elite removes two health");
   S(mission,"health",3);S(mission,"invulnerability",0f);C(mission,"EnemyDamagePlayer",2);C(mission,"EnemyDamagePlayer",2);Check((int)G(mission,"Health")==1,"shared invulnerability stops immediate repeat");
   Check((bool)C(mission,"DebugJumpToWave",51),"jump 51");var root=((GameObject)G(G(mission,"yard"),"Root")).transform;
   foreach(Component e in Enemies(mission))e.transform.position=root.TransformPoint(new Vector3(8,.05f,15));
   var p=new GameObject("test player").transform;p.SetPositionAndRotation(root.TransformPoint(new Vector3(-6.5f,.05f,4)),root.rotation);walker.MoveTo(p);UnityEngine.Object.Destroy(p.gameObject);Physics.SyncTransforms();
   B(weapon,"Loaded",1000);B(weapon,"Reserve",10);S(mission,"headshotProgress",0);S(mission,"wave",50);
   ShootKill(mission,weapon,walker,root,kind,"Normal");Check((int)G(mission,"HeadshotProgress")==0,"wave 50 kill does not count");S(mission,"wave",51);
   for(int i=0;i<3;i++)ShootKill(mission,weapon,walker,root,kind,"Normal");Check((int)G(mission,"HeadshotProgress")==3&&(int)G(weapon,"Reserve")==10,"three kills accumulated");
   S(mission,"wave",52);for(int i=0;i<2;i++)ShootKill(mission,weapon,walker,root,kind,"Normal");Check((int)G(mission,"HeadshotProgress")==0&&(int)G(weapon,"Reserve")==14,"cross wave five kills adds four");
   B(weapon,"Reserve",179);S(mission,"headshotProgress",4);ShootKill(mission,weapon,walker,root,kind,"Charger");Check((int)G(weapon,"Reserve")==180&&(int)G(mission,"HeadshotProgress")==0,"elite head kill and reserve cap");
   B(weapon,"Reserve",0);Check((int)C(weapon,"Restock",14)==50&&(int)C(weapon,"Restock",500)==50,"wave restock remains capped at 50");
   int record=(int)G(mission,"Best");S(mission,"cleared",300);C(mission,"Finish",true);Check(PlayerPrefs.GetInt(recordKey,0)==record,"debug exit does not change record");
   Check((bool)C(mission,"Begin",story),"restart after exit");Check((int)G(mission,"HeadshotProgress")==0&&(int)G(G(mission,"Weapon"),"Capacity")==8,"restart clears progression");C(mission,"Finish",false);
   Debug.Log("ENDGAME CHECKS PASSED "+passes);
  }catch(Exception ex){code=1;Debug.LogException(ex);}finally{if(had)PlayerPrefs.SetInt(recordKey,previous);else PlayerPrefs.DeleteKey(recordKey);PlayerPrefs.Save();}
  EditorApplication.Exit(code);
 }
 static void ShootKill(object mission,object weapon,FirstPersonWalker walker,Transform root,Type kind,string type){
  C(mission,"Spawn",Enum.Parse(kind,type),0);Component e=null;foreach(Component x in Enemies(mission))if(x&&(bool)G(x,"Alive")&&G(x,"EnemyKind").ToString()==type)e=x;
  e.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,9));e.transform.rotation=root.rotation*Quaternion.Euler(0,180,0);S(e,"health",4);
  var hit=(SphereCollider)G(e,"headHitBox");walker.ViewCamera.transform.LookAt(hit.transform.TransformPoint(hit.center));Physics.SyncTransforms();C(weapon,"Tick",1f);var shot=C(weapon,"Attack");Check(shot.ToString()=="Headshot"&&!(bool)G(e,"Alive"),"real headshot kill "+type);C(mission,"HandleShot",shot);
 }
}
